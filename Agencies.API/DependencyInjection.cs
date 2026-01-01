using Agencies.API.Services;
using Agencies.Infrastructure.Data;
using Agencies.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Agencies.API
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Database
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            // Repositories
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IPropertyRepository, PropertyRepository>();
            services.AddScoped<IClientRepository, ClientRepository>();
            services.AddScoped<IDealRepository, DealRepository>();
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

            // Services
            services.AddScoped<IAuthService, AuthService>();

            return services;
        }
    }
}