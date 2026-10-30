using System.ComponentModel.DataAnnotations;

namespace ECommerce.API.DTOs;

public class LoginDto
{
    [Required]
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}