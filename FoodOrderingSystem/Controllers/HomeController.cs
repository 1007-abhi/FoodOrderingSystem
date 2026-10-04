using FoodOrderingSystem.data;
using FoodOrderingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace FoodOrderingSystem.Controllers
{
    public class HomeController(AppDbContext _context) : Controller
    {
        public IActionResult Index()
        {
            var categories = _context.Categories.ToList();
            ViewBag.Categories = categories;

            var featuredItems = _context.FoodItems
                .Where(f => f.IsAvailable)
                .Take(6)
                .ToList();

            return View(featuredItems);
        }


    }
}
