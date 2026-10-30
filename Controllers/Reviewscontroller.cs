using ECommerce.API.Data;
using ECommerce.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using System.Security.Claims;

namespace ECommerce.API.Controllers
{
    [Route("api/products/{productId}/reviews")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReviewsController(ApplicationDbContext context)
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

        [HttpGet]
        public async Task<IActionResult> GetAll(int productId)
        {
            var reviews = await _context.Reviews
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.UserId,
                    Username = r.User.FullName,
                    r.Rating,
                    r.Comment,
                    r.CreatedAt
                })
                .ToListAsync();

            var average = reviews.Count > 0 ? reviews.Average(r => r.Rating) : 0;

            return Ok(new { average , count = reviews.Count, reviews});
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult> Create (int productId, CreateReviewDto dto)
        {
            var userId = GetUserId();

            var productExist = await _context.Products.AnyAsync(p => p.Id == productId);
            
                if (!productExist)
                {
                    return NotFound(new { message = "Product not found." });
                }
            
            if (dto.Rating < 1 || dto.Rating > 5)
                return BadRequest(new { message = "Rating must be between 1 and 5." });
            
            if(dto.Comment != null && dto.Comment.Length > 1000)
            {
                return BadRequest(new { message = "Comment cannot exceed 1000 characters." });
            }


            var alreadyReviewed = await _context.Reviews
                .AnyAsync(r => r.ProductId == productId && r.UserId == userId);

            if (alreadyReviewed)
                return Conflict(new { message = "You have already reviewed this product." });

            _context.Reviews.Add(new Entities.Review
            {
                ProductId = productId,
                UserId = userId,
                Rating = dto.Rating,
                Comment = dto.Comment,
            });

            await _context.SaveChangesAsync();


            return Ok(new { message = "Review added." });
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete (int productId, int id)
        {
            var userId = GetUserId();

            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.Id == id && r.ProductId == productId);

            if(review == null)
            
                return NotFound(new { message = "Review not found." });

            if (review.UserId != userId && !User.IsInRole("Admin"))
                return Forbid();

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            return NoContent();
            
        }
    }
}
