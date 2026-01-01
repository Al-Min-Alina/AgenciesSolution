using Agencies.Core.DTO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

// Используем псевдонимы для разрешения конфликта имен
using iText = iTextSharp.text;
using iTextParagraph = iTextSharp.text.Paragraph;
using iTextFont = iTextSharp.text.Font;
using iTextTable = iTextSharp.text.pdf.PdfPTable;
using iTextElement = iTextSharp.text.Element;

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
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        var document = new iText.Document(iText.PageSize.A4, 50, 50, 50, 50);
                        var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, stream);

                        document.Open();

                        // Заголовок
                        var titleFont = iText.FontFactory.GetFont("Arial", 18, iTextFont.BOLD);
                        var title = new iTextParagraph("Отчет по продажам", titleFont)
                        {
                            Alignment = iTextElement.ALIGN_CENTER,
                            SpacingAfter = 20
                        };
                        document.Add(title);

                        // Период отчета
                        var periodFont = iText.FontFactory.GetFont("Arial", 12);
                        var period = new iTextParagraph($"Период: {report.StartDate:dd.MM.yyyy} - {report.EndDate:dd.MM.yyyy}", periodFont)
                        {
                            SpacingAfter = 10
                        };
                        document.Add(period);

                        // Сводная информация
                        document.Add(new iTextParagraph("Сводная информация:",
                            iText.FontFactory.GetFont("Arial", 14, iTextFont.BOLD))
                        {
                            SpacingAfter = 10
                        });

                        var summaryTable = new iTextTable(2)
                        {
                            WidthPercentage = 100,
                            SpacingAfter = 20
                        };

                        summaryTable.AddCell("Общая выручка:");
                        summaryTable.AddCell(report.TotalRevenue.ToString("C"));
                        summaryTable.AddCell("Всего сделок:");
                        summaryTable.AddCell(report.TotalDeals.ToString());
                        summaryTable.AddCell("Завершено сделок:");
                        summaryTable.AddCell(report.CompletedDeals.ToString());
                        summaryTable.AddCell("В процессе:");
                        summaryTable.AddCell(report.PendingDeals.ToString());
                        summaryTable.AddCell("Средняя сумма сделки:");
                        summaryTable.AddCell(report.AverageDealAmount.ToString("C"));

                        document.Add(summaryTable);

                        // Статистика по агентам
                        if (report.AgentStatistics != null && report.AgentStatistics.Any())
                        {
                            document.Add(new iTextParagraph("Статистика по агентам:",
                                iText.FontFactory.GetFont("Arial", 14, iTextFont.BOLD))
                            {
                                SpacingAfter = 10
                            });

                            var agentTable = new iTextTable(5)
                            {
                                WidthPercentage = 100,
                                SpacingAfter = 20
                            };

                            // Заголовки таблицы
                            agentTable.AddCell("Агент");
                            agentTable.AddCell("Всего сделок");
                            agentTable.AddCell("Завершено");
                            agentTable.AddCell("Выручка");
                            agentTable.AddCell("Успешность");

                            // Данные
                            foreach (var agent in report.AgentStatistics)
                            {
                                agentTable.AddCell(agent.AgentName);
                                agentTable.AddCell(agent.TotalDeals.ToString());
                                agentTable.AddCell(agent.CompletedDeals.ToString());
                                agentTable.AddCell(agent.TotalRevenue.ToString("C"));
                                agentTable.AddCell($"{agent.SuccessRate:F1}%");
                            }

                            document.Add(agentTable);
                        }

                        document.Close();
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            });
        }

        public async Task<bool> ExportToExcelAsync(SalesReportDto report, string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Используем EPPlus или другой библиотеку для Excel
                    // Для простоты создаем CSV файл
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
                        "",
                        "Статистика по агентам:",
                        "Агент,Всего сделок,Завершено,Выручка,Успешность"
                    };

                    if (report.AgentStatistics != null)
                    {
                        foreach (var agent in report.AgentStatistics)
                        {
                            lines.Add($"{agent.AgentName},{agent.TotalDeals},{agent.CompletedDeals},{agent.TotalRevenue:C},{agent.SuccessRate:F1}%");
                        }
                    }

                    File.WriteAllLines(filePath, lines);
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            });
        }
    }
}