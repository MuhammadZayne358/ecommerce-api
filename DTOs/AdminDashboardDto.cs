namespace ECommerce.API.DTOs;

public class AdminDashboardDto
{
    public int TotalProducts { get; set; }

    public int ActiveProducts { get; set; }

    public int TotalCategories { get; set; }

    public int TotalUsers { get; set; }

    public int TotalOrders { get; set; }

    public int PendingOrders { get; set; }

    public int ProcessingOrders { get; set; }

    public int ShippedOrders { get; set; }

    public int DeliveredOrders { get; set; }

    public int CancelledOrders { get; set; }

    public decimal TotalRevenue { get; set; }

    public List<RecentOrderDto> RecentOrders { get; set; } = new();
}

public class RecentOrderDto
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}