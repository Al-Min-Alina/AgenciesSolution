using Agencies.Domain.Models;
using Agencies.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Agencies.Infrastructure.Repositories
{
    public interface IDealRepository : IRepository<Deal>
    {
        Task<IEnumerable<Deal>> GetDealsByAgentAsync(int agentId);
        Task<IEnumerable<Deal>> GetDealsByStatusAsync(string status);
        Task<IEnumerable<Deal>> GetDealsByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<Dictionary<string, int>> GetDealStatisticsAsync();
        Task<double> GetTotalRevenueAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<IEnumerable<Deal>> SearchDealsAsync(string searchTerm);
    }

    public class DealRepository : Repository<Deal>, IDealRepository
    {
        public DealRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Deal>> GetDealsByAgentAsync(int agentId)
        {
            return await _context.Deals
                .Include(d => d.Agent)
                .Include(d => d.Property)
                .Include(d => d.Client)
                .Where(d => d.AgentId == agentId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Deal>> GetDealsByStatusAsync(string status)
        {
            return await _context.Deals
                .Include(d => d.Agent)
                .Include(d => d.Property)
                .Include(d => d.Client)
                .Where(d => d.Status == status)
                .ToListAsync();
        }

        public async Task<IEnumerable<Deal>> GetDealsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.Deals
                .Include(d => d.Agent)
                .Include(d => d.Property)
                .Include(d => d.Client)
                .Where(d => d.DealDate >= startDate && d.DealDate <= endDate)
                .ToListAsync();
        }

        public async Task<Dictionary<string, int>> GetDealStatisticsAsync()
        {
            var stats = await _context.Deals
                .GroupBy(d => d.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            return stats;
        }

        public async Task<double> GetTotalRevenueAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Deals.AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(d => d.DealDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(d => d.DealDate <= endDate.Value);
            }

            var sum = await query.SumAsync(d => (double?)d.DealAmount);
            return sum ?? 0;
        }

        public async Task<IEnumerable<Deal>> SearchDealsAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return await GetAllAsync();
            }

            searchTerm = searchTerm.ToLower();

            return await _context.Deals
                .Include(d => d.Agent)
                .Include(d => d.Property)
                .Include(d => d.Client)
                .Where(d =>
                    d.Property.Title.ToLower().Contains(searchTerm) ||
                    d.Client.FirstName.ToLower().Contains(searchTerm) ||
                    d.Client.LastName.ToLower().Contains(searchTerm) ||
                    d.Status.ToLower().Contains(searchTerm))
                .ToListAsync();
        }

        public override async Task<IEnumerable<Deal>> GetAllAsync()
        {
            return await _context.Deals
                .Include(d => d.Agent)
                .Include(d => d.Property)
                .Include(d => d.Client)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        public override async Task<Deal> GetByIdAsync(int id)
        {
            return await _context.Deals
                .Include(d => d.Agent)
                .Include(d => d.Property)
                .Include(d => d.Client)
                .FirstOrDefaultAsync(d => d.Id == id);
        }
    }
}