namespace ECommerce.API.DTOs;

public class ProductUpdateDto
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public int CategoryId { get; set; }

    public bool IsActive { get; set; }

    public IFormFile? Image { get; set; }

}