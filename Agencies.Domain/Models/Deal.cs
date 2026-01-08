using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agencies.Domain.Models
{
    public class Deal
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public Property Property { get; set; }
        public int ClientId { get; set; }
        public Client Client { get; set; }
        public double DealAmount { get; set; }
        public DateTime DealDate { get; set; }
        public string Status { get; set; } // "В ожидании", "Завершено", "Отменено"
        public int AgentId { get; set; }
        public User Agent { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
