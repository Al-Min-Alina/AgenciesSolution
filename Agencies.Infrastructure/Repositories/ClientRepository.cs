using Agencies.Domain.Models;
using Agencies.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Agencies.Infrastructure.Repositories
{
    public interface IClientRepository : IRepository<Client>
    {
        Task<IEnumerable<Client>> GetClientsByAgentAsync(int agentId);
        Task<IEnumerable<Client>> SearchClientsAsync(string searchTerm);
        Task<IEnumerable<Client>> GetClientsWithDealsAsync();

        Task<bool> HasDealsAsync(int clientId);
    }

    public class ClientRepository : Repository<Client>, IClientRepository
    {
        public ClientRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Client>> GetClientsByAgentAsync(int agentId)
        {
            return await _context.Clients
                .Include(c => c.Agent)
                .Where(c => c.AgentId == agentId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Client>> SearchClientsAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return await GetAllAsync();
            }

            searchTerm = searchTerm.ToLower();

            return await _context.Clients
                .Include(c => c.Agent)
                .Where(c =>
                    c.FirstName.ToLower().Contains(searchTerm) ||
                    c.LastName.ToLower().Contains(searchTerm) ||
                    c.Email.ToLower().Contains(searchTerm) ||
                    c.Phone.Contains(searchTerm) ||
                    c.Requirements.ToLower().Contains(searchTerm))
                .ToListAsync();
        }

        public async Task<IEnumerable<Client>> GetClientsWithDealsAsync()
        {
            return await _context.Clients
                .Include(c => c.Agent)
                .Include(c => c.Deals)
                    .ThenInclude(d => d.Property)
                .ToListAsync();
        }

        public override async Task<IEnumerable<Client>> GetAllAsync()
        {
            return await _context.Clients
                .Include(c => c.Agent)
                .ToListAsync();
        }

        public override async Task<Client> GetByIdAsync(int id)
        {
            return await _context.Clients
                .Include(c => c.Agent)
                .Include(c => c.Deals)
                    .ThenInclude(d => d.Property)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<bool> HasDealsAsync(int clientId)
        {
            return await _context.Clients
                .Include(c => c.Deals)
                .Where(c => c.Id == clientId)
                .SelectMany(c => c.Deals)
                .AnyAsync();
        }
    }
}