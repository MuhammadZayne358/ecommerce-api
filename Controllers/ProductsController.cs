using ECommerce.API.Data;
using ECommerce.API.DTOs;
using ECommerce.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Quic;
using System.Runtime.InteropServices;

namespace ECommerce.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;

        }

        [HttpGet]
        public async Task<ActionResult<PagedResponseDto<ProductResponseDto>>> GetProducts(
            [FromQuery] ProductQueryDto query)
        {
            if (query.Page < 1)
                query.Page = 1;

            if (query.PageSize < 1)
                query.PageSize = 10;

            if (query.PageSize > 50)
                query.PageSize = 50;

            var productsQuery = _context.Products
                .Where(p => p.IsActive && !p.IsDeleted && p.Category.IsActive)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();

                productsQuery = productsQuery.Where(p =>
                p.Name.Contains(search) ||
                (p.Description != null && p.Description.Contains(search)));
            }

            if (query.CategoryId.HasValue)
            {
                productsQuery = productsQuery.Where(p =>
                p.CategoryId == query.CategoryId.Value);
            }

            if (query.MinPrice.HasValue)
            {
                productsQuery = productsQuery.Where(p =>
                p.Price >= query.MinPrice.Value);
            }

            if (query.MaxPrice.HasValue)
            {
                productsQuery = productsQuery.Where(p =>
                p.Price <= query.MaxPrice.Value);
            }
            var totalCount = await productsQuery.CountAsync();

            var products = await productsQuery
                .OrderByDescending(p => p.CreatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(p => new ProductResponseDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    StockQuantity = p.StockQuantity,
                    ImageUrl = p.ImageUrl,
                    IsActive = p.IsActive,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category.Name,
                    CreatedAt = p.CreatedAt,
                    AverageRating = _context.Reviews.Where(r => r.ProductId == p.Id).Average(r => (double?)r.Rating) ?? 0,
                    ReviewCount = _context.Reviews.Count(r => r.ProductId == p.Id),

                   
                })
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(
                totalCount / (double)query.PageSize);

            var response = new PagedResponseDto<ProductResponseDto>
            {
                Data = products,
                TotalPages = totalPages,
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize,
                HasPreviousPage = query.Page > 1,
                HasNextPage = query.Page < totalPages,
            };
            return Ok(response);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ProductResponseDto>> GetProduct(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == id && p.IsActive && !p.IsDeleted && p.Category.IsActive )
                .Select(p => new ProductResponseDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    Description = p.Description,
                    StockQuantity = p.StockQuantity,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category.Name,
                    ImageUrl = p.ImageUrl,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt,
                    AverageRating = _context.Reviews.Where(r => r.ProductId == p.Id).Average(r => (double?)r.Rating) ?? 0,
                    ReviewCount = _context.Reviews.Count(r => r.ProductId == p.Id),

                })
                .FirstOrDefaultAsync();
            if (product == null)
                return NotFound(new { message = "Product not found." });

            return Ok(product);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("admin/{id:int}")]
        public async Task<ActionResult<ProductResponseDto>> GetAdminProductById(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == id && !p.IsDeleted)
                .Select(p => new ProductResponseDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    StockQuantity = p.StockQuantity,
                    ImageUrl = p.ImageUrl,
                    IsActive = p.IsActive,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category.Name,
                    CreatedAt = p.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return NotFound(new
                {
                    message = "Product not found."
                });
            }

            return Ok(product);
        }

        private async Task<string?> SaveImageAsync(IFormFile? image)
        {
            if (image == null || image.Length == 0)
                return null;

            var allowedExtension = new[]
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp"
            };

            var extension = Path.GetExtension(image.FileName)
                .ToLowerInvariant();

            if (!allowedExtension.Contains(extension))
                throw new InvalidOperationException("Invalid image format.");

            if (image.Length > 5 * 1024 * 1024)
                throw new InvalidOperationException(
                    "Image size cannot exceed 5 MB.");

            var uploadFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "images",
                "products");

            Directory.CreateDirectory(uploadFolder);

            var filename = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadFolder, filename);

            await using var stream = new FileStream(
                filePath,
                FileMode.Create);

            await image.CopyToAsync(stream);

            return $"/images/products/{filename}";
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ProductResponseDto>> CreateProduct(
            [FromForm] ProductCreateDto dto)
        {
            var category = await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId);
            if (!category)
                return BadRequest(new { message = "Invalid Category" });

            var product = new Product
            {
                Name = dto.Name,
                Description = dto.Description,
                StockQuantity = dto.StockQuantity,
                Price = dto.Price,
                CategoryId = dto.CategoryId,
                ImageUrl = await SaveImageAsync(dto.Image)
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            var response = new ProductResponseDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                StockQuantity = product.StockQuantity,
                Price = product.Price,
                ImageUrl = product.ImageUrl,
                CategoryId = product.CategoryId,
                IsActive = product.IsActive,
                CreatedAt = product.CreatedAt
            };
            return CreatedAtAction(
                nameof(GetProduct),
                new { id = product.Id },
                response);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id:int}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateProduct(
            int id,
            [FromForm] ProductUpdateDto dto)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (product == null)
                return NotFound(new { message = "Product not found" });
            
            var category = await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId);
            if (!category)
                return BadRequest(new { message = "Invalid Category." });

            if(dto.Image != null)
            {
                product.ImageUrl = await SaveImageAsync(dto.Image);
            }

            product.Name = dto.Name;
            product.Description = dto.Description;
            product.Price = dto.Price;
            product.StockQuantity = dto.StockQuantity;
            product.CategoryId = dto.CategoryId;
            product.IsActive = dto.IsActive;
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = _context.Products.FirstOrDefault(p => p.Id == id);
            if (product == null) return NotFound(new { message = "Product not found." });
            product.IsDeleted = true;
            product.IsActive = false;
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("admin")]
        public async Task<ActionResult<PagedResponseDto<ProductResponseDto>>> GetAdminProducts(
    [FromQuery] ProductQueryDto query)
        {
            if (query.Page < 1)
                query.Page = 1;

            if (query.PageSize < 1)
                query.PageSize = 10;

            if (query.PageSize > 50)
                query.PageSize = 50;

            var productsQuery = _context.Products
                .Where(p => !p.IsDeleted)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();

                productsQuery = productsQuery.Where(p =>
                    p.Name.Contains(search) ||
                    (p.Description != null && p.Description.Contains(search)));
            }

            if (query.CategoryId.HasValue)
            {
                productsQuery = productsQuery.Where(
                    p => p.CategoryId == query.CategoryId.Value);
            }

            if (query.MinPrice.HasValue)
            {
                productsQuery = productsQuery.Where(
                    p => p.Price >= query.MinPrice.Value);
            }

            if (query.MaxPrice.HasValue)
            {
                productsQuery = productsQuery.Where(
                    p => p.Price <= query.MaxPrice.Value);
            }

            var totalCount = await productsQuery.CountAsync();

            var products = await productsQuery
                .OrderByDescending(p => p.CreatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(p => new ProductResponseDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    StockQuantity = p.StockQuantity,
                    ImageUrl = p.ImageUrl,
                    IsActive = p.IsActive,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category.Name,
                    CategoryIsActive = p.Category.IsActive,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(
                totalCount / (double)query.PageSize);

            var response = new PagedResponseDto<ProductResponseDto>
            {
                Data = products,
                TotalPages = totalPages,
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize,
                HasPreviousPage = query.Page > 1,
                HasNextPage = query.Page < totalPages
            };

            return Ok(response);
        }




    }
}