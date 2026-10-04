using FoodOrderingSystem.data;
using FoodOrderingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodOrderingSystem.Controllers
{
    public class AdminController(AppDbContext _context) : Controller
    {

        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("IsAdmin") == "True";
        }

        public IActionResult Dashboard()
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            ViewBag.TotalOrders = _context.Orders.Count();
            ViewBag.TotalUsers = _context.Users.Count();
            ViewBag.TotalItems = _context.FoodItems.Count();
            return View();
        }

        public IActionResult ManageMenu(int? categoryId, string searchString, bool? isAvailable, decimal? minPrice, decimal? maxPrice, string sortOrder)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            // Store sort order for view
            ViewBag.CurrentSort = sortOrder;
            ViewBag.NameSortParam = sortOrder == "name_desc" ? "name" : "name_desc";
            ViewBag.PriceSortParam = sortOrder == "price_asc" ? "price_desc" : "price_asc";
            ViewBag.CategorySortParam = sortOrder == "category" ? "category_desc" : "category";

            var items = _context.FoodItems
                .Include(f => f.Category)
                .AsQueryable();

            // Filter by Category
            if (categoryId.HasValue && categoryId > 0)
            {
                items = items.Where(f => f.CategoryId == categoryId);
                ViewBag.CurrentCategory = categoryId;
            }

            // Filter by Search String (Name or Description)
            if (!string.IsNullOrEmpty(searchString))
            {
                items = items.Where(f =>
                    f.Name.Contains(searchString) ||
                    f.Description.Contains(searchString));
                ViewBag.CurrentSearch = searchString;
            }

            // Filter by Availability
            if (isAvailable.HasValue)
            {
                items = items.Where(f => f.IsAvailable == isAvailable.Value);
                ViewBag.CurrentAvailability = isAvailable.Value;
            }

            // Filter by Price Range
            if (minPrice.HasValue)
            {
                items = items.Where(f => f.Price >= minPrice.Value);
                ViewBag.MinPrice = minPrice.Value;
            }
            if (maxPrice.HasValue)
            {
                items = items.Where(f => f.Price <= maxPrice.Value);
                ViewBag.MaxPrice = maxPrice.Value;
            }

            // Sorting
            items = sortOrder switch
            {
                "name_asc" => items.OrderBy(f => f.Name),
                "name_desc" => items.OrderByDescending(f => f.Name),
                "price_asc" => items.OrderBy(f => f.Price),
                "price_desc" => items.OrderByDescending(f => f.Price),
                "category" => items.OrderBy(f => f.Category.Name),
                "category_desc" => items.OrderByDescending(f => f.Category.Name),
                _ => items.OrderBy(f => f.CategoryId).ThenBy(f => f.Name)
            };

            // Stats for dashboard
            ViewBag.TotalItems = _context.FoodItems.Count();
            ViewBag.AvailableItems = _context.FoodItems.Count(f => f.IsAvailable);
            ViewBag.UnavailableItems = _context.FoodItems.Count(f => !f.IsAvailable);
            ViewBag.CategoryCount = _context.Categories.Count();

            ViewBag.Categories = _context.Categories.ToList();

            return View(items.ToList());
        }

        [HttpPost]
        public IActionResult ToggleAvailability(int id)
        {
            // Security check: Only Admins can toggle availability
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            var item = _context.FoodItems.Find(id);
            if (item == null) return NotFound();

            // Logic to flip the availability status
            item.IsAvailable = !item.IsAvailable;
            _context.SaveChanges();

            // Set notification message for the user
            TempData["Success"] = $"{item.Name} is now {(item.IsAvailable ? "available" : "unavailable")}";

            return RedirectToAction("ManageMenu");
        }
        
        [HttpGet]
        public IActionResult AddFoodItem()
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            ViewBag.Categories = _context.Categories.ToList();
            return View();
        }

        [HttpPost]
        public IActionResult AddFoodItem(FoodItem item)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            _context.FoodItems.Add(item);
            _context.SaveChanges();
            return RedirectToAction("ManageMenu");
        }
        
        [HttpGet]
        public IActionResult EditFoodItem(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            var item = _context.FoodItems.Find(id);
            if (item == null) return NotFound();

            ViewBag.Categories = _context.Categories.ToList();
            return View(item);
        }

        [HttpPost]
        public IActionResult EditFoodItem([Bind("Id,Name,Description,Price,CategoryId,ImageUrl,IsAvailable")] FoodItem item)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            //if (ModelState.IsValid)
            //{
                var existingItem = _context.FoodItems.Find(item.Id);
                if (existingItem == null) return NotFound();

                // Update only the fields we want
                existingItem.Name = item.Name;
                existingItem.Description = item.Description;
                existingItem.Price = item.Price;
                existingItem.CategoryId = item.CategoryId;
                existingItem.ImageUrl = item.ImageUrl;
                existingItem.IsAvailable = item.IsAvailable;

                _context.SaveChanges();
                TempData["Success"] = $"{item.Name} has been updated successfully!";
                return RedirectToAction("ManageMenu");
            //}

            //ViewBag.Categories = _context.Categories.ToList();
            //return View(item);
        }

        public IActionResult Orders(string status, string searchString, DateTime? fromDate, DateTime? toDate, string sortOrder)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            // Store current sort order for view
            ViewBag.CurrentSort = sortOrder;
            ViewBag.DateSortParam = sortOrder == "date_asc" ? "date_desc" : "date_asc";
            ViewBag.TotalSortParam = sortOrder == "total_asc" ? "total_desc" : "total_asc";
            ViewBag.StatusSortParam = sortOrder == "status" ? "status_desc" : "status";

            var orders = _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.FoodItem)
                .AsQueryable();

            // Filter by Status
            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                orders = orders.Where(o => o.Status == status);
                ViewBag.CurrentStatus = status;
            }

            // Filter by Customer Name
            if (!string.IsNullOrEmpty(searchString))
            {
                orders = orders.Where(o =>
                    o.User.FullName.Contains(searchString) ||
                    o.User.Username.Contains(searchString) ||
                    o.User.Email.Contains(searchString));
                ViewBag.CurrentSearch = searchString;
            }

            // Filter by Date Range
            if (fromDate.HasValue)
            {
                orders = orders.Where(o => o.OrderDate >= fromDate.Value);
                ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
            }
            if (toDate.HasValue)
            {
                orders = orders.Where(o => o.OrderDate <= toDate.Value.AddDays(1));
                ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");
            }

            // Sorting
            orders = sortOrder switch
            {
                "date_asc" => orders.OrderBy(o => o.OrderDate),
                "date_desc" => orders.OrderByDescending(o => o.OrderDate),
                "total_asc" => orders.OrderBy(o => o.TotalAmount),
                "total_desc" => orders.OrderByDescending(o => o.TotalAmount),
                "status" => orders.OrderBy(o => o.Status),
                "status_desc" => orders.OrderByDescending(o => o.Status),
                _ => orders.OrderByDescending(o => o.OrderDate) // default
            };

            // Status counts for dashboard stats
            ViewBag.PendingCount = _context.Orders.Count(o => o.Status == "Pending");
            ViewBag.ConfirmedCount = _context.Orders.Count(o => o.Status == "Confirmed");
            ViewBag.TodayCount = _context.Orders.Count(o => o.OrderDate.Date == DateTime.Today);
            ViewBag.TotalRevenue = _context.Orders.Where(o => o.Status != "Cancelled").Sum(o => (decimal?)o.TotalAmount) ?? 0;

            ViewBag.StatusList = new List<string> { "All", "Pending", "Confirmed", "Preparing", "OutForDelivery", "Delivered", "Cancelled" };

            return View(orders.ToList());
        }

        public IActionResult OrderDetails(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            var order = _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.FoodItem)
                .FirstOrDefault(o => o.Id == id);

            if (order == null) return NotFound();

            return View(order);
        }

        [HttpPost]
        public IActionResult UpdateOrderStatus(int orderId, string status)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            var order = _context.Orders.Find(orderId);
            if (order == null) return NotFound();

            // Valid statuses: Pending, Confirmed, Preparing, OutForDelivery, Delivered, Cancelled
            order.Status = status;
            _context.SaveChanges();

            TempData["Success"] = $"Order #{orderId} status updated to {status}";
            return RedirectToAction("Orders");
        }

        [HttpPost]
        public IActionResult ConfirmOrder(int orderId)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            var order = _context.Orders.Find(orderId);
            if (order == null) return NotFound();

            order.Status = "Confirmed";
            _context.SaveChanges();

            TempData["Success"] = $"Order #{orderId} has been confirmed!";
            return RedirectToAction("Orders");
        }

        [HttpPost]
        public IActionResult CancelOrder(int orderId)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            var order = _context.Orders.Find(orderId);
            if (order == null) return NotFound();

            // Only allow cancellation if not already delivered
            if (order.Status == "Delivered")
            {
                TempData["Error"] = "Cannot cancel delivered orders!";
                return RedirectToAction("Orders");
            }

            order.Status = "Cancelled";
            _context.SaveChanges();

            TempData["Success"] = $"Order #{orderId} has been cancelled!";
            return RedirectToAction("Orders");
        }
    }
}
