using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agencies.Domain.Models
{
    public class Client
    {
        public int Id { get; set; }
        public string? FirstName { get; set; } = string.Empty;
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Requirements { get; set; }
        public double? Budget { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? AgentId { get; set; }
        public virtual User? Agent { get; set; }
        public virtual ICollection<Deal> Deals { get; set; } = new List<Deal>();
    }
}
