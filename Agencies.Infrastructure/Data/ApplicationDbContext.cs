using Agencies.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static System.Net.Mime.MediaTypeNames;
using System;

namespace Agencies.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Property> Properties { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<Deal> Deals { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Конфигурация User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();

                // Дополнительные настройки
                entity.Property(e => e.Username).HasMaxLength(100);
                entity.Property(e => e.Email).HasMaxLength(200);
                entity.Property(e => e.PasswordHash).HasMaxLength(500);
                entity.Property(e => e.Role).HasMaxLength(50);
            });

            // Конфигурация Property
            modelBuilder.Entity<Property>(entity =>
            {
                entity.Property(e => e.Title).HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(2000);
                entity.Property(e => e.Address).HasMaxLength(500);
                entity.Property(e => e.Type).HasMaxLength(50);

                entity.Property(e => e.Price)
            .HasColumnType("float");  // Явно указываем SQL тип

                entity.Property(e => e.Area)
                    .HasColumnType("float");  // Явно указываем SQL тип

                entity.HasOne(p => p.CreatedByUser)
                      .WithMany()
                      .HasForeignKey(p => p.CreatedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Конфигурация Client
            modelBuilder.Entity<Client>(entity =>
            {
                entity.Property(e => e.FirstName).HasMaxLength(100);
                entity.Property(e => e.LastName).HasMaxLength(100);
                entity.Property(e => e.Phone).HasMaxLength(50);
                entity.Property(e => e.Email).HasMaxLength(200);
                entity.Property(e => e.Requirements).HasMaxLength(1000);

                entity.Property(e => e.Budget)
            .HasColumnType("float");  // Явно указываем SQL тип

                entity.HasOne(c => c.Agent)
                      .WithMany()
                      .HasForeignKey(c => c.AgentId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(c => c.Deals)
          .WithOne(d => d.Client)  // Указываем обратное свойство
          .HasForeignKey(d => d.ClientId)  // Указываем внешний ключ
          .OnDelete(DeleteBehavior.Restrict);
            });

            // Конфигурация Deal
            modelBuilder.Entity<Deal>(entity =>
            {
                entity.Property(e => e.Status).HasMaxLength(50);

                entity.Property(e => e.DealAmount)
            .HasColumnType("float");

                entity.HasOne(d => d.Property)
                      .WithMany()
                      .HasForeignKey(d => d.PropertyId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.Agent)
                      .WithMany()
                      .HasForeignKey(d => d.AgentId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Для отладки - включаем логирование SQL
                optionsBuilder.UseSqlServer("Data Source = (localdb)\\MSSQLLocalDB; Initial Catalog = AgenciesDB; Integrated Security = True; Connect Timeout = 30; Encrypt = False; Trust Server Certificate = False; Application Intent = ReadWrite; Multi Subnet Failover = False")
                             .EnableSensitiveDataLogging()  // Показывает значения параметров
                             .EnableDetailedErrors();       // Детальные ошибки
            }
        }
    }
}