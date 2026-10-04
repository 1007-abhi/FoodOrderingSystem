using System.ComponentModel.DataAnnotations;

namespace FoodOrderingSystem.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        public string Password { get; set; }

        [StringLength(100)]
        public string FullName { get; set; }
        public string Address { get; set; }
        [Required]
        public string Phone { get; set; }
        public bool IsAdmin { get; set; } = false;
    }
}
