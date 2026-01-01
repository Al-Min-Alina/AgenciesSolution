using System;
using System.ComponentModel.DataAnnotations;

namespace Agencies.Core.DTO
{

    public class ClientDto
    {
        public int Id { get; set; }
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(100)]
        public string LastName { get; set; }

        public string FullName => $"{FirstName ?? string.Empty} {LastName ?? string.Empty}".Trim();

        [StringLength(50)]
        public string Phone { get; set; }

        [EmailAddress]
        [StringLength(200)]
        public string Email { get; set; }

        [StringLength(1000)]
        public string Requirements { get; set; }

        [Range(0, double.MaxValue)]
        public double? Budget { get; set; }

        public DateTime CreatedAt { get; set; }

        public int? AgentId { get; set; }
        public string AgentName { get; set; }
    }

    public class CreateClientRequest
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(100)]
        public string LastName { get; set; }

        [StringLength(50)]
        public string Phone { get; set; }

        [EmailAddress]
        [StringLength(200)]
        public string Email { get; set; }

        [StringLength(1000)]
        public string Requirements { get; set; }

        [Range(0, double.MaxValue)]
        public double? Budget { get; set; }

        public int? AgentId { get; set; }
    }

    public class UpdateClientRequest : CreateClientRequest
    {

    }
}