namespace ECommerce.API.Entities
{
    public class User
    {
        public int Id { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; }  = string.Empty;

        public string PasswordHash {  get; set; } = string.Empty;

        public string Role { get; set; } = "Customer";

        public bool IsActive { get; set; } = true;

        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetExpiry { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Order> Orders { get; set; } = new List<Order>();

        public ICollection<UserAddress> Addresses { get; set; } = new List<UserAddress>();
    }
}
