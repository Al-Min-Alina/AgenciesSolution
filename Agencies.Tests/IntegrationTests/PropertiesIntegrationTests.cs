using Agencies.API;
using Agencies.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Agencies.Tests.IntegrationTests
{
    public class PropertiesIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        private readonly string _adminToken;

        public PropertiesIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Заменяем реальную базу данных на InMemory
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

                    if (descriptor != null)
                        services.Remove(descriptor);

                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("TestDatabase");
                    });
                });
            });

            _client = _factory.CreateClient();
            _adminToken = GetAdminToken().Result;
        }

        private async Task<string> GetAdminToken()
        {
            var loginRequest = new
            {
                Username = "admin",
                Password = "admin123"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(loginRequest),
                Encoding.UTF8,
                "application/json");

            var response = await _client.PostAsync("/api/auth/login", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseContent);
            return doc.RootElement.GetProperty("token").GetString();
        }

        [Fact]
        public async Task GetProperties_ReturnsSuccessStatusCode()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _adminToken);

            // Act
            var response = await _client.GetAsync("/api/properties");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal("application/json; charset=utf-8",
                response.Content.Headers.ContentType.ToString());
        }

        [Fact]
        public async Task CreateProperty_ValidData_ReturnsCreated()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _adminToken);

            var property = new
            {
                Title = "Integration Test Property",
                Address = "Test Address 123",
                Price = 500000,
                Area = 100.5m,
                Type = "Apartment",
                Rooms = 3,
                IsAvailable = true,
                Description = "Test description"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(property),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await _client.PostAsync("/api/properties", content);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task CreateProperty_InvalidData_ReturnsBadRequest()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _adminToken);

            var invalidProperty = new
            {
                Title = "", // Пустое название
                Address = "Test",
                Price = -100, // Отрицательная цена
                Area = 0, // Нулевая площадь
                Type = "InvalidType",
                Rooms = -1 // Отрицательное количество комнат
            };

            var content = new StringContent(
                JsonSerializer.Serialize(invalidProperty),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await _client.PostAsync("/api/properties", content);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UnauthorizedAccess_WithoutToken_ReturnsUnauthorized()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = null;

            // Act
            var response = await _client.GetAsync("/api/properties");

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AccessDenied_UserRole_ReturnsForbidden()
        {
            // Arrange
            // Получаем токен для обычного пользователя
            var userLoginRequest = new
            {
                Username = "agent1",
                Password = "agent123"
            };

            var loginContent = new StringContent(
                JsonSerializer.Serialize(userLoginRequest),
                Encoding.UTF8,
                "application/json");

            var loginResponse = await _client.PostAsync("/api/auth/login", loginContent);
            var loginResponseContent = await loginResponse.Content.ReadAsStringAsync();
            using var loginDoc = JsonDocument.Parse(loginResponseContent);
            var userToken = loginDoc.RootElement.GetProperty("token").GetString();

            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", userToken);

            var property = new
            {
                Title = "Test Property by User",
                Address = "Test Address",
                Price = 300000,
                Area = 80,
                Type = "Apartment",
                Rooms = 2,
                IsAvailable = true
            };

            var content = new StringContent(
                JsonSerializer.Serialize(property),
                Encoding.UTF8,
                "application/json");

            // Act - обычный пользователь пытается создать объект
            var response = await _client.PostAsync("/api/properties", content);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}