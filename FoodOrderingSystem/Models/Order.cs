using System.ComponentModel.DataAnnotations;

namespace FoodOrderingSystem.Models
{
    public class Order
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.Now;
        [Required]
        public string DeliveryAddress { get; set; }
        public string PhoneNumber { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Confirmed, Delivered, Cancelled
        public ICollection<OrderItem> OrderItems { get; set; }
    }
}
