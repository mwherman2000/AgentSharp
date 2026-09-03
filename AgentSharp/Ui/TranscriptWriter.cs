using System.IO.Compression;
using System.Text;

namespace AgentSharp.Ui;

/// <summary>
/// A single piece of an assistant's turn in a /transcribe transcript: either a plain
/// reply segment or a recorded "think" tool call, tagged so both renderers below can
/// format thoughts (blockquoted/indented) differently from actual replies.
/// </summary>
internal readonly record struct AnswerSegment(bool IsThought, string Text);

/// <summary>
/// Renders a ReplHost /transcribe Q&amp;A transcript to either Markdown or a minimal
/// Word (.docx) document -- ReplHost.WriteTranscript picks which based on the
/// requested file's extension.
/// </summary>
internal static class TranscriptWriter
{
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
        body.Append(Paragraph(title, bold: true, sizeHalfPoints: 40));
        if (systemPromptIntro.Length > 0)
            body.Append(Paragraph(systemPromptIntro, italic: true, indent: true));
        body.Append(Paragraph($"Transcript generated {generatedAt:yyyy-MM-dd HH:mm}", italic: true));

        for (int i = 0; i < qaPairs.Count; i++)
        {
            var (question, segments) = qaPairs[i];
            body.Append(Paragraph($"Q{i + 1}", bold: true, sizeHalfPoints: 28));
            foreach (var line in question.Split('\n'))
                body.Append(Paragraph(line));
            body.Append(Paragraph($"A{i + 1}", bold: true, sizeHalfPoints: 28));

            if (segments.Count == 0)
            {
                body.Append(Paragraph("(no response)", italic: true));
                continue;
            }

            foreach (var segment in segments)
            {
                if (segment.IsThought)
                {
                    body.Append(Paragraph("Thinking...", bold: true, italic: true, indent: true));
                    foreach (var line in segment.Text.Split('\n'))
                        body.Append(Paragraph(line, italic: true, indent: true));
                }
                else
                {
                    foreach (var line in segment.Text.Split('\n'))
                        body.Append(Paragraph(line));
                }
            }
        }

        var documentXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
            "<w:body>" + body + "<w:sectPr/></w:body></w:document>";

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", ContentTypesXml);
            WriteEntry(archive, "_rels/.rels", RelsXml);
            WriteEntry(archive, "word/document.xml", documentXml);
        }
        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    // Word paragraphs use direct run formatting (bold/italic/size) rather than named
    // styles, so a minimal docx with no styles.xml part is still valid and opens cleanly.
    private static string Paragraph(string text, bool bold = false, bool italic = false, int? sizeHalfPoints = null, bool indent = false)
    {
        if (text.Length == 0)
            return "<w:p/>";

        var rPr = new StringBuilder();
        if (bold) rPr.Append("<w:b/>");
        if (italic) rPr.Append("<w:i/>");
        if (sizeHalfPoints is { } sz) rPr.Append($"<w:sz w:val=\"{sz}\"/><w:szCs w:val=\"{sz}\"/>");

        var pPr = indent ? "<w:pPr><w:ind w:left=\"720\"/></w:pPr>" : "";
        var runProps = rPr.Length > 0 ? $"<w:rPr>{rPr}</w:rPr>" : "";
        return $"<w:p>{pPr}<w:r>{runProps}<w:t xml:space=\"preserve\">{Escape(text)}</w:t></w:r></w:p>";
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private const string ContentTypesXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
        "</Types>";

    private const string RelsXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
        "</Relationships>";
}
