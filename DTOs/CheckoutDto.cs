namespace ECommerce.API.DTOs;

public class CheckoutDto
{
    public string ShippingFullName { get; set; } = string.Empty;

    public string ShippingPhone { get; set; } = string.Empty;

    public string ShippingAddress { get; set; } = string.Empty;

    public string ShippingCity { get; set; } = string.Empty;

    public string ShippingPostalCode { get; set; } = string.Empty;

    public string PaymentMethod { get; set; } = "COD";

    public string? CardNumber { get; set; }

    public string? CardExpiry { get; set; }

    public string? CardCvc { get; set; }
}