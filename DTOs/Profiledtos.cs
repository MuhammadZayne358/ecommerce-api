namespace ECommerce.API.DTOs;

public class UpdateProfileDto
{
    public string FullName { get; set; } = string.Empty;
}

public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}