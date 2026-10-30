using ECommerce.API.Data;
using ECommerce.API.DTOs;
using ECommerce.API.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ECommerce.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AddressesController : ControllerBase
    {
        private const int MaxAddressesPerUser = 5;

        private readonly ApplicationDbContext _context;

        public AddressesController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid user token.");
            }
            return userId;
        }

        private static AddressResponseDto ToDto(UserAddress a) => new()
        {
            Id = a.Id,
            Label = a.Label,
            FullName = a.FullName,
            Phone = a.Phone,
            AddressLine = a.AddressLine,
            City = a.City,
            PostalCode = a.PostalCode,
            IsDefault = a.IsDefault,
        };

        [HttpGet]
        public async Task<ActionResult<List<AddressResponseDto>>> GetMyAddresses()
        {
            var userId = GetUserId();

            var addresses = await _context.UserAddresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedAt)
                .ToListAsync(); 

            return Ok (addresses.Select(ToDto).ToList());
        }

        [HttpPost]
        public async Task<ActionResult<AddressResponseDto>> CreateAddress(SaveAddressDto dto)
        {
            var userId = GetUserId();

            if(string.IsNullOrWhiteSpace(dto.FullName) ||
                string.IsNullOrWhiteSpace(dto.Phone) ||
                string.IsNullOrWhiteSpace(dto.City) ||
                string.IsNullOrWhiteSpace(dto.PostalCode) ||
                string.IsNullOrWhiteSpace(dto.AddressLine))
            {
                return BadRequest(new { message = "All address fields are required." });
            }

            var existing = await _context.UserAddresses
                .Where(a => a.UserId == userId)
                .ToListAsync();

            if(existing.Count >= MaxAddressesPerUser)
            {
                return BadRequest(new
                {
                    message = $"You can save up to {MaxAddressesPerUser} addresses. Please delete one first."
                });
            }

            var MakeDefault = dto.IsDefault || existing.Count == 0;

            if(MakeDefault)
            {
                foreach( var old in existing)
                {
                    old.IsDefault = false;
                }
            }
            var label = string.IsNullOrWhiteSpace(dto.Label) ? "Home" : dto.Label.Trim();
            if (label.Length > 30)
            {
                label = label[..30];
            }
            var address = new UserAddress
            {
                UserId = userId,
                FullName = dto.FullName.Trim(),
                Label = label,
                AddressLine = dto.AddressLine.Trim(),
                City = dto.City.Trim(),
                PostalCode = dto.PostalCode.Trim(),
                IsDefault = MakeDefault
            };
            _context.UserAddresses.Add(address);
            await _context.SaveChangesAsync();

            return Ok(ToDto(address));
        }

        [HttpPut("{id}/default")]
        public async Task<IActionResult> SetDefault (int id)
        {
            var userId = GetUserId();

            var address = await _context.UserAddresses
                .Where(a => a.UserId == userId)
                .ToListAsync();

            var target = address.FirstOrDefault(a => a.Id == id);

            if(target == null)
            {
                return NotFound(new { message = "Address not found." });
            }
            foreach (var a in address)
            {
                a.IsDefault = a.Id == id;
            }
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAddress(int id)
        {
            var userId = GetUserId();

            var address = await _context.UserAddresses
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if(address == null)
            {
                return NotFound(new { message = "Address not found." });
            }

            var wasDefault = address.IsDefault;

            _context.UserAddresses.Remove(address);
            await _context.SaveChangesAsync();

            if (wasDefault)
            {
                var next = await _context.UserAddresses
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.CreatedAt)
                    .FirstOrDefaultAsync();

                if(next != null)
                {
                    next.IsDefault = true;
                    await _context.SaveChangesAsync();
                }
            }
            return NoContent();

        }
    }
}
