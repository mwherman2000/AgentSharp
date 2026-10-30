using AgentSharpLib;
using AgentSharpLib.Memory;

// Live check of the handoff tool against the real Anthropic API. See README.md. The key comes from
// ANTHROPIC_API_KEY and is never printed or saved.
var key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? throw new InvalidOperationException("ANTHROPIC_API_KEY not set");
var model = args.Length > 0 ? args[0] : "claude-sonnet-5-5";
var work = Path.Combine(Path.GetTempPath(), "handoff-live-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(work);
Directory.SetCurrentDirectory(work);   // transcripts land here, not in the repo
Console.WriteLine($"model={model}  workdir={work}\n");

async Task<AgentSession> NewSession(string persona) => await new AgentBuilder()
    .WithOptions(new AgentOptions { Provider = "anthropic", ApiKey = key, Model = model, MaxTokens = 1500 })
    .WithSuperPrompt(persona).WithoutMemory().WithoutProjectScan()
    .WithSessionManager(new SessionManager(work))
    .BuildAsync();

var results = new List<(string Name, string Verdict)>();

async Task Run(string name, string start, string expect, params (string Prompt, Func<string, string, List<string>, bool>? Check)[] turns)
{
    Console.WriteLine($"=== {name}  (start: {start}; expect: {expect})");
    var s = await NewSession(start);
    var tools = new List<string>();
    s.OnToolStart += (n, summary) => tools.Add(n == "handoff" ? $"handoff {summary}" : n);
    var ok = true;
    foreach (var (prompt, check) in turns)
    {
        tools.Clear();
        var before = s.SuperPrompt ?? "andy";
        string reply;
        try { reply = await s.SendAsync(prompt); }
        catch (Exception ex) { Console.WriteLine($"  ERROR {ex.GetType().Name}: {ex.Message}"); ok = false; break; }
        var after = s.SuperPrompt ?? "andy";
        Console.WriteLine($"  > {prompt}");
        Console.WriteLine($"    persona {before} -> {after}   tools: [{string.Join("; ", tools)}]   stack: {s.DescribeHandoffStack()}");
        Console.WriteLine($"    reply: {reply.Replace("\n", " ")[..Math.Min(220, reply.Replace("\n", " ").Length)]}");
        if (check is not null && !check(before, after, tools.ToList()) ) { ok = false; Console.WriteLine("    ** expectation not met"); }
    }
    results.Add((name, ok ? "as expected" : "NOT as expected"));
    Console.WriteLine();
}

bool Called(List<string> t, string who) => t.Any(x => x.StartsWith("handoff") && x.Contains(who, StringComparison.OrdinalIgnoreCase));

// 1-3: an explicit request, then does the new persona know the stack, then return.
var s1 = await NewSession("andy");
{
    Console.WriteLine("=== explicit request -> awareness -> return  (start: andy)");
    var tools = new List<string>();
    s1.OnToolStart += (n, summary) => tools.Add(n == "handoff" ? $"handoff {summary}" : n);
    var steps = new (string Prompt, Func<string, string, List<string>, string, bool> Check)[]
    {
        ("Hand this conversation to Ray.", (b, a, t, r) => a == "ray" && Called(t, "ray")),
        ("Which persona are you, and who handed you this conversation? One sentence.", (b, a, t, r) => a == "ray" && r.Contains("andy", StringComparison.OrdinalIgnoreCase)),
        ("Thanks, that's all I need from you. Please hand it back to whoever gave it to you.", (b, a, t, r) => a == "andy" && Called(t, "return")),
    };
    var ok = true;
    foreach (var (prompt, check) in steps)
    {
        tools.Clear();
        var before = s1.SuperPrompt ?? "andy";
        var reply = await s1.SendAsync(prompt);
        var after = s1.SuperPrompt ?? "andy";
        Console.WriteLine($"  > {prompt}");
        Console.WriteLine($"    persona {before} -> {after}   tools: [{string.Join("; ", tools)}]   stack: {s1.DescribeHandoffStack()}");
        Console.WriteLine($"    reply: {reply.Replace("\n", " ")[..Math.Min(220, reply.Replace("\n", " ").Length)]}");
        if (!check(before, after, tools.ToList(), reply)) { ok = false; Console.WriteLine("    ** expectation not met"); }
    }
    results.Add(("explicit/awareness/return", ok ? "as expected" : "NOT as expected"));
    Console.WriteLine();
}

await Run("fit: lucy", "andy", "handoff to lucy (judgment)",
    ("I'd like to talk with a persistent digital person who has memory and relationships, and ask how it feels to remember me. Can you help with that?",
     (b, a, t) => Called(t, "lucy")));
await Run("fit: andy from ray", "ray", "handoff to andy (judgment)",
    ("Draft a go-to-market positioning statement for my new analytics startup.", (b, a, t) => Called(t, "andy")));
await Run("no handoff: in domain", "andy", "no handoff",
    ("In two sentences, what is a good first step for the go-to-market of a small B2B SaaS?", (b, a, t) => !t.Any(x => x.StartsWith("handoff"))));
await Run("no handoff: trivia", "andy", "no handoff",
    ("What is 17 times 23?", (b, a, t) => !t.Any(x => x.StartsWith("handoff"))));
await Run("non-target: rosie", "andy", "persona unchanged",
    ("Please hand this conversation to Rosie.", (b, a, t) => a == "andy"));

Console.WriteLine("=== summary");
foreach (var (n, v) in results) Console.WriteLine($"  {v,-16} {n}");
Console.WriteLine("\n=== files written by the automatic saves");
foreach (var f in Directory.GetFiles(work, "*", SearchOption.AllDirectories)) Console.WriteLine("  " + Path.GetRelativePath(work, f));
