using System;
using System.ComponentModel.DataAnnotations;

namespace Agencies.Core.DTO
{
    public class DealDto
    {
        public int Id { get; set; }
        public string FullName => Client?.FullName ?? string.Empty;
        public int PropertyId { get; set; }
        public PropertyDto Property { get; set; }

        public int ClientId { get; set; }
        public ClientDto Client { get; set; }

        [Range(0, double.MaxValue)]
        public double DealAmount { get; set; }

        public DateTime DealDate { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; }

        public int AgentId { get; set; }
        public string AgentName { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class CreateDealRequest
    {
        [Required]
        public int PropertyId { get; set; }

        [Required]
        public int ClientId { get; set; }

        [Range(0, double.MaxValue)]
        public double DealAmount { get; set; }

        public DateTime DealDate { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; }

        public int AgentId { get; set; }
    }

    public class UpdateDealRequest : CreateDealRequest
    {
    }

    public class DealStatisticsDto
    {
        public int TotalDeals { get; set; }
        public int CompletedDeals { get; set; }
        public int PendingDeals { get; set; }
        public int CancelledDeals { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageDealAmount { get; set; }
        public Dictionary<string, int> DealsByMonth { get; set; }
    }
}