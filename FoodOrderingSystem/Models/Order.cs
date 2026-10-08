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

        // Order Status : Pending, Confirmed, Preparing, OutForDelivery, Delivered, Cancelled
        public string Status { get; set; } = "Pending";

        // COD or Razorpay
        public string PaymentMethod { get; set; } = "COD";

        // Pending, Paid, Failed, Refunded
        public string PaymentStatus { get; set; } = "Pending";

        // Razorpay Order ID
        public string? RazorpayOrderId { get; set; }

        // Razorpay Payment ID
        public string? RazorpayPaymentId { get; set; }

        // Razorpay Signature
        public string? RazorpaySignature { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; }
            = new List<OrderItem>();
    }
}