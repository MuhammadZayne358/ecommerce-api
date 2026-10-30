using ECommerce.API.Data;
using ECommerce.API.DTOs;
using ECommerce.API.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ECommerce.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NewsletterController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public NewsletterController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Subscribe(SubscribeDto dto)
        {
            var email = dto.Email.Trim().ToLowerInvariant();

            if (!new EmailAddressAttribute().IsValid(email))
                return BadRequest(new { message = "Please enter valid email address." });

            var exists = await _context.NewsletterSubscribers
                .AnyAsync(s => s.Email == email);

            if (!exists)
            {
                _context.NewsletterSubscribers.Add(new NewsletterSubscriber { Email = email });
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Thank you for subscribing!" });
        }
    }
}
