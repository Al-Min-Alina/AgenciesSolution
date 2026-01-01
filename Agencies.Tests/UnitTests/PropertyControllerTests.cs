using Agencies.API.Controllers;
using Agencies.Core.DTO;
using Agencies.Domain.Models;
using Agencies.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

namespace Agencies.Tests.UnitTests
{
    public class PropertyControllerTests
    {
        private readonly Mock<IPropertyRepository> _mockPropertyRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<ILogger<PropertiesController>> _mockLogger;
        private readonly PropertiesController _controller;

        public PropertyControllerTests()
        {
            _mockPropertyRepository = new Mock<IPropertyRepository>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockLogger = new Mock<ILogger<PropertiesController>>();

            // Исправлено: добавлен ILogger в конструктор
            _controller = new PropertiesController(
                _mockPropertyRepository.Object,
                _mockUserRepository.Object,
                _mockLogger.Object);

            SetupControllerContext();
        }

        private void SetupControllerContext()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(ClaimTypes.Role, "Admin")
            }, "TestAuthentication"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        [Fact]
        public async Task GetProperties_ReturnsOkResult_WithListOfProperties()
        {
            // Arrange
            var properties = new List<Property>
            {
                new Property { Id = 1, Title = "Test Property 1", Price = 100000, IsAvailable = true },
                new Property { Id = 2, Title = "Test Property 2", Price = 200000, IsAvailable = true }
            };

            _mockPropertyRepository.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(properties);

            // Act
            var result = await _controller.GetProperties();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedProperties = Assert.IsType<List<PropertyDto>>(okResult.Value);
            Assert.Equal(2, returnedProperties.Count);
        }

        [Fact]
        public async Task GetProperty_WithValidId_ReturnsProperty()
        {
            // Arrange
            var property = new Property
            {
                Id = 1,
                Title = "Test Property",
                Price = 100000,
                IsAvailable = true,
                CreatedByUserId = 1
            };

            _mockPropertyRepository.Setup(repo => repo.GetByIdAsync(1))
                .ReturnsAsync(property);

            // Act
            var result = await _controller.GetProperty(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedProperty = Assert.IsType<PropertyDto>(okResult.Value);
            Assert.Equal("Test Property", returnedProperty.Title);
            Assert.Equal(100000, returnedProperty.Price);
        }

        [Fact]
        public async Task GetProperty_WithInvalidId_ReturnsNotFound()
        {
            // Arrange
            _mockPropertyRepository.Setup(repo => repo.GetByIdAsync(1))
                .ReturnsAsync((Property)null);

            // Act
            var result = await _controller.GetProperty(1);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task CreateProperty_ValidRequest_ReturnsCreatedAtActionResult()
        {
            // Arrange
            var request = new CreatePropertyRequest
            {
                Title = "New Property",
                Address = "Test Address",
                Price = 150000,
                Area = 100,
                Type = "Apartment",
                Rooms = 3,
                IsAvailable = true
            };

            var createdProperty = new Property
            {
                Id = 1,
                Title = request.Title,
                Price = request.Price,
                CreatedByUserId = 1
            };

            _mockPropertyRepository.Setup(repo => repo.AddAsync(It.IsAny<Property>()))
                .ReturnsAsync(createdProperty);

            // Act
            var result = await _controller.CreateProperty(request);

            // Assert
            var actionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returnedProperty = Assert.IsType<PropertyDto>(actionResult.Value);
            Assert.Equal(1, returnedProperty.Id);
            Assert.Equal("New Property", returnedProperty.Title);
        }

        [Fact]
        public async Task UpdateProperty_ValidRequest_ReturnsNoContent()
        {
            // Arrange
            var existingProperty = new Property
            {
                Id = 1,
                Title = "Old Title",
                CreatedByUserId = 1
            };

            var request = new UpdatePropertyRequest
            {
                Title = "Updated Title",
                Address = "Updated Address",
                Price = 200000,
                Area = 120,
                Type = "House",
                Rooms = 4,
                IsAvailable = true
            };

            _mockPropertyRepository.Setup(repo => repo.GetByIdAsync(1))
                .ReturnsAsync(existingProperty);

            // Act
            var result = await _controller.UpdateProperty(1, request);

            // Assert
            Assert.IsType<NoContentResult>(result);
            _mockPropertyRepository.Verify(repo => repo.UpdateAsync(It.IsAny<Property>()), Times.Once);
        }

        [Fact]
        public async Task DeleteProperty_ExistingProperty_ReturnsNoContent()
        {
            // Arrange
            var property = new Property { Id = 1, CreatedByUserId = 1 };
            _mockPropertyRepository.Setup(repo => repo.GetByIdAsync(1))
                .ReturnsAsync(property);

            // Act
            var result = await _controller.DeleteProperty(1);

            // Assert
            Assert.IsType<NoContentResult>(result);
            _mockPropertyRepository.Verify(repo => repo.DeleteAsync(property), Times.Once);
        }

        [Fact]
        public async Task SearchProperties_WithTerm_ReturnsFilteredResults()
        {
            // Arrange
            var properties = new List<Property>
            {
                new Property { Id = 1, Title = "Apartment in Center", Address = "Central Street" },
                new Property { Id = 2, Title = "House in Suburb", Address = "Suburb Street" }
            };

            _mockPropertyRepository.Setup(repo => repo.SearchPropertiesAsync("Center", null))
                .ReturnsAsync(properties.Where(p => p.Title.Contains("Center")).ToList());

            // Act
            var result = await _controller.SearchProperties("Center", null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedProperties = Assert.IsType<List<PropertyDto>>(okResult.Value);
            Assert.Single(returnedProperties);
            Assert.Contains("Center", returnedProperties[0].Title);
        }
    }
}