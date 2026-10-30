using ECommerce.API.Data;
using ECommerce.API.DTOs;
using ECommerce.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Npgsql;
using System.ComponentModel.DataAnnotations;

namespace ECommerce.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContactController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ContactController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateContactDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))

                return BadRequest(new { message = "Your name is required." });

            if (string.IsNullOrWhiteSpace(dto.Email) ||
                !new EmailAddressAttribute().IsValid(dto.Email.Trim()))
                return BadRequest(new { message = "Please enter a valid email address." });

            if (string.IsNullOrWhiteSpace(dto.Message))
                return BadRequest(new { message = "Message is required." });

            if (dto.Name.Trim().Length > 100 ||
                dto.Email.Trim().Length > 150 ||
                (dto.Subject?.Trim().Length ?? 0) > 200 ||
                dto.Message.Trim().Length > 2000)
                return BadRequest(new { message = "One of the fields is too long." });

            var contact = new ContactMessage
            {
                Name = dto.Name.Trim(),
                Email = dto.Email.Trim(),
                Subject = dto.Subject?.Trim() ?? string.Empty,
                Message = dto.Message.Trim(),
                CreatedAt = DateTime.UtcNow,
            };

            _context.ContactMessages.Add(contact);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Message sent successfully." });
        }
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var message = await _context.ContactMessages
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return Ok(message);
        }
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var contact = await _context.ContactMessages.FindAsync(id);
            if (contact == null)

                return NotFound(new { message = "Message not found." });
            contact.IsRead = true;
            await _context.SaveChangesAsync();

            return NoContent();
        }
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var contact = await _context.ContactMessages.FindAsync(id);

            if (contact == null)

                return NotFound(new { message = "Message not found." });

            _context.ContactMessages.Remove(contact);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
