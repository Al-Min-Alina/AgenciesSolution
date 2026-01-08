using Agencies.Core.DTO;
using Agencies.Domain.Models;
using Agencies.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Agencies.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "AdminOrUser")]
    public class DealsController : ControllerBase
    {
        private readonly IDealRepository _dealRepository;
        private readonly IPropertyRepository _propertyRepository;
        private readonly IClientRepository _clientRepository;
        private readonly IUserRepository _userRepository;

        public DealsController(
            IDealRepository dealRepository,
            IPropertyRepository propertyRepository,
            IClientRepository clientRepository,
            IUserRepository userRepository)
        {
            _dealRepository = dealRepository;
            _propertyRepository = propertyRepository;
            _clientRepository = clientRepository;
            _userRepository = userRepository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DealDto>>> GetDeals(
            [FromQuery] string search = "",
            [FromQuery] string status = "")
        {
            IEnumerable<Deal> deals;

            if (!string.IsNullOrWhiteSpace(search))
            {
                deals = await _dealRepository.SearchDealsAsync(search);
            }
            else if (!string.IsNullOrWhiteSpace(status))
            {
                deals = await _dealRepository.GetDealsByStatusAsync(status);
            }
            else
            {
                deals = await _dealRepository.GetAllAsync();
            }

            // Проверяем роль пользователя
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            if (userRole != "Admin")
            {
                // Пользователи видят только свои сделки
                deals = deals.Where(d => d.AgentId == userId);
            }

            var dealDtos = deals.Select(d => MapToDto(d)).ToList();
            return Ok(dealDtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<DealDto>> GetDeal(int id)
        {
            var deal = await _dealRepository.GetByIdAsync(id);

            if (deal == null)
            {
                return NotFound();
            }

            // Проверка доступа
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            if (userRole != "Admin" && deal.AgentId != userId)
            {
                return Forbid();
            }

            return Ok(MapToDto(deal));
        }

        [HttpPost]
        public async Task<ActionResult<DealDto>> CreateDeal([FromBody] CreateDealRequest request)
        {
            // Валидация данных
            var property = await _propertyRepository.GetByIdAsync(request.PropertyId);
            if (property == null)
            {
                return BadRequest(new { message = "Property not found" });
            }

            var client = await _clientRepository.GetByIdAsync(request.ClientId);
            if (client == null)
            {
                return BadRequest(new { message = "Client not found" });
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // Определяем агента для сделки
            int agentId;
            if (request.AgentId > 0)
            {
                var agent = await _userRepository.GetByIdAsync(request.AgentId);
                if (agent == null)
                {
                    return BadRequest(new { message = "Agent not found" });
                }

                // Проверяем права
                if (userRole != "Admin" && request.AgentId != userId)
                {
                    return Forbid();
                }

                agentId = request.AgentId;
            }
            else
            {
                agentId = userId;
            }

            // Проверяем, доступен ли объект недвижимости
            if (!property.IsAvailable)
            {
                return BadRequest(new { message = "Property is not available" });
            }

            var deal = new Deal
            {
                PropertyId = request.PropertyId,
                ClientId = request.ClientId,
                DealAmount = request.DealAmount,
                DealDate = request.DealDate,
                Status = request.Status,
                AgentId = agentId,
                CreatedAt = DateTime.UtcNow
            };

            var createdDeal = await _dealRepository.AddAsync(deal);

            // Обновляем статус объекта недвижимости
            if (request.Status == "Завершено")
            {
                property.IsAvailable = false;
                await _propertyRepository.UpdateAsync(property);
            }

            return CreatedAtAction(nameof(GetDeal), new { id = createdDeal.Id }, MapToDto(createdDeal));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDeal(int id, [FromBody] UpdateDealRequest request)
        {
            var deal = await _dealRepository.GetByIdAsync(id);

            if (deal == null)
            {
                return NotFound();
            }

            // Проверка доступа
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            if (userRole != "Admin" && deal.AgentId != userId)
            {
                return Forbid();
            }

            // Валидация данных
            var property = await _propertyRepository.GetByIdAsync(request.PropertyId);
            if (property == null)
            {
                return BadRequest(new { message = "Property not found" });
            }

            var client = await _clientRepository.GetByIdAsync(request.ClientId);
            if (client == null)
            {
                return BadRequest(new { message = "Client not found" });
            }

            // Определяем агента для сделки
            int agentId;
            if (request.AgentId > 0)
            {
                var agent = await _userRepository.GetByIdAsync(request.AgentId);
                if (agent == null)
                {
                    return BadRequest(new { message = "Agent not found" });
                }

                // Проверяем права
                if (userRole != "Admin" && request.AgentId != userId)
                {
                    return Forbid();
                }

                agentId = request.AgentId;
            }
            else
            {
                agentId = userId;
            }

            // Сохраняем старый статус для проверки изменений
            var oldStatus = deal.Status;
            var oldPropertyId = deal.PropertyId;

            // Обновляем данные
            deal.PropertyId = request.PropertyId;
            deal.ClientId = request.ClientId;
            deal.DealAmount = request.DealAmount;
            deal.DealDate = request.DealDate;
            deal.Status = request.Status;
            deal.AgentId = agentId;

            await _dealRepository.UpdateAsync(deal);

            // Обновляем статус объектов недвижимости
            if (oldPropertyId != request.PropertyId)
            {
                // Освобождаем старый объект
                var oldProperty = await _propertyRepository.GetByIdAsync(oldPropertyId);
                if (oldProperty != null && oldStatus == "Завершено")
                {
                    oldProperty.IsAvailable = true;
                    await _propertyRepository.UpdateAsync(oldProperty);
                }

                // Блокируем новый объект, если сделка завершена
                if (request.Status == "Завершено")
                {
                    property.IsAvailable = false;
                    await _propertyRepository.UpdateAsync(property);
                }
            }
            else if (oldStatus != request.Status)
            {
                // Меняем статус доступности объекта
                property.IsAvailable = (request.Status != "Завершено");
                await _propertyRepository.UpdateAsync(property);
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteDeal(int id)
        {
            var deal = await _dealRepository.GetByIdAsync(id);

            if (deal == null)
            {
                return NotFound();
            }

            // Освобождаем объект недвижимости
            if (deal.Status == "Завершено")
            {
                var property = await _propertyRepository.GetByIdAsync(deal.PropertyId);
                if (property != null)
                {
                    property.IsAvailable = true;
                    await _propertyRepository.UpdateAsync(property);
                }
            }

            await _dealRepository.DeleteAsync(deal);
            return NoContent();
        }

        [HttpGet("statistics")]
        public async Task<ActionResult<object>> GetStatistics(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            // Проверяем роль пользователя
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            IEnumerable<Deal> deals;
            if (startDate.HasValue && endDate.HasValue)
            {
                deals = await _dealRepository.GetDealsByDateRangeAsync(startDate.Value, endDate.Value);
            }
            else
            {
                deals = await _dealRepository.GetAllAsync();
            }

            if (userRole != "Admin")
            {
                deals = deals.Where(d => d.AgentId == userId);
            }

            var statistics = new
            {
                TotalDeals = deals.Count(),
                CompletedDeals = deals.Count(d => d.Status == "Завершено"),
                PendingDeals = deals.Count(d => d.Status == "В ожидании"),
                CancelledDeals = deals.Count(d => d.Status == "Отменено"),
                TotalRevenue = deals.Where(d => d.Status == "Завершено").Sum(d => d.DealAmount),
                AverageDealAmount = deals.Any() ? deals.Average(d => d.DealAmount) : 0
            };

            return Ok(statistics);
        }

        [HttpGet("agent/{agentId}")]
        public async Task<ActionResult<IEnumerable<DealDto>>> GetDealsByAgent(int agentId)
        {
            // Проверка доступа
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            if (userRole != "Admin" && agentId != userId)
            {
                return Forbid();
            }

            var deals = await _dealRepository.GetDealsByAgentAsync(agentId);
            var dealDtos = deals.Select(d => MapToDto(d)).ToList();

            return Ok(dealDtos);
        }

        [HttpGet("status/{status}")]
        public async Task<ActionResult<IEnumerable<DealDto>>> GetDealsByStatus(string status)
        {
            var deals = await _dealRepository.GetDealsByStatusAsync(status);

            // Проверяем роль пользователя
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            if (userRole != "Admin")
            {
                deals = deals.Where(d => d.AgentId == userId);
            }

            var dealDtos = deals.Select(d => MapToDto(d)).ToList();
            return Ok(dealDtos);
        }

        private DealDto MapToDto(Deal deal)
        {
            return new DealDto
            {
                Id = deal.Id,
                PropertyId = deal.PropertyId,
                Property = deal.Property != null ? new PropertyDto
                {
                    Id = deal.Property.Id,
                    Title = deal.Property.Title,
                    Address = deal.Property.Address,
                    Price = deal.Property.Price,
                    Type = deal.Property.Type
                } : null,
                ClientId = deal.ClientId,
                Client = deal.Client != null ? new ClientDto
                {
                    Id = deal.Client.Id,
                    FirstName = deal.Client.FirstName,
                    LastName = deal.Client.LastName,
                    Email = deal.Client.Email,
                    Phone = deal.Client.Phone
                } : null,
                DealAmount = deal.DealAmount,
                DealDate = deal.DealDate,
                Status = deal.Status,
                AgentId = deal.AgentId,
                AgentName = deal.Agent != null ? deal.Agent.Username : null,
                CreatedAt = deal.CreatedAt
            };
        }
    }
}