using Agencies.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Agencies.Infrastructure.Data
{
    public static class SeedData
    {
        public static async Task Initialize(ApplicationDbContext context)
        {
            try
            {
                Console.WriteLine("Starting data seeding...");

                await SeedUsersAsync(context);
                await SeedPropertiesAsync(context);
                await SeedClientsAsync(context);
                await SeedDealsAsync(context);

                Console.WriteLine("Data seeding completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during data seeding: {ex.Message}");
                throw;
            }
        }

        private static async Task SeedUsersAsync(ApplicationDbContext context)
        {
            Console.WriteLine("Seeding users...");

            if (!await context.Users.AnyAsync())
            {
                var users = new[]
                {
                    new User
                    {
                        Username = "admin",
                        Email = "admin@agencies.com",
                        PasswordHash = HashPassword("admin123"),
                        Role = "Admin",
                        CreatedAt = DateTime.UtcNow
                    },
                    new User
                    {
                        Username = "agent1",
                        Email = "agent1@agencies.com",
                        PasswordHash = HashPassword("agent123"),
                        Role = "User",
                        CreatedAt = DateTime.UtcNow
                    },
                    new User
                    {
                        Username = "agent2",
                        Email = "agent2@agencies.com",
                        PasswordHash = HashPassword("agent123"),
                        Role = "User",
                        CreatedAt = DateTime.UtcNow
                    }
                };

                await context.Users.AddRangeAsync(users);
                await context.SaveChangesAsync();
                Console.WriteLine($"Seeded {users.Length} users.");
            }
            else
            {
                Console.WriteLine("Users already exist, skipping seeding.");
            }
        }

        private static async Task SeedPropertiesAsync(ApplicationDbContext context)
        {
            Console.WriteLine("Seeding properties...");

            if (!await context.Properties.AnyAsync())
            {
                var admin = await context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
                var agent1 = await context.Users.FirstOrDefaultAsync(u => u.Username == "agent1");

                if (admin == null || agent1 == null)
                {
                    Console.WriteLine("Required users not found for property seeding.");
                    return;
                }

                var properties = new[]
                {
                    new Property
                    {
                        Title = "Квартира в центре",
                        Description = "Просторная 3-комнатная квартира в историческом центре",
                        Address = "ул. Центральная, 10",
                        Price = 4500000,
                        Area = 85.5,
                        Type = "Apartment",
                        Rooms = 3,
                        IsAvailable = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedByUserId = admin.Id
                    },
                    new Property
                    {
                        Title = "Загородный дом",
                        Description = "Новый дом с участком 10 соток",
                        Address = "д. Лесная, 25",
                        Price = 8500000,
                        Area = 120.0,
                        Type = "House",
                        Rooms = 4,
                        IsAvailable = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedByUserId = agent1.Id
                    },
                    new Property
                    {
                        Title = "Офисное помещение",
                        Description = "Офис в бизнес-центре класса А",
                        Address = "пр. Деловой, 15",
                        Price = 12000000,
                        Area = 200.0,
                        Type = "Commercial",
                        Rooms = 8,
                        IsAvailable = false,
                        CreatedAt = DateTime.UtcNow,
                        CreatedByUserId = admin.Id
                    }
                };

                await context.Properties.AddRangeAsync(properties);
                await context.SaveChangesAsync();
                Console.WriteLine($"Seeded {properties.Length} properties.");
            }
            else
            {
                Console.WriteLine("Properties already exist, skipping seeding.");
            }
        }

        private static async Task SeedClientsAsync(ApplicationDbContext context)
        {
            Console.WriteLine("Seeding clients...");

            if (!await context.Clients.AnyAsync())
            {
                var agent1 = await context.Users.FirstOrDefaultAsync(u => u.Username == "agent1");

                if (agent1 == null)
                {
                    Console.WriteLine("Agent1 not found for client seeding.");
                    return;
                }

                var clients = new[]
                {
                    new Client
                    {
                        FirstName = "Иван",
                        LastName = "Петров",
                        Phone = "+7 (123) 456-78-90",
                        Email = "ivan@example.com",
                        Requirements = "Ищет 2-комнатную квартиру в центре",
                        Budget = 3500000,
                        CreatedAt = DateTime.UtcNow,
                        AgentId = agent1.Id
                    },
                    new Client
                    {
                        FirstName = "Мария",
                        LastName = "Сидорова",
                        Phone = "+7 (987) 654-32-10",
                        Email = "maria@example.com",
                        Requirements = "Нужен дом за городом",
                        Budget = 7000000,
                        CreatedAt = DateTime.UtcNow,
                        AgentId = agent1.Id
                    }
                };

                await context.Clients.AddRangeAsync(clients);
                await context.SaveChangesAsync();
                Console.WriteLine($"Seeded {clients.Length} clients.");
            }
            else
            {
                Console.WriteLine("Clients already exist, skipping seeding.");
            }
        }

        private static string HashPassword(string password)
        {
            // Simplified hashing for seeding
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(password + "salt");
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private static async Task SeedDealsAsync(ApplicationDbContext context)
        {
            Console.WriteLine("Seeding deals...");

            if (!await context.Deals.AnyAsync())
            {
                var agent1 = await context.Users.FirstOrDefaultAsync(u => u.Username == "agent1");
                var property1 = await context.Properties.FirstOrDefaultAsync(p => p.Title == "Квартира в центре");
                var property2 = await context.Properties.FirstOrDefaultAsync(p => p.Title == "Загородный дом");
                var client1 = await context.Clients.FirstOrDefaultAsync(c => c.FirstName == "Иван");
                var client2 = await context.Clients.FirstOrDefaultAsync(c => c.FirstName == "Мария");

                if (agent1 != null && property1 != null && property2 != null && client1 != null && client2 != null)
                {
                    var deals = new[]
                    {
                new Deal
                {
                    PropertyId = property1.Id,
                    ClientId = client1.Id,
                    DealAmount = 4200000, // Скидка 300000
                    DealDate = DateTime.UtcNow.AddDays(-30),
                    Status = "Completed",
                    AgentId = agent1.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-60)
                },
                new Deal
                {
                    PropertyId = property2.Id,
                    ClientId = client2.Id,
                    DealAmount = 8200000, // Скидка 300000
                    DealDate = DateTime.UtcNow.AddDays(-15),
                    Status = "Pending",
                    AgentId = agent1.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-45)
                }
            };

                    await context.Deals.AddRangeAsync(deals);
                    await context.SaveChangesAsync();
                    Console.WriteLine($"Seeded {deals.Length} deals.");

                    // Обновляем статус объектов недвижимости
                    property1.IsAvailable = false; // Квартира продана
                    property2.IsAvailable = false; // Дом в процессе продажи

                    context.Properties.UpdateRange(property1, property2);
                    await context.SaveChangesAsync();
                }
                else
                {
                    Console.WriteLine("Required data not found for deal seeding.");
                }
            }
            else
            {
                Console.WriteLine("Deals already exist, skipping seeding.");
            }
        }
    }
}