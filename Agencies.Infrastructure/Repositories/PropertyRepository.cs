using Agencies.Domain.Models;
using Agencies.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Agencies.Infrastructure.Repositories
{
    public interface IPropertyRepository : IRepository<Property>
    {
        Task<IEnumerable<Property>> GetAvailablePropertiesAsync();
        Task<IEnumerable<Property>> SearchPropertiesAsync(string searchTerm, string propertyType = null);
        Task<IEnumerable<Property>> GetPropertiesByUserIdAsync(int userId);
    }

    public class PropertyRepository : Repository<Property>, IPropertyRepository
    {
        public PropertyRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Property>> GetAvailablePropertiesAsync()
        {
            return await _context.Properties
                .Where(p => p.IsAvailable)
                .Include(p => p.CreatedByUser)
                .ToListAsync();
        }

        public async Task<IEnumerable<Property>> SearchPropertiesAsync(string searchTerm, string propertyType = null)
        {
            var query = _context.Properties
                .Include(p => p.CreatedByUser)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(p =>
                    p.Title.ToLower().Contains(searchTerm) ||
                    p.Description.ToLower().Contains(searchTerm) ||
                    p.Address.ToLower().Contains(searchTerm));
            }

            if (!string.IsNullOrWhiteSpace(propertyType))
            {
                query = query.Where(p => p.Type == propertyType);
            }

            return await query.ToListAsync();
        }

        public async Task<IEnumerable<Property>> GetPropertiesByUserIdAsync(int userId)
        {
            return await _context.Properties
                .Where(p => p.CreatedByUserId == userId)
                .Include(p => p.CreatedByUser)
                .ToListAsync();
        }
    }
}