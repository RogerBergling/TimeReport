using System.IO.Compression;
using System.Xml.Linq;
using TimeReport.Services;

namespace TimeReport.Tests;

public sealed class XlsxWriterTests
{
    [Fact]
    public void DuplicateLongSheetNamesAreUniqueAndDoNotHang()
    {
        using var context = new TempDirectory();
        var path = Path.Combine(context.Path, "duplicate-sheets.xlsx");
        var longName = new string('A', 31);

        XlsxWriter.Write(
            path,
            [
                Sheet(longName),
                Sheet(longName),
            ]);

        using var archive = ZipFile.OpenRead(path);
        var names = SheetNames(archive);
        Assert.Equal(2, names.Count);
        Assert.Equal(2, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void InvalidXmlCharactersAreSanitized()
    {
        using var context = new TempDirectory();
        var path = Path.Combine(context.Path, "control-characters.xlsx");

        XlsxWriter.Write(
            path,
            [
                new XlsxSheet(
                    "Control\u0001sheet",
                    [20],
                    new[]
                    {
                        new[] { XlsxCell.TextCell("Hello\u0001world") },
                    }),
            ]);

        using var archive = ZipFile.OpenRead(path);
        var entry = archive.GetEntry("xl/worksheets/sheet1.xml")!;
        using var stream = entry.Open();
        var xml = XDocument.Load(stream);
        var text = xml.ToString();
        Assert.Contains("Hello world", text);
        Assert.Equal(-1, text.IndexOf('\u0001'));

        var workbookEntry = archive.GetEntry("xl/workbook.xml")!;
        using var workbookStream = workbookEntry.Open();
        var workbook = XDocument.Load(workbookStream);
        Assert.Equal(-1, workbook.ToString().IndexOf('\u0001'));
    }

    private static XlsxSheet Sheet(string name) =>
        new(
            name,
            [20],
            new[]
            {
                new[] { XlsxCell.TextCell("value") },
            });

    private static List<string> SheetNames(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/workbook.xml")!;
        using var stream = entry.Open();
        return XDocument.Load(stream)
            .Descendants()
            .Where(element => element.Name.LocalName == "sheet")
            .Select(element => (string?)element.Attribute("name") ?? string.Empty)
            .ToList();
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "timereport-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
