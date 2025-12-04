using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Windows.Storage;

namespace Barometer_UWP.Helpers
{
    public static class ExcelHelper
    {
        public static async Task<byte[]> GenerateExcelContentAsync(IEnumerable<Models.PressureRecord> records)
        {
            var memoryStream = new MemoryStream();
            
            using (var spreadsheetDocument = SpreadsheetDocument.Create(memoryStream, SpreadsheetDocumentType.Workbook))
            {
                var workbookPart = spreadsheetDocument.AddWorkbookPart();
                workbookPart.Workbook = new Workbook();

                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var sheetData = new SheetData();
                worksheetPart.Worksheet = new Worksheet(sheetData);

                var sheets = spreadsheetDocument.WorkbookPart.Workbook.AppendChild(new Sheets());
                var sheet = new Sheet()
                {
                    Id = spreadsheetDocument.WorkbookPart.GetIdOfPart(worksheetPart),
                    SheetId = 1,
                    Name = "Data"
                };
                sheets.Append(sheet);

                // Add header row
                var headerRow = new Row();
                headerRow.Append(
                    new Cell() { CellValue = new CellValue("Timestamp"), DataType = CellValues.String },
                    new Cell() { CellValue = new CellValue("Pressure_hPa"), DataType = CellValues.String },
                    new Cell() { CellValue = new CellValue("Pressure_mmHg"), DataType = CellValues.String }
                );
                sheetData.AppendChild(headerRow);

                // Add data rows
                foreach (var record in records)
                {
                    var row = new Row();
                    row.Append(
                        new Cell() { CellValue = new CellValue(record.Timestamp.ToString("o")), DataType = CellValues.String },
                        new Cell() { CellValue = new CellValue(record.PressureHpa.ToString()), DataType = CellValues.Number },
                        new Cell() { CellValue = new CellValue(record.PressureMmHg.ToString()), DataType = CellValues.Number }
                    );
                    sheetData.AppendChild(row);
                }

                workbookPart.Workbook.Save();
            }

            return memoryStream.ToArray();
        }

        public static async Task SaveToExcelFileAsync(IEnumerable<Models.PressureRecord> records, StorageFile file)
        {
            var excelData = await GenerateExcelContentAsync(records);
            await FileIO.WriteBytesAsync(file, excelData);
        }
    }
}