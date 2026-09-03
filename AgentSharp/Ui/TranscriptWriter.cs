using System.IO.Compression;
using System.Text;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace AgentSharp.Ui;

/// <summary>
/// A single piece of an assistant's turn in a /transcribe transcript: either a plain
/// reply segment or a recorded "think" tool call, tagged so both renderers below can
/// format thoughts (blockquoted/indented) differently from actual replies.
/// </summary>
internal readonly record struct AnswerSegment(bool IsThought, string Text);

/// <summary>
/// Renders a ReplHost /transcribe Q&amp;A transcript to either Markdown or a Word
/// (.docx) document -- ReplHost.WriteTranscript picks which based on the requested
/// file's extension. The docx path parses each piece of text with Markdig and walks
/// its AST into Open XML so headings, code blocks, lists, tables, links, and inline
/// emphasis all come out as real Word formatting rather than literal Markdown syntax.
/// </summary>
internal static class TranscriptWriter
{
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseTaskLists()
        .UseAutoLinks()
        .UseEmphasisExtras()
        .Build();

    public static string BuildMarkdown(
        string title,
        string systemPromptIntro,
        DateTime generatedAt,
        IReadOnlyList<(string Question, List<AnswerSegment> Segments)> qaPairs)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        if (systemPromptIntro.Length > 0)
        {
            foreach (var line in systemPromptIntro.Split('\n'))
                sb.AppendLine($"> {line}");
            sb.AppendLine();
        }
        sb.AppendLine($"_Transcript generated {generatedAt:yyyy-MM-dd HH:mm}_");
        sb.AppendLine();

        for (int i = 0; i < qaPairs.Count; i++)
        {
            var (question, segments) = qaPairs[i];
            sb.AppendLine($"## Q{i + 1}");
            sb.AppendLine();
            sb.AppendLine(question);
            sb.AppendLine();
            sb.AppendLine($"## A{i + 1}");
            sb.AppendLine();
            sb.AppendLine(segments.Count > 0
                ? string.Join("\n\n", segments.Select(FormatSegmentMarkdown))
                : "_(no response)_");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string FormatSegmentMarkdown(AnswerSegment segment)
    {
        if (!segment.IsThought) return segment.Text;

        var lines = new[] { "***Thinking...***" }.Concat(segment.Text.Split('\n'));
        return string.Join("\n", lines.Select(line => $"> {line}"));
    }

    public static byte[] BuildDocx(
        string title,
        string systemPromptIntro,
        DateTime generatedAt,
        IReadOnlyList<(string Question, List<AnswerSegment> Segments)> qaPairs)
    {
        var body = new StringBuilder();
        var hyperlinks = new List<(string Id, string Target)>();

        body.Append(HeadingParagraph(title, sizeHalfPoints: 44));
        if (systemPromptIntro.Length > 0)
            body.Append(SimpleParagraph(systemPromptIntro, default(RunState) with { Italic = true }, quoteDepth: 1));
        body.Append(SimpleParagraph($"Transcript generated {generatedAt:yyyy-MM-dd HH:mm}", default(RunState) with { Italic = true }));

        for (int i = 0; i < qaPairs.Count; i++)
        {
            var (question, segments) = qaPairs[i];
            body.Append(HeadingParagraph($"Q{i + 1}", sizeHalfPoints: 28));
            AppendMarkdown(body, question, hyperlinks, quoteDepth: 0);
            body.Append(HeadingParagraph($"A{i + 1}", sizeHalfPoints: 28));

            if (segments.Count == 0)
            {
                body.Append(SimpleParagraph("(no response)", default(RunState) with { Italic = true }));
                continue;
            }

            foreach (var segment in segments)
            {
                if (segment.IsThought)
                {
                    body.Append(SimpleParagraph("Thinking...", default(RunState) with { Bold = true, Italic = true }, quoteDepth: 1));
                    AppendMarkdown(body, segment.Text, hyperlinks, quoteDepth: 1, forceItalic: true);
                }
                else
                {
                    AppendMarkdown(body, segment.Text, hyperlinks, quoteDepth: 0);
                }
            }
        }

        var relationshipsXml = string.Concat(hyperlinks.Select(r =>
            $"<Relationship Id=\"{r.Id}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\"{EscapeAttribute(r.Target)}\" TargetMode=\"External\"/>"));
        var documentRelsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            relationshipsXml + "</Relationships>";

        var documentXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" " +
            "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<w:body>" + body + "<w:sectPr/></w:body></w:document>";

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", ContentTypesXml);
            WriteEntry(archive, "_rels/.rels", PackageRelsXml);
            WriteEntry(archive, "word/document.xml", documentXml);
            WriteEntry(archive, "word/_rels/document.xml.rels", documentRelsXml);
        }
        return stream.ToArray();
    }

