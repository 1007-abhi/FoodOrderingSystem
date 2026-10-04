using FoodOrderingSystem.data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodOrderingSystem.Controllers
{
    public class MenuController(AppDbContext _context) : Controller
    {
        public IActionResult Index(int? categoryId)
        {
            // Get categories for the filter sidebar
            var categories = _context.Categories.ToList();
            ViewBag.Categories = categories;

            // Fetch items with optional category filtering
            var foodItems = _context.FoodItems
                .Include(f => f.Category)
                .Where(f => f.IsAvailable && (categoryId == null || f.CategoryId == categoryId))
                .ToList();

            return View(foodItems);
        }
    }
}
