using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Barometer_UWP.Models;
using Windows.Storage;

namespace Barometer_UWP.Helpers
{
    /// <summary>
    /// v1.4.0.34: minimal XLSX (Spreadsheet ML / OPC) writer+reader WITHOUT any NuGet
    /// dependency. Replaces DocumentFormat.OpenXml, whose only UAP-compatible asset was
    /// netstandard1.3 and thereby dragged the .NET Standard 1.3 facade package graph into
    /// the compile universe -> WMC1006/WMC9999 'System.Runtime' unresolved in the UAP
    /// XamlCompiler. Everything used here (System.IO.Compression.ZipArchive, System.Xml)
    /// ships inside the Windows SDK / netcore50 itself.
    /// Output is a valid xlsx: [Content_Types].xml, _rels/.rels, workbook.xml,
    /// xl/_rels/workbook.xml.rels, xl/styles.xml, xl/worksheets/sheet1.xml.
    /// Dates are written as ISO strings, numbers as numeric cells (2-decimal style).
    /// </summary>
    public static class ExcelHelper
    {
        private const string DateFormat = "yyyy-MM-dd HH:mm:ss";

        #region Writing

        public static byte[] GenerateExcelContent(IEnumerable<PressureRecord> records)
        {
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                AddEntry(zip, "[Content_Types].xml", ContentTypesXml);
                AddEntry(zip, "_rels/.rels", RootRelsXml);
                AddEntry(zip, "xl/workbook.xml", WorkbookXml);
                AddEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRelsXml);
                AddEntry(zip, "xl/styles.xml", StylesXml);
                AddEntry(zip, "xl/worksheets/sheet1.xml", BuildSheetXml(records));
            }
            return ms.ToArray();
        }

        private static void AddEntry(ZipArchive zip, string path, string content)
        {
            var e = zip.CreateEntry(path, CompressionLevel.Optimal);
            using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false)))
                w.Write(content);
        }

        private static string BuildSheetXml(IEnumerable<PressureRecord> records)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sb.Append("<cols>");
            sb.Append("<col min=\"1\" max=\"1\" width=\"22\" customWidth=\"1\"/>");
            sb.Append("<col min=\"2\" max=\"2\" width=\"14\" customWidth=\"1\"/>");
            sb.Append("<col min=\"3\" max=\"3\" width=\"16\" customWidth=\"1\"/>");
            sb.Append("</cols>");
            sb.Append("<sheetData>");

            // header row (style 0 = bold)
            sb.Append("<row r=\"1\">");
            sb.Append(InlineCell("A1", "Timestamp", 0));
            sb.Append(InlineCell("B1", "Pressure_hPa", 0));
            sb.Append(InlineCell("C1", "Pressure_mmHg", 0));
            sb.Append("</row>");

            int r = 2;
            foreach (var rec in records ?? Enumerable.Empty<PressureRecord>())
            {
                sb.Append("<row r=\"").Append(r).Append("\">");
                sb.Append(InlineCell("A" + r, rec.Timestamp.ToString(DateFormat, ci), 1));
                sb.Append(NumberCell("B" + r, rec.PressureHpa, ci));
                sb.Append(NumberCell("C" + r, rec.PressureMmHg, ci));
                sb.Append("</row>");
                r++;
            }
            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static string InlineCell(string reference, string text, int style) =>
            "<c r=\"" + reference + "\" s=\"" + style + "\" t=\"inlineStr\"><is><t>" +
            XmlEscape(text) + "</t></is></c>";

        private static string NumberCell(string reference, double value, CultureInfo ci)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) value = 0;
            return "<c r=\"" + reference + "\" s=\"2\"><v>" + value.ToString("R", ci) + "</v></c>";
        }

        private static string XmlEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    case '\'': sb.Append("&apos;"); break;
                    default:
                        if (c < 0x20 && c != '\t' && c != '\n' && c != '\r') { /* drop invalid XML chars */ }
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private const string ContentTypesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "</Types>";

        private const string RootRelsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        private const string WorkbookXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
            "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<sheets><sheet name=\"Data\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";

        private const string WorkbookRelsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";

        // cell styles: 0 = header (bold), 1 = plain text, 2 = number with 2 decimals
        private const string StylesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"0.00\"/></numFmts>" +
            "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
            "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
            "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
            "<borders count=\"1\"><border/></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"3\">" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            "</cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>";

        #endregion

        #region Reading

        public static async Task SaveToExcelFileAsync(IEnumerable<PressureRecord> records, StorageFile file)
        {
            var bytes = GenerateExcelContent(records);
            await FileIO.WriteBytesAsync(file, bytes);
        }

        public static async Task<List<PressureRecord>> LoadFromExcelFileAsync(StorageFile file)
        {
            var result = new List<PressureRecord>();
            using (var stream = await file.OpenStreamForReadAsync())
            using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                ms.Position = 0;
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Read))
                {
                    // shared strings table (optional part)
                    var shared = new List<string>();
                    var sstEntry = zip.GetEntry("xl/sharedStrings.xml");
                    if (sstEntry != null)
                    {
                        using (var sr = new StreamReader(sstEntry.Open()))
                        {
                            var doc = new XmlDocument();
                            doc.Load(sr);
                            foreach (XmlNode si in doc.GetElementsByTagName("si"))
                                shared.Add(si.InnerText ?? "");
                        }
                    }

                    // first worksheet
                    var sheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml")
                                    ?? zip.Entries.FirstOrDefault(e =>
                                         string.Equals(e.FullName, "xl/worksheets/sheet.xml", StringComparison.OrdinalIgnoreCase) ||
                                         (e.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) &&
                                          e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)));
                    if (sheetEntry == null) return result;

                    using (var sr = new StreamReader(sheetEntry.Open()))
                    {
                        var doc = new XmlDocument();
                        doc.Load(sr);
                        var rows = doc.GetElementsByTagName("row");
                        for (int i = 1; i < rows.Count; i++) // skip header row
                        {
                            var cells = rows[i].ChildNodes.Cast<XmlNode>()
                                .Where(n => n.LocalName == "c").ToList();
                            if (cells.Count < 2) continue;
                            var tsText = CellText(cells[0], shared);
                            var valText = CellText(cells.Count > 2 ? cells[1] : cells[0], shared);
                            if (DateTime.TryParse(tsText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var ts) &&
                                double.TryParse(valText, NumberStyles.Number, CultureInfo.InvariantCulture, out var hpa))
                            {
                                result.Add(new PressureRecord { Timestamp = ts, PressureHpa = hpa, Source = "import" });
                            }
                        }
                    }
                }
            }
            return result;
        }

        private static string CellText(XmlNode c, List<string> shared)
        {
            if (c == null) return null;
            string type = null;
            var attrs = c.Attributes;
            if (attrs != null)
                foreach (XmlAttribute a in attrs)
                    if (a.LocalName == "t") { type = a.Value; break; }
            XmlNode value = null, inline = null;
            foreach (XmlNode n in c.ChildNodes)
            {
                if (value == null && n.LocalName == "v") value = n;
                if (inline == null && n.LocalName == "is") inline = n;
            }
            if (type == "s" && value != null)
            {
                int idx;
                if (int.TryParse(value.InnerText, out idx) && idx >= 0 && idx < shared.Count)
                    return shared[idx];
                return null;
            }
            if (inline != null) return inline.InnerText;
            return value != null ? value.InnerText : null;
        }

        #endregion
    }
}
