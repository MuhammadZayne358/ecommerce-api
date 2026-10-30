using ECommerce.API.Data;
using ECommerce.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.API.Controllers
{
    [Route("api/admin/dashboard")]
    [Authorize(Roles = "Admin")]
    [ApiController]
    public class AdminDashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AdminDashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<AdminDashboardDto>> GetDashboard()
        {
            var dashboard = new AdminDashboardDto
            {
                TotalProducts =
                await _context.Products.CountAsync(p => p.IsActive),

                TotalCategories =
                await _context.Categories.CountAsync(),

                TotalUsers =
                await _context.Users.CountAsync(),

                TotalOrders =
                await _context.Orders.CountAsync(),

                PendingOrders = 
                await _context.Orders.CountAsync(
                    o => o.Status == "Pending"),

                ProcessingOrders =
                await _context.Orders.CountAsync(
                    o => o.Status == "Processing"),

                ShippedOrders = 
                await _context.Orders.CountAsync(
                    o => o.Status == "Shipped"),

                DeliveredOrders = 
                await _context.Orders.CountAsync(
                    o => o.Status == "Delivered"),

                CancelledOrders =
                await _context.Orders.CountAsync(
                    o => o.Status == "Cancelled"),

                TotalRevenue = await _context.Orders.Where(
                    o => o.Status != "Cancelled")
                .SumAsync(o => o.TotalAmount)
            };

            dashboard.RecentOrders =
                await _context.Orders
                .AsNoTracking()
                .Include(o => o.User)
                .OrderByDescending(o => o.CreatedAt)
                .Take(10)
                .Select(o => new RecentOrderDto
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    CustomerName = o.User.FullName,
                    TotalAmount = o.TotalAmount,
                    Status = o.Status,
                    CreatedAt = o.CreatedAt,
                })
                .ToListAsync();

            return Ok(dashboard);

        }
    }
}
