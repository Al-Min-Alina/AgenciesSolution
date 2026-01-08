using Agencies.Core.DTO;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Text;

// Используем псевдонимы для iText7
using iText7 = iText.Kernel.Pdf;
using iTextLayout = iText.Layout;
using iTextElement = iText.Layout.Element;
using iTextProperties = iText.Layout.Properties;
using iTextColors = iText.Kernel.Colors;
using iTextIOFont = iText.IO.Font;
using iTextKernelFont = iText.Kernel.Font;

namespace Agencies.Client.Services
{
    public class ReportExporter
    {
        public async Task<bool> ExportToPdfAsync(SalesReportDto report, string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var writer = new iText7.PdfWriter(filePath))
                    using (var pdf = new iText7.PdfDocument(writer))
                    {
                        var document = new iTextLayout.Document(pdf);

                        // Создаем шрифты
                        var titleFont = iTextKernelFont.PdfFontFactory.CreateFont(iTextIOFont.Constants.StandardFonts.HELVETICA_BOLD);
                        var normalFont = iTextKernelFont.PdfFontFactory.CreateFont(iTextIOFont.Constants.StandardFonts.HELVETICA);
                        var italicFont = iTextKernelFont.PdfFontFactory.CreateFont(iTextIOFont.Constants.StandardFonts.HELVETICA_OBLIQUE);

                        // Заголовок
                        var title = new iTextElement.Paragraph("Отчет по продажам")
                            .SetFont(titleFont)
                            .SetFontSize(20)
                            .SetTextAlignment(iTextProperties.TextAlignment.CENTER)
                            .SetMarginBottom(20);
                        document.Add(title);

                        // Период отчета
                        var periodText = new iTextElement.Paragraph($"Период: {report.StartDate:dd.MM.yyyy} - {report.EndDate:dd.MM.yyyy}")
                            .SetFont(normalFont)
                            .SetFontSize(12)
                            .SetMarginBottom(15);
                        document.Add(periodText);

                        // Сводная информация
                        var summaryTitle = new iTextElement.Paragraph("Сводная информация:")
                            .SetFont(titleFont)
                            .SetFontSize(14)
                            .SetMarginBottom(10);
                        document.Add(summaryTitle);

                        // Таблица сводной информации
                        var summaryTable = new iTextElement.Table(2)
                            .UseAllAvailableWidth()
                            .SetMarginBottom(20);

                        summaryTable.AddCell(CreateCell("Общая выручка:", true, titleFont));
                        summaryTable.AddCell(CreateCell(report.TotalRevenue.ToString("C"), false, normalFont));

                        summaryTable.AddCell(CreateCell("Всего сделок:", true, titleFont));
                        summaryTable.AddCell(CreateCell(report.TotalDeals.ToString(), false, normalFont));

                        summaryTable.AddCell(CreateCell("Завершено сделок:", true, titleFont));
                        summaryTable.AddCell(CreateCell(report.CompletedDeals.ToString(), false, normalFont));

                        summaryTable.AddCell(CreateCell("В ожидании:", true, titleFont));
                        summaryTable.AddCell(CreateCell(report.PendingDeals.ToString(), false, normalFont));

                        summaryTable.AddCell(CreateCell("Средняя сумма сделки:", true, titleFont));
                        summaryTable.AddCell(CreateCell(report.AverageDealAmount.ToString("C"), false, normalFont));

                        document.Add(summaryTable);

                        // Статистика по агентам
                        if (report.AgentStatistics != null && report.AgentStatistics.Any())
                        {
                            var agentsTitle = new iTextElement.Paragraph("Статистика по агентам:")
                                .SetFont(titleFont)
                                .SetFontSize(14)
                                .SetMarginBottom(10);
                            document.Add(agentsTitle);

                            var agentTable = new iTextElement.Table(5)
                                .UseAllAvailableWidth()
                                .SetMarginBottom(20);

                            // Заголовки
                            agentTable.AddHeaderCell(CreateHeaderCell("Агент", titleFont));
                            agentTable.AddHeaderCell(CreateHeaderCell("Всего сделок", titleFont));
                            agentTable.AddHeaderCell(CreateHeaderCell("Завершено", titleFont));
                            agentTable.AddHeaderCell(CreateHeaderCell("Выручка", titleFont));
                            agentTable.AddHeaderCell(CreateHeaderCell("Успешность", titleFont));

                            // Данные
                            foreach (var agent in report.AgentStatistics)
                            {
                                agentTable.AddCell(CreateCell(agent.AgentName, false, normalFont));
                                agentTable.AddCell(CreateCell(agent.TotalDeals.ToString(), false, normalFont));
                                agentTable.AddCell(CreateCell(agent.CompletedDeals.ToString(), false, normalFont));
                                agentTable.AddCell(CreateCell(agent.TotalRevenue.ToString("C"), false, normalFont));
                                agentTable.AddCell(CreateCell($"{agent.SuccessRate:F1}%", false, normalFont));
                            }

                            document.Add(agentTable);
                        }

                        // Топ объектов
                        if (report.TopProperties != null && report.TopProperties.Any())
                        {
                            var propertiesTitle = new iTextElement.Paragraph("Топ объектов:")
                                .SetFont(titleFont)
                                .SetFontSize(14)
                                .SetMarginBottom(10);
                            document.Add(propertiesTitle);

                            var propertyTable = new iTextElement.Table(3)
                                .UseAllAvailableWidth();

                            // Заголовки
                            propertyTable.AddHeaderCell(CreateHeaderCell("Объект", titleFont));
                            propertyTable.AddHeaderCell(CreateHeaderCell("Кол-во сделок", titleFont));
                            propertyTable.AddHeaderCell(CreateHeaderCell("Общая выручка", titleFont));

                            // Данные
                            foreach (var property in report.TopProperties)
                            {
                                propertyTable.AddCell(CreateCell(property.PropertyTitle, false, normalFont));
                                propertyTable.AddCell(CreateCell(property.DealCount.ToString(), false, normalFont));
                                propertyTable.AddCell(CreateCell(property.TotalRevenue.ToString("C"), false, normalFont));
                            }

                            document.Add(propertyTable);
                        }

                        // Подпись
                        var signature = new iTextElement.Paragraph($"Сгенерировано: {DateTime.Now:dd.MM.yyyy HH:mm}")
                            .SetFont(italicFont)
                            .SetFontSize(10)
                            .SetTextAlignment(iTextProperties.TextAlignment.RIGHT)
                            .SetMarginTop(30);
                        document.Add(signature);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка экспорта в PDF: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            });
        }

        private iTextElement.Cell CreateCell(string text, bool isBold, iTextKernelFont.PdfFont font)
        {
            var cellFont = isBold
                ? iTextKernelFont.PdfFontFactory.CreateFont(iTextIOFont.Constants.StandardFonts.HELVETICA_BOLD)
                : font;

            var paragraph = new iTextElement.Paragraph(text)
                .SetFont(cellFont)
                .SetFontSize(10);

            var cell = new iTextElement.Cell().Add(paragraph);

            if (isBold)
            {
                cell.SetBackgroundColor(iTextColors.ColorConstants.LIGHT_GRAY);
            }

            cell.SetPadding(5);
            return cell;
        }

        private iTextElement.Cell CreateHeaderCell(string text, iTextKernelFont.PdfFont font)
        {
            var boldFont = iTextKernelFont.PdfFontFactory.CreateFont(iTextIOFont.Constants.StandardFonts.HELVETICA_BOLD);

            return new iTextElement.Cell()
                .Add(new iTextElement.Paragraph(text)
                    .SetFont(boldFont)
                    .SetFontSize(10))
                .SetBackgroundColor(iTextColors.ColorConstants.DARK_GRAY)
                .SetFontColor(iTextColors.ColorConstants.WHITE)
                .SetTextAlignment(iTextProperties.TextAlignment.CENTER)
                .SetPadding(8);
        }

        // Метод для экспорта в Excel (без изменений)
        public async Task<bool> ExportToExcelAsync(SalesReportDto report, string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

                    using (var package = new ExcelPackage())
                    {
                        // Основной лист
                        var worksheet = package.Workbook.Worksheets.Add("Отчет по продажам");

                        // Заголовок
                        worksheet.Cells["A1"].Value = "Отчет по продажам";
                        worksheet.Cells["A1"].Style.Font.Bold = true;
                        worksheet.Cells["A1"].Style.Font.Size = 16;
                        worksheet.Cells["A1:E1"].Merge = true;

                        // Период
                        worksheet.Cells["A2"].Value = $"Период: {report.StartDate:dd.MM.yyyy} - {report.EndDate:dd.MM.yyyy}";
                        worksheet.Cells["A2:E2"].Merge = true;

                        // Сводная информация
                        worksheet.Cells["A4"].Value = "Сводная информация";
                        worksheet.Cells["A4"].Style.Font.Bold = true;
                        worksheet.Cells["A4"].Style.Font.Size = 14;

                        int row = 5;

                        // Таблица сводной информации
                        var summaryData = new[]
                        {
                            new { Label = "Общая выручка:", Value = report.TotalRevenue.ToString("C") },
                            new { Label = "Всего сделок:", Value = report.TotalDeals.ToString() },
                            new { Label = "Завершено сделок:", Value = report.CompletedDeals.ToString() },
                            new { Label = "В ожидании:", Value = report.PendingDeals.ToString() },
                            new { Label = "Средняя сумма сделки:", Value = report.AverageDealAmount.ToString("C") }
                        };

                        foreach (var item in summaryData)
                        {
                            worksheet.Cells[row, 1].Value = item.Label;
                            worksheet.Cells[row, 2].Value = item.Value;
                            row++;
                        }

                        row += 2; // Пропуск строки

                        // Статистика по агентам
                        if (report.AgentStatistics != null && report.AgentStatistics.Any())
                        {
                            worksheet.Cells[row, 1].Value = "Статистика по агентам";
                            worksheet.Cells[row, 1].Style.Font.Bold = true;
                            worksheet.Cells[row, 1].Style.Font.Size = 14;
                            row++;

                            // Заголовки
                            worksheet.Cells[row, 1].Value = "Агент";
                            worksheet.Cells[row, 2].Value = "Всего сделок";
                            worksheet.Cells[row, 3].Value = "Завершено";
                            worksheet.Cells[row, 4].Value = "Выручка";
                            worksheet.Cells[row, 5].Value = "Успешность";

                            // Стиль заголовков
                            for (int col = 1; col <= 5; col++)
                            {
                                worksheet.Cells[row, col].Style.Font.Bold = true;
                                worksheet.Cells[row, col].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                                worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                                worksheet.Cells[row, col].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                            }

                            row++;

                            // Данные
                            foreach (var agent in report.AgentStatistics)
                            {
                                worksheet.Cells[row, 1].Value = agent.AgentName;
                                worksheet.Cells[row, 2].Value = agent.TotalDeals;
                                worksheet.Cells[row, 3].Value = agent.CompletedDeals;
                                worksheet.Cells[row, 4].Value = agent.TotalRevenue;
                                worksheet.Cells[row, 4].Style.Numberformat.Format = "$#,##0.00";
                                worksheet.Cells[row, 5].Value = agent.SuccessRate / 100; // как процент
                                worksheet.Cells[row, 5].Style.Numberformat.Format = "0.0%";

                                // Границы
                                for (int col = 1; col <= 5; col++)
                                {
                                    worksheet.Cells[row, col].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                                }

                                row++;
                            }
                        }

                        row += 2; // Пропуск строки

                        // Топ объектов
                        if (report.TopProperties != null && report.TopProperties.Any())
                        {
                            worksheet.Cells[row, 1].Value = "Топ объектов";
                            worksheet.Cells[row, 1].Style.Font.Bold = true;
                            worksheet.Cells[row, 1].Style.Font.Size = 14;
                            row++;

                            // Заголовки
                            worksheet.Cells[row, 1].Value = "Объект";
                            worksheet.Cells[row, 2].Value = "Кол-во сделок";
                            worksheet.Cells[row, 3].Value = "Общая выручка";

                            // Стиль заголовков
                            for (int col = 1; col <= 3; col++)
                            {
                                worksheet.Cells[row, col].Style.Font.Bold = true;
                                worksheet.Cells[row, col].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                                worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                                worksheet.Cells[row, col].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                            }

                            row++;

                            // Данные
                            foreach (var property in report.TopProperties)
                            {
                                worksheet.Cells[row, 1].Value = property.PropertyTitle;
                                worksheet.Cells[row, 2].Value = property.DealCount;
                                worksheet.Cells[row, 3].Value = property.TotalRevenue;
                                worksheet.Cells[row, 3].Style.Numberformat.Format = "$#,##0.00";

                                // Границы
                                for (int col = 1; col <= 3; col++)
                                {
                                    worksheet.Cells[row, col].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                                }

                                row++;
                            }
                        }

                        // Автонастройка ширины колонок
                        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                        // Сохраняем файл
                        var file = new FileInfo(filePath);
                        package.SaveAs(file);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка экспорта в Excel: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            });
        }

        // Метод для экспорта в CSV
        public async Task<bool> ExportToCsvAsync(SalesReportDto report, string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var lines = new List<string>
                    {
                        "Отчет по продажам",
                        $"Период: {report.StartDate:dd.MM.yyyy} - {report.EndDate:dd.MM.yyyy}",
                        "",
                        "Сводная информация:",
                        $"Общая выручка,{report.TotalRevenue:C}",
                        $"Всего сделок,{report.TotalDeals}",
                        $"Завершено сделок,{report.CompletedDeals}",
                        $"В процессе,{report.PendingDeals}",
                        $"Средняя сумма сделки,{report.AverageDealAmount:C}",
                        ""
                    };

                    if (report.AgentStatistics != null && report.AgentStatistics.Any())
                    {
                        lines.Add("Статистика по агентам:");
                        lines.Add("Агент,Всего сделок,Завершено,Выручка,Успешность");

                        foreach (var agent in report.AgentStatistics)
                        {
                            lines.Add($"{agent.AgentName},{agent.TotalDeals},{agent.CompletedDeals},{agent.TotalRevenue:C},{agent.SuccessRate:F1}%");
                        }

                        lines.Add("");
                    }

                    if (report.TopProperties != null && report.TopProperties.Any())
                    {
                        lines.Add("Топ объектов:");
                        lines.Add("Объект,Кол-во сделок,Общая выручка");

                        foreach (var property in report.TopProperties)
                        {
                            lines.Add($"{property.PropertyTitle},{property.DealCount},{property.TotalRevenue:C}");
                        }
                    }

                    File.WriteAllLines(filePath, lines, Encoding.UTF8);
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка экспорта в CSV: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            });
        }
    }
}