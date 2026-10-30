using ECommerce.API.Data;
using ECommerce.API.DTOs;
using ECommerce.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop.Infrastructure;
using System.Security.Claims;

namespace ECommerce.API.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out var userId))
            {

                throw new UnauthorizedAccessException("Invalid user token");
            }
            return userId;
        }

        private async Task<Cart?> GetUserCart(int userId)
        {
            return await _context.Carts
                .Include(x => x.Items)
                .ThenInclude(x => x.Product)
                .FirstOrDefaultAsync(x => x.UserId == userId);
        }



        [HttpGet]
        public async Task<ActionResult<CartResponseDto>> GetCart()
        {
            var userId = GetUserId();
            var cart = await GetUserCart(userId);

            if (cart == null)
            {
                return Ok(new CartResponseDto());
            }

            var response = new CartResponseDto
            {
                CartId = cart.Id,
                ShippingFee = 10,
                Items = cart.Items.Select(item => new CartItemResponseDto
                {
                    CartItemId = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.Product.Name,
                    ImageUrl = item.Product.ImageUrl,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    TotalPrice = item.UnitPrice * item.Quantity,

                }).ToList(),
            };
            response.TotalItems = response.Items.Sum(x => x.Quantity);
            response.SubTotal = response.Items.Sum(x => x.TotalPrice);

            return Ok(response);
        }

        [HttpPost("items")]
        public async Task<ActionResult<CartResponseDto>> AddToCart( AddToCartDto dto)
        {
            var userId = GetUserId();
            if(dto.Quantity <= 0)
            {
                return BadRequest(new { message = "Quantity must be greater then zero." });
            }
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.IsActive);

            if(product == null)
            {
                return NotFound(new { message = "Product not found." });
            }
            if(product.StockQuantity <= 0)
            {
                return BadRequest(new { message = "Product is out of stock." });
            }
            var cart = await GetUserCart(userId);
            if(cart == null)
            {
                cart = new Cart
                {
                    UserId = userId,

                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            var existingCart = cart.Items
                .FirstOrDefault(c =>  c.ProductId == dto.ProductId);
            if(existingCart != null)
            {
                var newQuantity = existingCart.Quantity + dto.Quantity;
                if(newQuantity > product.StockQuantity)
                {
                    return BadRequest(new { message = $"Only {product.StockQuantity} items are available" });
                }
                existingCart.Quantity = newQuantity;
                existingCart.UnitPrice = product.Price;
            }
            else
            {
                  if(dto.Quantity > product.StockQuantity)
                {
                    return BadRequest(new { message = $"Only {product.StockQuantity} items are available" });
                }
                cart.Items.Add(new CartItem
                {
                    ProductId = dto.ProductId,
                    Quantity = dto.Quantity,
                    UnitPrice = product.Price,
                });
            }
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return await GetCart();
        }

        
        [HttpPut("items/{cartItemId:int}")]
        public async Task<ActionResult<CartResponseDto>> UpdateCartItems(
            int cartItemId,
            UpdateCartItemDto dto)
        {
            var userId = GetUserId();
            if (dto.Quantity <= 0)
            {
                return BadRequest(new { message = "Quantity must be greater then zero." });
            }
            var cartItem = await _context.CartItems.
                Include(c => c.Cart)
                .Include(c => c.Product)
                .FirstOrDefaultAsync(c => c.Id == cartItemId && c.Cart.UserId == userId);
            if (cartItem == null)
            {
                return NotFound(new {message = "Cart item not found"});
            }
            if(dto.Quantity > cartItem.Product.StockQuantity)
            {
                return BadRequest(new { message = $"Only {cartItem.Product.StockQuantity} items are availaible." });
            }
            cartItem.Quantity = dto.Quantity;
            cartItem.UnitPrice = cartItem.Product.Price;

            cartItem.Cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return await GetCart();    
        }

       

        [HttpDelete("items/{cartItemId:int}")]
        public async Task<ActionResult<CartResponseDto>> RemoveCartItem(
            int cartItemId)
        {
            var userId = GetUserId();

            var cartItem = await _context.CartItems.
                Include(c => c.Cart)
                .FirstOrDefaultAsync(c => c.Id == cartItemId && c.Cart.UserId == userId);

            if (cartItem == null)
            {
                return NotFound(new { message = "Cart item not found." });
            }
            cartItem.Cart.UpdatedAt = DateTime.UtcNow;
            _context.CartItems.Remove(cartItem);
            await _context.SaveChangesAsync();
            return await GetCart();
        }

        [HttpDelete]
        public async Task<IActionResult> ClearCart()
        {
            var userId = GetUserId();

            var cart = await _context.Carts.
                Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                return NoContent();
            }
            _context.CartItems.RemoveRange(cart.Items);
            await _context.SaveChangesAsync();
            return NoContent();
        }


}        
}
