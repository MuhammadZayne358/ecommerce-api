using ECommerce.API.Data;
using ECommerce.API.DTOs;
using ECommerce.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.API.Controllers
{
    [Route("api/admin/orders")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminOrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AdminOrdersController(ApplicationDbContext context)
        {
            _context = context;
        }
        private static readonly Dictionary<string, string[]> AllowedTransitions = new()
        {
            ["Pending"] = new[] { "Confirmed", "Cancelled" },
            ["Confirmed"] = new[] { "Processing", "Cancelled" },
            ["Processing"] = new[] { "Shipped", "Cancelled" },
            ["Shipped"] = new[] { "Delivered" },
            ["Delivered"] = Array.Empty<string>(),
            ["Cancelled"] = Array.Empty<string>()
        };

        [HttpGet]

        public async Task<ActionResult<PagedResponseDto<OrderResponseDto>>> GetOrders(
            [FromQuery] OrderQueryDto query)
        {
            if (query.Page < 1) query.Page = 1;
            if (query.PageSize < 1) query.PageSize = 10;
            if (query.PageSize > 50) query.PageSize = 50;

            var orderQuery = _context.Orders
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                var status = query.Status.Trim();
                orderQuery = orderQuery.Where(o => o.Status == status);
            }

            var totalCount = await orderQuery.CountAsync();

            var orders = await orderQuery
                .Include(o => o.Items)
                .Include(o => o.User)
                .OrderByDescending(o => o.CreatedAt)
                .Skip((query.Page -1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(
                totalCount / (double)query.PageSize);

            var response = new PagedResponseDto<OrderResponseDto>
            {
                Data = orders.Select(MapOrder).ToList(),
                TotalPages = totalPages,
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize,
                HasPreviousPage = query.Page > 1,
                HasNextPage = query.Page < totalPages,
            };

            return Ok(response);
        }


        private static OrderResponseDto MapOrder(
            Entities.Order order)
        {
            return new OrderResponseDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                SubTotal = order.SubTotal,
                ShippingFee = order.ShippingFee,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                PaymentStatus = order.PaymentStatus,
                PaymentMethod = order.PaymentMethod,
                ShippingAddress = order.ShippingAddress,
                ShippingCity = order.ShippingCity,
                ShippingFullName = order.ShippingFullName,
                ShippingPhone = order.ShippingPhone,
                ShippingPostalCode = order.ShippingPostalCode,
                UserName = order.User?.FullName ?? "",
                UserEmail = order.User?.Email ?? "",
                CreatedAt = order.CreatedAt,

                Items = order.Items.Select(item =>
                new OrderItemResponseDto
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    ImageUrl = item.ImageUrl,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    TotalPrice = item.TotalPrice,
                }).ToList()
            };
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<OrderResponseDto>> GetOrder(int id)
        {
            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound(new
                {
                    message = "Order not found."
                });
            }
            return Ok(MapOrder(order));
        }

        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, UpdateOrderStatusDto dto)
        {
            var newStatus = dto.Status?.Trim() ?? "";

            if (!AllowedTransitions.ContainsKey(newStatus))
            {
                return BadRequest(new
                {
                    message = "Invalid order status.",
                    allowedStatus = AllowedTransitions.Keys
                });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound(new { message = "Order not found." });

            if (!AllowedTransitions[order.Status].Contains(newStatus))
            {
                return BadRequest(new
                {
                    message = $"Cannot change order from {order.Status} to {newStatus}."
                });
            }

            if (newStatus == "Cancelled")
            {
                var productIds = order.Items.Select(i => i.ProductId).ToList();

                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToListAsync();

                foreach (var item in order.Items)
                {
                    var product = products.FirstOrDefault(p => p.Id == item.ProductId);
                    if (product != null)
                        product.StockQuantity += item.Quantity;
                }
                if (order.PaymentStatus == "Paid")
                    order.PaymentStatus = "Refunded";
            }

            order.Status = newStatus;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return NoContent();
        }


        [HttpPut("{id:int}/payment-status")]
        public async Task<IActionResult> UpdatePaymentStatus(
       int id,
       UpdatePaymentStatusDto dto)
        {
            var newStatus = dto.PaymentStatus?.Trim() ?? "";

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id);

            if(order == null)
            {
                return NotFound(new { message = "Order not found." });
            }

            if(order.PaymentMethod != "COD")
            {
                return BadRequest(new
                {
                    message = "Only COD order payments can be updated manually."
                });
            }

            if(order.Status == "Cancelled")
            {
                return BadRequest(new
                {
                    message = "Cannot update payment of a cancelled order."
                });
            }
            if (order.Status != "Delivered")
            {
                return BadRequest(new
                {
                    message = "COD payment can only be marked as paid after delivery."
                });
            }

            if (newStatus != "Paid")
            {
                return BadRequest(new
                {
                    message = "Payment can only be marked ad Paid."
                });
            } 
            if(order.PaymentStatus == "Paid")
            {
                return BadRequest(new
                {
                    message = "Order is already paid."
                });
            }
            order.PaymentStatus = "Paid";
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return NoContent();
        }

    }

}

