using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace TimeReport.Services;

public sealed record XlsxCell(
    string? Text = null,
    double? Number = null,
    DateOnly? Date = null,
    int? DurationMinutes = null,
    bool Header = false)
{
    public static XlsxCell TextCell(string text) => new(Text: text);

    public static XlsxCell HeaderCell(string text) => new(Text: text, Header: true);

    public static XlsxCell NumberCell(double number) => new(Number: number);

    public static XlsxCell DateCell(DateOnly date) => new(Date: date);

    public static XlsxCell DurationCell(int minutes) => new(DurationMinutes: minutes);
}

public sealed record XlsxSheet(
    string Name,
    IReadOnlyList<double> ColumnWidths,
    IReadOnlyList<IReadOnlyList<XlsxCell>> Rows);

public static class XlsxWriter
{
    private static readonly DateOnly ExcelEpoch = new(1899, 12, 30);

    public static void Write(string path, IReadOnlyList<XlsxSheet> sheets)
    {
        if (sheets.Count == 0)
        {
            throw new ArgumentException("At least one worksheet is required.", nameof(sheets));
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("The export path has no directory.", nameof(path));
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(
            directory,
            $".timereport-export-{Guid.NewGuid():N}.xlsx");

        try
        {
            using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "[Content_Types].xml", BuildContentTypes(sheets));
                WriteEntry(archive, "_rels/.rels", BuildRootRelationships());
                WriteEntry(archive, "xl/workbook.xml", BuildWorkbook(sheets));
                WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships(sheets));
                WriteEntry(archive, "xl/styles.xml", BuildStyles());
                for (var index = 0; index < sheets.Count; index++)
                {
                    WriteEntry(
                        archive,
                        $"xl/worksheets/sheet{index + 1}.xml",
                        BuildWorksheet(sheets[index]));
                }
            }

