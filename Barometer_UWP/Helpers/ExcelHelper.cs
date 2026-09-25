using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Barometer_UWP.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Windows.Storage;

namespace Barometer_UWP.Helpers
{
    public static class ExcelHelper
    {
        private const string DateFormat = "yyyy-MM-dd HH:mm:ss";

        public static byte[] GenerateExcelContent(IEnumerable<PressureRecord> records)
        {
            var ms = new MemoryStream();
            using (var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
            {
                var wbPart = doc.AddWorkbookPart();
                wbPart.Workbook = new Workbook();

                var stylesPart = wbPart.AddNewPart<WorkbookStylesPart>();
                stylesPart.Stylesheet = BuildStylesheet();
                stylesPart.Stylesheet.Save();

                var wsPart = wbPart.AddNewPart<WorksheetPart>();
                var sheetData = new SheetData();
                wsPart.Worksheet = new Worksheet(sheetData);

                var sheets = wbPart.Workbook.AppendChild(new Sheets());
                sheets.Append(new Sheet
                {
                    Id = wbPart.GetIdOfPart(wsPart),
                    SheetId = 1,
                    Name = "Data"
                });

                // Header row
                var header = new Row();
                header.Append(MakeInlineCell("Timestamp", 0),
                              MakeInlineCell("Pressure_hPa", 0),
                              MakeInlineCell("Pressure_mmHg", 0));
                sheetData.Append(header);

                var ci = CultureInfo.InvariantCulture;
                foreach (var r in records ?? Enumerable.Empty<PressureRecord>())
                {
                    var row = new Row();
                    row.Append(MakeDateCell(r.Timestamp, 1),
                               MakeNumberCell(r.PressureHpa, 2, ci),
                               MakeNumberCell(r.PressureMmHg, 2, ci));
                    sheetData.Append(row);
                }

                // Column widths
                wsPart.Worksheet.InsertBefore(
                    new Columns(
                        new Column { Min = 1, Max = 1, Width = 22 },
                        new Column { Min = 2, Max = 2, Width = 14 },
                        new Column { Min = 3, Max = 3, Width = 16 }),
                    sheetData);

                wbPart.Workbook.Save();
            }
            return ms.ToArray();
        }

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
                using (var doc = SpreadsheetDocument.Open(ms, false))
                {
                    var wb = doc.WorkbookPart;
                    var ws = wb.WorksheetParts.First();
                    var shared = wb.SharedStringPart?.SharedStringTable
                        ?.Elements<SharedStringItem>()
                        .Select(s => s.InnerText).ToList() ?? new List<string>();

                    var rows = ws.Worksheet.Descendants<Row>().ToList();
                    for (int i = 1; i < rows.Count; i++) // skip header
                    {
                        var cells = rows[i].Elements<Cell>().ToList();
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
            return result;
        }

        private static string CellText(Cell c, List<string> shared)
        {
            if (c == null) return null;
            if (c.DataType != null && c.DataType.Value == CellValues.SharedString && c.CellValue != null)
            {
                int idx;
                if (int.TryParse(c.CellValue.Text, out idx) && idx >= 0 && idx < shared.Count)
                    return shared[idx];
            }
            return c.CellValue?.Text;
        }

        private static Cell MakeInlineCell(string text, int styleIndex) =>
            new Cell
            {
                StyleIndex = (UInt32Value)(uint)styleIndex,
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(text))
            };

        private static Cell MakeDateCell(DateTime value, int styleIndex) =>
            new Cell
            {
                StyleIndex = (UInt32Value)(uint)styleIndex,
                DataType = CellValues.String,
                CellValue = new CellValue(value.ToString(DateFormat, CultureInfo.InvariantCulture))
            };

        private static Cell MakeNumberCell(double value, int styleIndex, CultureInfo ci) =>
            new Cell
            {
                StyleIndex = (UInt32Value)(uint)styleIndex,
                DataType = CellValues.Number,
                CellValue = new CellValue(value.ToString(ci))
            };

        private static Stylesheet BuildStylesheet()
        {
            return new Stylesheet(
                new Fonts(
                    new Font(new FontSize { Val = 11 }, new Bold()),          // 0: header bold
                    new Font(new FontSize { Val = 11 })                        // 1: normal
                ),
                new Fills(
                    new Fill(new PatternFill { PatternType = PatternValues.None }),
                    new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
                    new Fill(new PatternFill(new ForegroundColor { Rgb = "FFDDEBF7" }) { PatternType = PatternValues.Solid })
                ),
                new Borders(
                    new Border(),
                    new Border(
                        new LeftBorder { Style = BorderStyleValues.Thin },
                        new RightBorder { Style = BorderStyleValues.Thin },
                        new TopBorder { Style = BorderStyleValues.Thin },
                        new BottomBorder { Style = BorderStyleValues.Thin })
                ),
                new CellFormats(
                    // 0: header
                    new CellFormat { FontId = 0, FillId = 2, BorderId = 1, ApplyFont = true, ApplyFill = true, ApplyBorder = true },
                    // 1: date-as-text
                    new CellFormat { FontId = 1, ApplyFont = true },
                    // 2: number with 2 decimals
                    new CellFormat { FontId = 1, NumberFormatId = 4, ApplyFont = true }
                ));
        }
    }
}
