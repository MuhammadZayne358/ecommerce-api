namespace ECommerce.API.DTOs;

public class CategoryUpdateDto
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }
}