            File.Move(temporary, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static string BuildContentTypes(IReadOnlyList<XlsxSheet> sheets)
    {
        var builder = new StringBuilder();
        builder.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.AppendLine("""<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">""");
        builder.AppendLine("""  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>""");
        builder.AppendLine("""  <Default Extension="xml" ContentType="application/xml"/>""");
        builder.AppendLine("""  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>""");
        builder.AppendLine("""  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>""");
        for (var index = 0; index < sheets.Count; index++)
        {
            builder.AppendLine(
                $"""  <Override PartName="/xl/worksheets/sheet{index + 1}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""");
        }
        builder.AppendLine("</Types>");
        return builder.ToString();
    }

    private static string BuildRootRelationships() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private static string BuildWorkbook(IReadOnlyList<XlsxSheet> sheets)
    {
        var builder = new StringBuilder();
        builder.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.AppendLine("""<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">""");
        builder.AppendLine("  <sheets>");
        for (var index = 0; index < sheets.Count; index++)
        {
            builder.AppendLine(
                $"""    <sheet name="{Escape(UniqueSheetName(sheets, index))}" sheetId="{index + 1}" r:id="rId{index + 2}"/>""");
        }
        builder.AppendLine("  </sheets>");
        builder.AppendLine("</workbook>");
        return builder.ToString();
    }

    private static string BuildWorkbookRelationships(IReadOnlyList<XlsxSheet> sheets)
    {
        var builder = new StringBuilder();
        builder.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.AppendLine("""<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""");
        builder.AppendLine("""  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>""");
        for (var index = 0; index < sheets.Count; index++)
        {
            builder.AppendLine(
                $"""  <Relationship Id="rId{index + 2}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{index + 1}.xml"/>""");
        }
        builder.AppendLine("</Relationships>");
        return builder.ToString();
    }

    private static string BuildStyles() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <numFmts count="1">
            <numFmt numFmtId="164" formatCode="[h]:mm"/>
          </numFmts>
          <fonts count="2">
            <font><sz val="11"/><name val="Calibri"/><family val="2"/></font>
            <font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/><family val="2"/></font>
          </fonts>
          <fills count="3">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FF1F4E78"/><bgColor indexed="64"/></patternFill></fill>
          </fills>
          <borders count="2">
            <border><left/><right/><top/><bottom/><diagonal/></border>
            <border><left style="thin"><color rgb="FFD9DEE8"/></left><right style="thin"><color rgb="FFD9DEE8"/></right><top style="thin"><color rgb="FFD9DEE8"/></top><bottom style="thin"><color rgb="FFD9DEE8"/></bottom><diagonal/></border>
          </borders>
          <cellStyleXfs count="1">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0"/>
          </cellStyleXfs>
          <cellXfs count="4">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
            <xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment vertical="center"/></xf>
            <xf numFmtId="14" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
            <xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
          </cellXfs>
          <cellStyles count="1">
            <cellStyle name="Normal" xfId="0" builtinId="0"/>
          </cellStyles>
        </styleSheet>
        """;

    private static string BuildWorksheet(XlsxSheet sheet)
    {
        var builder = new StringBuilder();
        builder.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.AppendLine("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");
        builder.AppendLine("""  <sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>""");
        if (sheet.ColumnWidths.Count > 0)
        {
            builder.AppendLine("  <cols>");
            for (var index = 0; index < sheet.ColumnWidths.Count; index++)
            {
                builder.AppendLine(
                    $"""    <col min="{index + 1}" max="{index + 1}" width="{sheet.ColumnWidths[index].ToString(CultureInfo.InvariantCulture)}" customWidth="1"/>""");
            }
            builder.AppendLine("  </cols>");
        }

        builder.AppendLine("  <sheetData>");
        for (var rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
        {
            var row = sheet.Rows[rowIndex];
            builder.AppendLine($"    <row r=\"{rowIndex + 1}\">");
            for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                AppendCell(builder, row[columnIndex], ColumnName(columnIndex + 1), rowIndex + 1);
            }
            builder.AppendLine("    </row>");
        }
        builder.AppendLine("  </sheetData>");
        builder.AppendLine("</worksheet>");
        return builder.ToString();
    }

    private static void AppendCell(StringBuilder builder, XlsxCell cell, string column, int row)
    {
        var reference = $"{column}{row}";
        if (cell.Header)
        {
            builder.AppendLine(
                $"""      <c r="{reference}" t="inlineStr" s="1"><is><t xml:space="preserve">{Escape(cell.Text ?? string.Empty)}</t></is></c>""");
            return;
        }
        if (cell.Text is not null)
        {
            builder.AppendLine(
                $"""      <c r="{reference}" t="inlineStr"><is><t xml:space="preserve">{Escape(cell.Text)}</t></is></c>""");
            return;
        }
        if (cell.Date is DateOnly date)
        {
            builder.AppendLine(
                $"""      <c r="{reference}" s="2"><v>{date.DayNumber - ExcelEpoch.DayNumber}</v></c>""");
            return;
        }
        if (cell.DurationMinutes is int minutes)
        {
            var value = (minutes / (double)TimeUtil.MinutesPerDay)
                .ToString("0.##########", CultureInfo.InvariantCulture);
            builder.AppendLine($"""      <c r="{reference}" s="3"><v>{value}</v></c>""");
            return;
        }
        if (cell.Number is double number)
        {
            builder.AppendLine(
                $"""      <c r="{reference}"><v>{number.ToString("0.##########", CultureInfo.InvariantCulture)}</v></c>""");
            return;
        }
        builder.AppendLine($"""      <c r="{reference}"/>""");
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string UniqueSheetName(IReadOnlyList<XlsxSheet> sheets, int index)
    {
        var baseName = SanitizeSheetName(sheets[index].Name);
        var candidate = baseName;
        var suffix = 2;
        for (var previous = 0; previous < index; previous++)
        {
            if (string.Equals(
                    SanitizeSheetName(sheets[previous].Name),
                    candidate,
                    StringComparison.OrdinalIgnoreCase))
            {
                candidate = SanitizeSheetName($"{baseName} {suffix++}");
                previous = -1;
            }
        }
        return candidate;
    }

    private static string SanitizeSheetName(string name)
    {
        var invalid = new[] { '\\', '/', '?', '*', '[', ']', ':' };
        var cleaned = new string(name.Where(character => !invalid.Contains(character)).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = "Sheet";
        }
        return cleaned.Length <= 31 ? cleaned : cleaned[..31];
    }

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        while (index > 0)
        {
            index--;
            name = (char)('A' + index % 26) + name;
            index /= 26;
        }
        return name;
    }

    private static string Escape(string value) =>
        value.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;")
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ");
}
