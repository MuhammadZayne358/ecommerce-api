namespace ECommerce.API.DTOs;

public class CartResponseDto
{
    public int CartId { get; set; }

    public List<CartItemResponseDto> Items { get; set; } = new();

    public int TotalItems { get; set; }

    public decimal ShippingFee { get; set; }

    public decimal SubTotal { get; set; }
}

public class CartItemResponseDto
{
    public int CartItemId { get; set; }

    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal TotalPrice { get; set; }
}