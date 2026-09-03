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

        var documentXml = BuildAndReadDocument("t", pairs);

        // Well-formed parse fails outright on unescaped &/</> in text content.
        Assert.Contains("A &amp; B &lt; C &gt; D", documentXml);
    }

    [Fact]
    public void BuildDocx_RendersFencedCodeBlockAsShadedMonospaceParagraph()
    {
        var reply = "Here:\n\n```csharp\nvar x = 1;\nConsole.WriteLine(x);\n```\n";
        var pairs = new List<(string Question, List<AnswerSegment> Segments)>
        {
            ("Q", new List<AnswerSegment> { new(IsThought: false, Text: reply) }),
        };

        var documentXml = BuildAndReadDocument("t", pairs);

        Assert.Contains("csharp", documentXml);
        Assert.Contains("var x = 1;", documentXml);
        Assert.Contains("Console.WriteLine(x);", documentXml);
        Assert.Contains("w:ascii=\"Consolas\"", documentXml);
        Assert.Contains("<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"F2F2F2\"/>", documentXml);
        // The two code lines must be joined by a run break inside one paragraph, not
        // split across separate <w:p> boxes.
        Assert.Contains("var x = 1;</w:t></w:r><w:r><w:br/></w:r>", documentXml);
    }

    [Fact]
    public void BuildDocx_RendersBoldAndItalicEmphasisAsRealFormatting()
    {
        var reply = "This is **bold** and this is *italic* and this is `code`.";
        var pairs = new List<(string Question, List<AnswerSegment> Segments)>
        {
            ("Q", new List<AnswerSegment> { new(IsThought: false, Text: reply) }),
        };

        var documentXml = BuildAndReadDocument("t", pairs);

        Assert.DoesNotContain("**bold**", documentXml);
        Assert.DoesNotContain("*italic*", documentXml);
        Assert.Contains("<w:r><w:rPr><w:b/></w:rPr><w:t xml:space=\"preserve\">bold</w:t></w:r>", documentXml);
        Assert.Contains("<w:r><w:rPr><w:i/></w:rPr><w:t xml:space=\"preserve\">italic</w:t></w:r>", documentXml);
        Assert.Contains("code", documentXml);
        Assert.Contains("<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"F2F2F2\"/>", documentXml);
    }

    [Fact]
    public void BuildDocx_RendersBulletListItemsWithMarkers()
    {
        var reply = "- first item\n- second item\n";
        var pairs = new List<(string Question, List<AnswerSegment> Segments)>
        {
            ("Q", new List<AnswerSegment> { new(IsThought: false, Text: reply) }),
        };

        var documentXml = BuildAndReadDocument("t", pairs);

        Assert.Contains("first item", documentXml);
        Assert.Contains("second item", documentXml);
        Assert.Contains("w:hanging=\"360\"", documentXml);
    }

    [Fact]
    public void BuildDocx_RendersLinksAsRealHyperlinksWithRelationship()
    {
        var reply = "See [the docs](https://example.com/docs) for more.";
        var pairs = new List<(string Question, List<AnswerSegment> Segments)>
        {
            ("Q", new List<AnswerSegment> { new(IsThought: false, Text: reply) }),
        };

        var bytes = TranscriptWriter.BuildDocx("t", "", DateTime.Now, pairs);
        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var documentXml = ReadEntry(archive, "word/document.xml");
        Assert.Contains("<w:hyperlink r:id=\"rId1\"", documentXml);
        Assert.Contains("the docs", documentXml);

        var relsXml = ReadEntry(archive, "word/_rels/document.xml.rels");
        XDocument.Parse(relsXml);
        Assert.Contains("Id=\"rId1\"", relsXml);
        Assert.Contains("https://example.com/docs", relsXml);
        Assert.Contains("TargetMode=\"External\"", relsXml);
    }

    [Fact]
    public void BuildDocx_RendersPipeTableAsWordTable()
    {
        var reply = "| A | B |\n| --- | --- |\n| 1 | 2 |\n";
        var pairs = new List<(string Question, List<AnswerSegment> Segments)>
        {
            ("Q", new List<AnswerSegment> { new(IsThought: false, Text: reply) }),
        };

        var documentXml = BuildAndReadDocument("t", pairs);

        Assert.Contains("<w:tbl>", documentXml);
        Assert.Contains("</w:tbl>", documentXml);
        Assert.Contains("<w:tr>", documentXml);
        Assert.Contains("<w:tc>", documentXml);
        // Cell padding, so text doesn't sit flush against the borders.
        Assert.Contains("<w:tblCellMar>", documentXml);
        Assert.Contains("<w:left w:w=\"150\" w:type=\"dxa\"/>", documentXml);
        // Header row repeats on page breaks and is shaded to stand out from data rows.
        Assert.Contains("<w:tblHeader/>", documentXml);
        Assert.Contains("<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"F2F2F2\"/>", documentXml);
        Assert.Contains("<w:vAlign w:val=\"center\"/>", documentXml);
    }

    private static string BuildAndReadDocument(string title, List<(string Question, List<AnswerSegment> Segments)> pairs)
    {
        var bytes = TranscriptWriter.BuildDocx(title, "", DateTime.Now, pairs);
        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var documentXml = ReadEntry(archive, "word/document.xml");
        // Throws if malformed -- Word will refuse to open a package with invalid XML.
        XDocument.Parse(documentXml);
        return documentXml;
    }

    private static string ReadEntry(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw new InvalidOperationException($"Missing entry: {name}");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }
}