    /// <summary>
    /// Parses <paramref name="markdown"/> with Markdig and renders its block tree
    /// straight into the docx body. <paramref name="quoteDepth"/> is the *base*
    /// indent level (1 for a "think" segment, which reads as one blockquote level
    /// deep even before any Markdown blockquote inside it adds more); QuoteBlock
    /// nesting inside the text adds further levels on top of that.
    /// </summary>
    private static void AppendMarkdown(StringBuilder body, string markdown, List<(string Id, string Target)> hyperlinks, int quoteDepth, bool forceItalic = false)
    {
        var document = Markdown.Parse(markdown, MarkdownPipeline);
        var baseState = forceItalic ? default(RunState) with { Italic = true } : default(RunState);
        RenderBlocks(document, body, hyperlinks, quoteDepth, baseState);
    }

    private static void RenderBlocks(ContainerBlock container, StringBuilder body, List<(string Id, string Target)> hyperlinks, int quoteDepth, RunState baseState)
    {
        // Blockquote nesting (and a "think" segment's own base indent) makes every
        // run underneath italic, in addition to whatever emphasis Markdig applies --
        // matching the "> ***Thinking...***" convention used in the Markdown output.
        var textState = baseState with { Italic = baseState.Italic || quoteDepth > 0 };

        foreach (var block in container)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    body.Append(OpenParagraph(quoteDepth));
                    RenderInline(heading.Inline, textState with { Bold = true, Size = HeadingSize(heading.Level) }, body, hyperlinks);
                    body.Append("</w:p>");
                    break;

                case FencedCodeBlock { Info.Length: > 0 } fenced:
                    body.Append(SimpleParagraph(fenced.Info, default(RunState) with { Mono = true, Size = 16 }, quoteDepth));
                    AppendCodeBlock(body, fenced, quoteDepth);
                    break;

                case CodeBlock codeBlock:
                    AppendCodeBlock(body, codeBlock, quoteDepth);
                    break;

                case QuoteBlock quote:
                    RenderBlocks(quote, body, hyperlinks, quoteDepth + 1, baseState);
                    break;

                case ListBlock list:
                    AppendList(body, list, hyperlinks, listDepth: 0, textState);
                    break;

                case Table table:
                    AppendTable(body, table, hyperlinks, textState);
                    break;

                case ThematicBreakBlock:
                    body.Append("<w:p><w:pPr><w:pBdr><w:bottom w:val=\"single\" w:sz=\"6\" w:space=\"1\" w:color=\"999999\"/></w:pBdr></w:pPr></w:p>");
                    break;

                case ParagraphBlock para:
                    body.Append(OpenParagraph(quoteDepth));
                    RenderInline(para.Inline, textState, body, hyperlinks);
                    body.Append("</w:p>");
                    break;

                case ContainerBlock nested:
                    RenderBlocks(nested, body, hyperlinks, quoteDepth, baseState);
                    break;

                case LeafBlock leaf when leaf.Lines.Lines is { } lines:
                    // Catch-all for leaf block types we don't special-case (e.g. raw
                    // HTML) -- render the raw text rather than silently dropping it.
                    var raw = string.Join("\n", lines.Where(l => l.Slice.Text != null).Select(l => l.Slice.ToString()));
                    if (raw.Length > 0)
                        body.Append(SimpleParagraph(raw, textState, quoteDepth));
                    break;
            }
        }
    }

    private static void AppendList(StringBuilder body, ListBlock list, List<(string Id, string Target)> hyperlinks, int listDepth, RunState state)
    {
        var index = list.IsOrdered && int.TryParse(list.OrderedStart, out var start) ? start : 1;
        var indentLeft = 360 + 360 * listDepth;

        foreach (var itemBlock in list)
        {
            if (itemBlock is not ListItemBlock item) continue;
            var marker = list.IsOrdered ? $"{index}. " : "• ";
            index++;

            var isFirstParagraph = true;
            foreach (var child in item)
            {
                switch (child)
                {
                    case ParagraphBlock p:
                        body.Append($"<w:p><w:pPr><w:ind w:left=\"{indentLeft}\" w:hanging=\"360\"/></w:pPr>");
                        var inline = p.Inline?.FirstChild;
                        if (isFirstParagraph)
                        {
                            if (inline is TaskList task)
                            {
                                AppendRun(body, task.Checked ? "☑ " : "☐ ", state);
                                inline = task.NextSibling;
                            }
                            else
                            {
                                AppendRun(body, marker, state);
                            }
                        }
                        RenderInline(inline, state, body, hyperlinks);
                        body.Append("</w:p>");
                        isFirstParagraph = false;
                        break;

                    case ListBlock nestedList:
                        AppendList(body, nestedList, hyperlinks, listDepth + 1, state);
                        break;

                    case ContainerBlock other:
                        RenderBlocks(other, body, hyperlinks, quoteDepth: 0, state);
                        break;
                }
            }
        }
    }

    private static void AppendTable(StringBuilder body, Table table, List<(string Id, string Target)> hyperlinks, RunState state)
    {
        body.Append(
            "<w:tbl><w:tblPr><w:tblW w:w=\"0\" w:type=\"auto\"/><w:tblBorders>" +
            "<w:top w:val=\"single\" w:sz=\"4\" w:color=\"999999\"/>" +
            "<w:left w:val=\"single\" w:sz=\"4\" w:color=\"999999\"/>" +
            "<w:bottom w:val=\"single\" w:sz=\"4\" w:color=\"999999\"/>" +
            "<w:right w:val=\"single\" w:sz=\"4\" w:color=\"999999\"/>" +
            "<w:insideH w:val=\"single\" w:sz=\"4\" w:color=\"999999\"/>" +
            "<w:insideV w:val=\"single\" w:sz=\"4\" w:color=\"999999\"/>" +
            "</w:tblBorders></w:tblPr>");

        foreach (var rowBlock in table)
        {
            if (rowBlock is not TableRow row) continue;
            body.Append("<w:tr>");
            foreach (var cellBlock in row)
            {
                if (cellBlock is not TableCell cell) continue;
                body.Append("<w:tc><w:tcPr><w:tcW w:w=\"0\" w:type=\"auto\"/></w:tcPr>");
                var wroteParagraph = false;
                foreach (var content in cell)
                {
                    if (content is not ParagraphBlock p) continue;
                    body.Append("<w:p>");
                    RenderInline(p.Inline, row.IsHeader ? state with { Bold = true } : state, body, hyperlinks);
                    body.Append("</w:p>");
                    wroteParagraph = true;
                }
                if (!wroteParagraph) body.Append("<w:p/>");
                body.Append("</w:tc>");
            }
            body.Append("</w:tr>");
        }
        // A table can't be the last thing before </w:body><w:sectPr/> in a valid
        // package, and a trailing paragraph is good spacing either way.
        body.Append("</w:tbl><w:p/>");
    }

    private static void AppendCodeBlock(StringBuilder body, CodeBlock block, int quoteDepth)
    {
        var lines = block.Lines.Lines?.Where(l => l.Slice.Text != null).Select(l => l.Slice.ToString()).ToArray()
            ?? Array.Empty<string>();
        var indentLeft = 200 + 720 * quoteDepth;

        body.Append(
            $"<w:p><w:pPr><w:ind w:left=\"{indentLeft}\" w:right=\"200\"/>" +
            "<w:spacing w:before=\"120\" w:after=\"120\"/>" +
            "<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"F2F2F2\"/>" +
            "<w:pBdr>" +
            "<w:top w:val=\"single\" w:sz=\"4\" w:space=\"4\" w:color=\"CCCCCC\"/>" +
            "<w:left w:val=\"single\" w:sz=\"4\" w:space=\"4\" w:color=\"CCCCCC\"/>" +
            "<w:bottom w:val=\"single\" w:sz=\"4\" w:space=\"4\" w:color=\"CCCCCC\"/>" +
            "<w:right w:val=\"single\" w:sz=\"4\" w:space=\"4\" w:color=\"CCCCCC\"/>" +
            "</w:pBdr></w:pPr>");

        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) body.Append("<w:r><w:br/></w:r>");
            if (lines[i].Length > 0)
                AppendRun(body, lines[i], default(RunState) with { Mono = true });
        }
        if (lines.Length == 0)
            body.Append("<w:r><w:t xml:space=\"preserve\"></w:t></w:r>");
        body.Append("</w:p>");
    }

    private static void RenderInline(Inline? inline, RunState state, StringBuilder body, List<(string Id, string Target)> hyperlinks)
    {
        for (var node = inline; node != null; node = node.NextSibling)
        {
            switch (node)
            {
                case LiteralInline literal:
                    AppendRun(body, literal.Content.ToString(), state);
                    break;

                case CodeInline code:
                    AppendRun(body, code.Content, state with { Mono = true, Shade = true });
                    break;

                case LineBreakInline:
                    body.Append("<w:r><w:br/></w:r>");
                    break;

                case EmphasisInline emphasis:
                    var nested = emphasis.DelimiterChar == '~'
                        ? state with { Strike = true }
                        : emphasis.DelimiterCount >= 3
                            ? state with { Bold = true, Italic = true }
                            : emphasis.DelimiterCount == 2
                                ? state with { Bold = true }
                                : state with { Italic = true };
                    RenderInline(emphasis.FirstChild, nested, body, hyperlinks);
                    break;

                case TaskList task:
                    AppendRun(body, task.Checked ? "☑ " : "☐ ", state);
                    break;

                case LinkInline { IsImage: true } image:
                    AppendRun(body, $"[image: {CollectPlainText(image)}]", state with { Italic = true });
                    break;

                case LinkInline { Url.Length: > 0 } link:
                    var linkId = $"rId{hyperlinks.Count + 1}";
                    hyperlinks.Add((linkId, link.Url!));
                    body.Append($"<w:hyperlink r:id=\"{linkId}\" w:history=\"1\">");
                    RenderInline(link.FirstChild, state with { Underline = true }, body, hyperlinks);
                    body.Append("</w:hyperlink>");
                    break;

                case AutolinkInline autolink:
                    var autoId = $"rId{hyperlinks.Count + 1}";
                    hyperlinks.Add((autoId, autolink.Url));
                    body.Append($"<w:hyperlink r:id=\"{autoId}\" w:history=\"1\">");
                    AppendRun(body, autolink.Url, state with { Underline = true });
                    body.Append("</w:hyperlink>");
                    break;

                case ContainerInline container:
                    RenderInline(container.FirstChild, state, body, hyperlinks);
                    break;
            }
        }
    }

    private static string CollectPlainText(Inline? inline)
    {
        var sb = new StringBuilder();
        for (var node = inline; node != null; node = node.NextSibling)
        {
            switch (node)
            {
                case LiteralInline literal: sb.Append(literal.Content.ToString()); break;
                case CodeInline code: sb.Append(code.Content); break;
                case ContainerInline container: sb.Append(CollectPlainText(container.FirstChild)); break;
            }
        }
        return sb.ToString();
    }

    private static int HeadingSize(int level) => level switch
    {
        1 => 32,
        2 => 28,
        3 => 26,
        4 => 24,
        5 => 22,
        _ => 20,
    };

    private static string OpenParagraph(int quoteDepth) =>
        quoteDepth > 0 ? $"<w:p><w:pPr><w:ind w:left=\"{720 * quoteDepth}\"/></w:pPr>" : "<w:p>";

    private static string HeadingParagraph(string text, int sizeHalfPoints) =>
        SimpleParagraph(text, default(RunState) with { Bold = true, Size = sizeHalfPoints });

    private static string SimpleParagraph(string text, RunState state, int quoteDepth = 0)
    {
        var sb = new StringBuilder();
        sb.Append(OpenParagraph(quoteDepth));
        AppendRun(sb, text, state);
        sb.Append("</w:p>");
        return sb.ToString();
    }

    // Direct run formatting (bold/italic/size/shading) rather than named styles, so a
    // minimal docx with no styles.xml part is still valid and opens cleanly.
    private static void AppendRun(StringBuilder body, string text, RunState state)
    {
        if (text.Length == 0) return;

        var rPr = new StringBuilder();
        if (state.Bold) rPr.Append("<w:b/>");
        if (state.Italic) rPr.Append("<w:i/>");
        if (state.Strike) rPr.Append("<w:strike/>");
        if (state.Underline) rPr.Append("<w:u w:val=\"single\"/>");
        if (state.Mono) rPr.Append("<w:rFonts w:ascii=\"Consolas\" w:hAnsi=\"Consolas\" w:cs=\"Consolas\"/>");
        if (state.Shade) rPr.Append("<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"F2F2F2\"/>");
        if (state.Size is { } sz) rPr.Append($"<w:sz w:val=\"{sz}\"/><w:szCs w:val=\"{sz}\"/>");

        var runProps = rPr.Length > 0 ? $"<w:rPr>{rPr}</w:rPr>" : "";
        body.Append($"<w:r>{runProps}<w:t xml:space=\"preserve\">{Escape(text)}</w:t></w:r>");
    }

    private readonly record struct RunState(bool Bold, bool Italic, bool Strike, bool Underline, bool Mono, bool Shade, int? Size);

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string EscapeAttribute(string text) =>
        Escape(text).Replace("\"", "&quot;");

    private const string ContentTypesXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
        "</Types>";

    private const string PackageRelsXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
        "</Relationships>";
}
