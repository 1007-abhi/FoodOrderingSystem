using System.ComponentModel.DataAnnotations;

namespace FoodOrderingSystem.Models
{
    public class Category
    {

        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        public string Description { get; set; }
        public ICollection<FoodItem> FoodItems { get; set; }
    }
}
