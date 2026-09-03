using System.IO.Compression;
using System.Xml.Linq;
using AgentSharp.Ui;

namespace AgentSharp.Tests.Ui;

public class TranscriptWriterTests
{
    private static readonly List<(string Question, List<AnswerSegment> Segments)> SamplePairs = new()
    {
        ("What's 2+2?", new List<AnswerSegment>
        {
            new(IsThought: true, Text: "Let me add these.\nCarry the one? No."),
            new(IsThought: false, Text: "4"),
        }),
    };

    [Fact]
    public void BuildMarkdown_MarksThoughtsWithBoldItalicHeaderInsideBlockquote()
    {
        var md = TranscriptWriter.BuildMarkdown("t", "", DateTime.Now, SamplePairs);

        Assert.Contains("> ***Thinking...***", md);
        Assert.Contains("> Let me add these.", md);
        Assert.Contains("> Carry the one? No.", md);
        Assert.Contains("4", md);
    }

    [Fact]
    public void BuildMarkdown_NoResponseIsMarkedExplicitly()
    {
        var pairs = new List<(string Question, List<AnswerSegment> Segments)>
        {
            ("Q with no answer", new List<AnswerSegment>()),
        };

        var md = TranscriptWriter.BuildMarkdown("t", "", DateTime.Now, pairs);

        Assert.Contains("_(no response)_", md);
    }

    [Fact]
    public void BuildDocx_ProducesWellFormedOpenXmlPackage()
    {
        var bytes = TranscriptWriter.BuildDocx("My Title", "You are a helpful assistant.", DateTime.Now, SamplePairs);

        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var entryNames = archive.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("[Content_Types].xml", entryNames);
        Assert.Contains("_rels/.rels", entryNames);
        Assert.Contains("word/document.xml", entryNames);

        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            var xml = reader.ReadToEnd();
            // Throws if malformed -- this is the real assertion: every part must be
            // valid XML or Word will refuse to open the package.
            XDocument.Parse(xml);
        }

        var documentXml = ReadEntry(archive, "word/document.xml");
        Assert.Contains("My Title", documentXml);
        Assert.Contains("Thinking...", documentXml);
        Assert.Contains("Let me add these.", documentXml);
        Assert.Contains("<w:b/>", documentXml);
        Assert.Contains("<w:i/>", documentXml);
    }

    [Fact]
    public void BuildDocx_EscapesXmlSpecialCharacters()
    {
        var pairs = new List<(string Question, List<AnswerSegment> Segments)>
        {
            ("Q", new List<AnswerSegment> { new(IsThought: false, Text: "A & B < C > D") }),
        };

        var bytes = TranscriptWriter.BuildDocx("t", "", DateTime.Now, pairs);

        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var documentXml = ReadEntry(archive, "word/document.xml");

        // Well-formed parse fails outright on unescaped &/</> in text content.
        XDocument.Parse(documentXml);
        Assert.Contains("A &amp; B &lt; C &gt; D", documentXml);
    }

    private static string ReadEntry(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw new InvalidOperationException($"Missing entry: {name}");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }
}
