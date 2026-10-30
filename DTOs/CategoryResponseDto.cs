namespace ECommerce.API.DTOs
{
    public class CategoryResponseDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description {  get; set; } 

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public int ProductCount { get; set; }
    }
}
