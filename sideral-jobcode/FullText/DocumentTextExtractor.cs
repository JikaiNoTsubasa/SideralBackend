using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using dependancies.SharedClasses;
using sideral_jobcode.Storage;
using UglyToad.PdfPig;

namespace sideral_jobcode.FullText;

/// <summary>
/// Extraction du texte d'un fichier pour l'indexation full text : texte brut, PDF (PdfPig), Office Open XML
/// (docx, xlsx, pptx) et OpenDocument (odt, ods, odp). Les autres formats (images, vidéos, anciens .doc...)
/// renvoient une chaîne vide : seul le nom du document est alors indexé.
/// </summary>
public partial class DocumentTextExtractor : ISingleton
{
    /// <summary>Taille maximale du texte envoyé au serveur full text.</summary>
    public const int MAX_TEXT_LENGTH = 2_000_000;

    /// <summary>Taille maximale d'un fichier texte lu.</summary>
    private const long MAX_PLAIN_TEXT_SIZE = 10 * 1024 * 1024;

    public string Extract(string path, string contentType, string fileName)
    {
        var text = contentType switch
        {
            DocumentStorage.TEXT_PLAIN => _PlainText(path),
            DocumentStorage.PDF => _Pdf(path),
            "application/zip" => _Zip(path, Path.GetExtension(fileName).ToLowerInvariant()),
            _ => string.Empty
        };

        text = _Whitespace().Replace(text, " ").Trim();
        return text.Length > MAX_TEXT_LENGTH ? text[..MAX_TEXT_LENGTH] : text;
    }

    private static string _PlainText(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[Math.Min(stream.Length, MAX_PLAIN_TEXT_SIZE)];
        var read = reader.ReadBlock(buffer, 0, buffer.Length);
        return new string(buffer, 0, read);
    }

    private static string _Pdf(string path)
    {
        var builder = new StringBuilder();
        using var pdf = PdfDocument.Open(path);
        foreach (var page in pdf.GetPages())
        {
            builder.AppendLine(page.Text);
            if (builder.Length > MAX_TEXT_LENGTH)
                break;
        }
        return builder.ToString();
    }

    /// <summary>Formats bureautiques : archives zip contenant du XML. On récupère les nœuds texte des parties utiles.</summary>
    private static string _Zip(string path, string extension)
    {
        Func<string, bool>? isTextPart = extension switch
        {
            ".docx" or ".docm" or ".dotx" => name => name.StartsWith("word/") && (name == "word/document.xml" || name.StartsWith("word/header") || name.StartsWith("word/footer") || name == "word/footnotes.xml"),
            // Les cellules texte des feuilles ne contiennent que des index vers les chaînes partagées
            ".xlsx" or ".xlsm" => name => name == "xl/sharedStrings.xml",
            ".pptx" or ".pptm" => name => (name.StartsWith("ppt/slides/slide") || name.StartsWith("ppt/notesSlides/")) && name.EndsWith(".xml"),
            ".odt" or ".ods" or ".odp" => name => name == "content.xml",
            _ => null
        };
        if (isTextPart is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        using var archive = ZipFile.OpenRead(path);
        foreach (var entry in archive.Entries.Where(e => isTextPart(e.FullName)).OrderBy(e => e.FullName, StringComparer.Ordinal))
        {
            using var stream = entry.Open();
            _AppendXmlText(stream, builder);
            if (builder.Length > MAX_TEXT_LENGTH)
                break;
        }
        return builder.ToString();
    }

    private static void _AppendXmlText(Stream stream, StringBuilder builder)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true };
        using var reader = XmlReader.Create(stream, settings);
        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                    builder.Append(reader.Value);
                    break;
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    // Ex. <w:t xml:space="preserve"> </w:t> entre deux mots
                    builder.Append(' ');
                    break;
                case XmlNodeType.EndElement:
                    // Fin de paragraphe / cellule / ligne : séparateur pour ne pas coller les mots
                    if (reader.LocalName is "p" or "tc" or "c" or "si" or "row" or "h" or "table-cell")
                        builder.Append('\n');
                    break;
                case XmlNodeType.Element when reader.LocalName is "tab" or "br" or "s":
                    builder.Append(' ');
                    break;
            }
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex _Whitespace();
}
