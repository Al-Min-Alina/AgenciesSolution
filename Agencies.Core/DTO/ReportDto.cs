using System;
using System.Collections.Generic;

namespace Agencies.Core.DTO
{
    public class SalesReportDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime GeneratedDate { get; set; }
        public int TotalDeals { get; set; }
        public int CompletedDeals { get; set; }
        public int PendingDeals { get; set; }
        public int CancelledDeals { get; set; }
        public double TotalRevenue { get; set; }              // decimal → double
        public double AverageDealAmount { get; set; }         // decimal → double
        public List<AgentStatisticsDto> AgentStatistics { get; set; }
        public List<MonthlyStatisticsDto> MonthlyStatistics { get; set; }
        public List<PropertyStatisticsDto> TopProperties { get; set; }
    }

    public class AgentStatisticsDto
    {
        public int AgentId { get; set; }
        public string AgentName { get; set; }
        public int TotalDeals { get; set; }
        public int CompletedDeals { get; set; }
        public double TotalRevenue { get; set; }              // decimal → double
        public double SuccessRate => TotalDeals > 0 ? (double)CompletedDeals / TotalDeals * 100 : 0;  // decimal → double
        public string SuccessRateFormatted => $"{SuccessRate:N1}%";
    }

    public class MonthlyStatisticsDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
        public int DealCount { get; set; }
        public double TotalRevenue { get; set; }              // decimal → double
        public double AverageDealAmount { get; set; }         // decimal → double
    }

    public class PropertyStatisticsDto
    {
        public int PropertyId { get; set; }
        public string PropertyTitle { get; set; }
        public int DealCount { get; set; }
        public double TotalRevenue { get; set; }              // decimal → double
    }

    public class PropertyAnalysisReportDto
    {
        public DateTime GeneratedDate { get; set; }
        public int TotalProperties { get; set; }
        public int AvailableProperties { get; set; }
        public int SoldProperties { get; set; }
        public double TotalPropertyValue { get; set; }        // decimal → double
        public double AveragePropertyPrice { get; set; }      // decimal → double
        public double MinPrice { get; set; }                  // decimal → double
        public double MaxPrice { get; set; }                  // decimal → double
        public double MedianPrice { get; set; }               // decimal → double
        public List<PropertyTypeAnalysisDto> PropertyTypeAnalysis { get; set; }
        public List<RoomDistributionDto> RoomDistribution { get; set; }
        public List<PriceSegmentDto> PriceSegments { get; set; }
        public int PropertiesWithDeals { get; set; }
        public double AverageDealAmountPerProperty { get; set; }  // decimal → double
    }

    public class PropertyTypeAnalysisDto
    {
        public string PropertyType { get; set; }
        public int Count { get; set; }
        public double AveragePrice { get; set; }              // decimal → double
        public double AverageArea { get; set; }
        public int SoldCount { get; set; }
        public double SoldPercentage { get; set; }            // decimal → double
    }

    public class RoomDistributionDto
    {
        public int Rooms { get; set; }
        public int Count { get; set; }
        public double Percentage { get; set; }                // decimal → double
    }

    public class PriceSegmentDto
    {
        public string Range { get; set; }
        public int Count { get; set; }
    }

    public class AgentPerformanceDto
    {
        public int AgentId { get; set; }
        public string AgentName { get; set; }
        public int TotalDeals { get; set; }
        public int CompletedDeals { get; set; }
        public int PendingDeals { get; set; }
        public int CancelledDeals { get; set; }
        public double TotalRevenue { get; set; }              // decimal → double
        public double AverageDealAmount { get; set; }         // decimal → double
        public double SuccessRate { get; set; }               // decimal → double
        public TimeSpan AverageDealTime { get; set; }
        public int TotalClients { get; set; }
        public int ActiveClients { get; set; }
    }

    public class ClientAnalysisReportDto
    {
        public DateTime GeneratedDate { get; set; }
        public int TotalClients { get; set; }
        public int ClientsWithDeals { get; set; }
        public double AverageBudget { get; set; }             // decimal → double
        public double TotalPotentialValue { get; set; }       // decimal → double
        public List<BudgetSegmentDto> BudgetDistribution { get; set; }
        public List<ClientStatisticsDto> TopClients { get; set; }
        public List<RequirementAnalysisDto> TopRequirements { get; set; }
    }

    public class BudgetSegmentDto
    {
        public string Range { get; set; }
        public int Count { get; set; }
    }

    public class ClientStatisticsDto
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; }
        public int DealCount { get; set; }
        public double TotalSpent { get; set; }                // decimal → double
        public DateTime LastDealDate { get; set; }
    }

    public class RequirementAnalysisDto
    {
        public string Requirement { get; set; }
        public int Count { get; set; }
        public double Percentage { get; set; }                // decimal → double
    }

    public class FinancialReportDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime GeneratedDate { get; set; }
        public double TotalRevenue { get; set; }              // decimal → double
        public int TotalDeals { get; set; }
        public double AverageCommission { get; set; }         // decimal → double
        public double EstimatedCommission { get; set; }       // decimal → double
        public List<MonthlyRevenueDto> MonthlyRevenue { get; set; }
        public FinancialForecastDto NextMonthForecast { get; set; }
    }

    public class MonthlyRevenueDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
        public double Revenue { get; set; }                   // decimal → double
        public double Commission { get; set; }                // decimal → double
        public int DealCount { get; set; }
    }

    public class FinancialForecastDto
    {
        public double ForecastedRevenue { get; set; }         // decimal → double
        public double ForecastedCommission { get; set; }      // decimal → double
        public double ConfidenceLevel { get; set; }           // decimal → double
    }
}