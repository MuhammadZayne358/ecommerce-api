using ECommerce.API.Data;
using ECommerce.API.DTOs;
using ECommerce.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.EntityFrameworkCore;


namespace ECommerce.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CategoryResponseDto>>> GetCategories()
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .Select(c => new CategoryResponseDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    CreatedAt = c.CreatedAt,
                    ProductCount = c.Products.Count(p => p.IsActive),
                })
                .OrderBy(c => c.Name)
                .ToListAsync();
            return Ok(categories);
        }
        [Authorize(Roles = "Admin")]
        [HttpGet("admin")]
        public async Task<ActionResult<IEnumerable<CategoryResponseDto>>> GetAdminCategories()
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .Select(c => new CategoryResponseDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    CreatedAt = c.CreatedAt,
                    ProductCount = c.Products.Count(p => p.IsActive),
                })
                .OrderBy(c => c.Name)
                .ToListAsync();
            return Ok(categories);
        }
        [HttpGet("{id:int}")]
        public async Task<ActionResult<CategoryResponseDto>> GetCategory(int id)
        {
            var category = await _context.Categories.AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new CategoryResponseDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    CreatedAt = c.CreatedAt,
                    ProductCount = c.Products.Count(),
                })
                .FirstOrDefaultAsync();

            if (category == null)

                return NotFound(new { message = "Category not found." });
            return Ok(category);

        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<CategoryResponseDto>> CreateCategory(
            CategoryCreateDto dto)
        {
            if (string.IsNullOrEmpty(dto.Name))
            {
                return BadRequest(new { message = "Category is not exist" });
            }

            var nameExist = await _context.Categories
                .AnyAsync(c => c.Name.ToLower() == dto.Name.Trim().ToLower());
            if (nameExist)
            {
                return Conflict(new { message = "Category with this name already exist." });
            }
            var category = new Category
            {
                Name = dto.Name,
                Description = dto.Description,
            };
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            var response = new CategoryResponseDto
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                CreatedAt = category.CreatedAt,
                IsActive = category.IsActive,
                ProductCount = 0
            };

            return CreatedAtAction(
                nameof(GetCategory),
                new { id = category.Id },
                response);

        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateCategory(
            int id,
            CategoryUpdateDto dto)
        {
            var category = await _context.Categories.FirstOrDefaultAsync(
                c => c.Id == id);
            if (category == null)
            {
                return NotFound(new { message = "Category not found." });

            }
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest(new { message = "Category name is required." });
            }
            var duplicateCheck = await _context.Categories.AnyAsync(
                c => c.Id != id && c.Name.ToLower() == dto.Name.Trim().ToLower());

            if (duplicateCheck)
            {
                return Conflict(new { message = "Categroy with this name already exist." });
            }
            category.Name = dto.Name.Trim();
            category.Description = dto.Description?.Trim();
            category.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                return NotFound(new { message = "Category not found." });
            }
            if (category.Products.Any())
            {

                return BadRequest(new { message = "This Category contains products. Remove or move products before deleting." });
            }
            _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}