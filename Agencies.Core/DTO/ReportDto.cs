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
        public double TotalRevenue { get; set; }             
        public double AverageDealAmount { get; set; }         
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
        public double TotalRevenue { get; set; }             
        public double SuccessRate => TotalDeals > 0 ? (double)CompletedDeals / TotalDeals * 100 : 0;  
        public string SuccessRateFormatted => $"{SuccessRate:N1}%";
    }

    public class MonthlyStatisticsDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
        public int DealCount { get; set; }
        public double TotalRevenue { get; set; }              
        public double AverageDealAmount { get; set; }     
    }

    public class PropertyStatisticsDto
    {
        public int PropertyId { get; set; }
        public string PropertyTitle { get; set; }
        public int DealCount { get; set; }
        public double TotalRevenue { get; set; }              
    }

    public class PropertyAnalysisReportDto
    {
        public DateTime GeneratedDate { get; set; }
        public int TotalProperties { get; set; }
        public int AvailableProperties { get; set; }
        public int SoldProperties { get; set; }
        public double TotalPropertyValue { get; set; }        
        public double AveragePropertyPrice { get; set; }   
        public double MinPrice { get; set; }                  
        public double MaxPrice { get; set; }                  
        public double MedianPrice { get; set; }             
        public List<PropertyTypeAnalysisDto> PropertyTypeAnalysis { get; set; }
        public List<RoomDistributionDto> RoomDistribution { get; set; }
        public List<PriceSegmentDto> PriceSegments { get; set; }
        public int PropertiesWithDeals { get; set; }
        public double AverageDealAmountPerProperty { get; set; }  
    }

    public class PropertyTypeAnalysisDto
    {
        public string PropertyType { get; set; }
        public int Count { get; set; }
        public double AveragePrice { get; set; }             
        public double AverageArea { get; set; }
        public int SoldCount { get; set; }
        public double SoldPercentage { get; set; }            
    }

    public class RoomDistributionDto
    {
        public int Rooms { get; set; }
        public int Count { get; set; }
        public double Percentage { get; set; }               
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
        public double TotalRevenue { get; set; }              
        public double AverageDealAmount { get; set; }         
        public double SuccessRate { get; set; }               
        public TimeSpan AverageDealTime { get; set; }
        public int TotalClients { get; set; }
        public int ActiveClients { get; set; }
    }

    public class ClientAnalysisReportDto
    {
        public DateTime GeneratedDate { get; set; }
        public int TotalClients { get; set; }
        public int ClientsWithDeals { get; set; }
        public double AverageBudget { get; set; }             
        public double TotalPotentialValue { get; set; }      
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
        public double TotalSpent { get; set; }                
        public DateTime LastDealDate { get; set; }
    }

    public class RequirementAnalysisDto
    {
        public string Requirement { get; set; }
        public int Count { get; set; }
        public double Percentage { get; set; }                
    }

    public class FinancialReportDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime GeneratedDate { get; set; }
        public double TotalRevenue { get; set; }             
        public int TotalDeals { get; set; }
        public double AverageCommission { get; set; }         
        public double EstimatedCommission { get; set; }      
        public List<MonthlyRevenueDto> MonthlyRevenue { get; set; }
        public FinancialForecastDto NextMonthForecast { get; set; }
    }

    public class MonthlyRevenueDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
        public double Revenue { get; set; }                   
        public double Commission { get; set; }                
        public int DealCount { get; set; }
    }

    public class FinancialForecastDto
    {
        public double ForecastedRevenue { get; set; }        
        public double ForecastedCommission { get; set; }      
        public double ConfidenceLevel { get; set; }          
    }
}