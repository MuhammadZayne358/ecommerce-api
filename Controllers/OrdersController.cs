using ECommerce.API.Data;
using ECommerce.API.DTOs;
using ECommerce.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ECommerce.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if(!int.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException(
                    "Invalid user token.");
            }
            return userId;
        }

        [HttpPost("checkout")]
        public async Task<ActionResult<OrderResponseDto>> Checkout(
            CheckoutDto dto)
        {
            var userId = GetUserId();

            if(string.IsNullOrWhiteSpace(dto.ShippingFullName) ||
               string.IsNullOrWhiteSpace(dto.ShippingPhone) ||
               string.IsNullOrWhiteSpace(dto.ShippingCity) ||
               string.IsNullOrWhiteSpace(dto.ShippingPostalCode) ||
               string.IsNullOrWhiteSpace(dto.ShippingAddress))
            {
                return BadRequest(new { message = "All shipping fields are required." });
            }

            var paymentMethod = (dto.PaymentMethod ?? "COD").Trim().ToUpper();
            if (paymentMethod != "COD" && paymentMethod != "CARD")
            {
                return BadRequest(new { message = "Invalid payment method." });
            }
            var paymentStatus = "Pending";

            if(paymentMethod == "CARD")
            {
                var cardNumber = (dto.CardNumber ?? "").Replace(" ", "");
                var cardCv = (dto.CardCvc ?? "").Trim();

                if (string.IsNullOrWhiteSpace(dto.CardExpiry) || cardCv.Length < 3)
                {
                    return BadRequest(new { message = "Please enter valid card details." });
                }

                if(cardNumber == "4242424242424242")
                {
                    paymentStatus = "Paid";
                }
                else if (cardNumber == "4000000000000002")
                {
                    return BadRequest(new { message = "Payment declined. Please try another card." });
                }
                else
                {
                    return BadRequest(new { message = "Invalid test card. Use 4242 4242 4242 4242." });
                }
            }
             await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .ThenInclude(c => c.Product)
                    .ThenInclude(c => c.Category)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if(cart == null || !cart.Items.Any())
                {
                    return BadRequest(new { message = "Cart is empty." });
                }
                foreach (var items in cart.Items)
                {
                    if(items.Product == null)
                    {
                        return BadRequest(new { message = "One or more items in your cart are no longer available." });
                    }
                    if (!items.Product.IsActive || items.Product.IsDeleted || !items.Product.Category.IsActive)
                    {
                        return BadRequest(new { message = $"Product {items.Product.Name} is no longer available." });
                    }
                    if(items.Quantity > items.Product.StockQuantity)
                    {
                        return BadRequest(new { message = $"Only {items.Product.StockQuantity} units of {items.Product.Name} are available" });
                    }
                }
                const decimal shippingFee = 10m;

                var subTotal = cart.Items.Sum(item => item.Product.Price * item.Quantity);

                var totalAmount = subTotal + shippingFee;

                var order = new Order
                {
                    UserId = userId,
                    OrderNumber = GenerateOrderNumber(),
                    SubTotal = subTotal,
                    TotalAmount = totalAmount,
                    ShippingFee = shippingFee,
                    Status = "Pending",
                    PaymentStatus = paymentStatus,
                    PaymentMethod = paymentMethod == "CARD" ? "Card" : "COD",
                    ShippingFullName = dto.ShippingFullName.Trim(),
                    ShippingAddress = dto.ShippingAddress.Trim(),
                    ShippingCity = dto.ShippingCity.Trim(),
                    ShippingPhone = dto.ShippingPhone.Trim(),
                    ShippingPostalCode = dto.ShippingPostalCode.Trim(),
                };
                foreach (var carItems in cart.Items)
                {
                    var product = carItems.Product;

                    product.StockQuantity -= carItems.Quantity;

                    order.Items.Add(new OrderItem
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        ImageUrl = product.ImageUrl,
                        UnitPrice = product.Price,
                        Quantity = carItems.Quantity,
                        TotalPrice = product.Price * carItems.Quantity
                    });
                }
                _context.Orders.Add(order);
                _context.CartItems.RemoveRange(cart.Items);
                cart.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

               await transaction.CommitAsync();

                return Ok(MapOrder(order));
            }
            catch(Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new
                {
                    message = ex.Message,
                    innerException = ex.InnerException?.Message
                });
            }
            
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetMyOrder()
        {
            var userId = GetUserId();

            var orders = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return Ok(orders.Select(MapOrder));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<OrderResponseDto>> GetOrder(int id)
        {
            var userId = GetUserId();
            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => 
                o.Id == id && o.UserId == userId);

            if(order == null)
            {
                return NotFound(new
                {
                    message = "Order not found."
                });
            }
            return Ok(MapOrder(order));
        }

        [HttpPost("{id:int}/cancel")]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var userId = GetUserId();

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);
            if(order == null)
            {
                return NotFound(new
                {
                    message = "Order not found."
                });
            }
            if(order.Status != "Pending")
            {
                return BadRequest(new
                {
                    message = "Only pending orders can be cancelled."
                });
            }
            var productIds = order.Items.Select(i => i.ProductId).ToList();

            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            foreach(var item in order.Items)
            {
                var product = products.FirstOrDefault(p => p.Id == item.ProductId);
                if (product != null)
                    product.StockQuantity += item.Quantity;
            }
            if (order.PaymentStatus == "Paid")
                order.PaymentStatus = "Refunded";
            order.Status = "Cancelled";
            order.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return NoContent();



        }
       

        
        private static string GenerateOrderNumber()
        {
            return $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        }

        private static OrderResponseDto MapOrder(Order order)
        {
            return new OrderResponseDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                SubTotal = order.SubTotal,
                TotalAmount = order.TotalAmount,
                ShippingFee = order.ShippingFee,
                Status = order.Status,
                PaymentStatus = order.PaymentStatus,
                PaymentMethod = order.PaymentMethod,
                ShippingFullName = order.ShippingFullName,
                ShippingAddress = order.ShippingAddress,
                ShippingCity = order.ShippingCity,
                ShippingPhone = order.ShippingPhone,
                ShippingPostalCode = order.ShippingPostalCode,
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

        
    }
}
