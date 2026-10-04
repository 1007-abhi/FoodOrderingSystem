using FoodOrderingSystem.data;
using FoodOrderingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Net.NetworkInformation;
using System.Text.Json.Serialization;
namespace FoodOrderingSystem.Controllers
{
    public class OrderController(AppDbContext _context) : Controller
    {
      
        public IActionResult AddToCart(int foodItemId, int quantity = 1)
        {
            var cart = GetCart();
            var existingItem = cart.FirstOrDefault(c => c.FoodItemId == foodItemId);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                var foodItem = _context.FoodItems.Find(foodItemId);
                cart.Add(new CartItem
                {
                    FoodItemId = foodItemId,
                    Name = foodItem.Name,
                    Price = foodItem.Price,
                    Quantity = quantity,
                    ImageUrl = foodItem.ImageUrl
                });
            }

            SaveCart(cart);
            return RedirectToAction("Cart");
        }
        private List<CartItem> GetCart()
        {
            var cartJson = HttpContext.Session.GetString("Cart");
            return cartJson == null ? new List<CartItem>() :
                JsonConvert.DeserializeObject<List<CartItem>>(cartJson);
        }

        private void SaveCart(List<CartItem> cart)
        {
            HttpContext.Session.SetString("Cart", JsonConvert.SerializeObject(cart));
        }
        public IActionResult UpdateQuantity(int foodItemId, int quantity)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.FoodItemId == foodItemId);

            if (item != null && quantity > 0)
            {
                item.Quantity = quantity;
                SaveCart(cart);
            }

            return RedirectToAction("Cart");
        }
        public IActionResult RemoveFromCart(int foodItemId)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.FoodItemId == foodItemId);

            if (item != null)
            {
                cart.Remove(item);
                SaveCart(cart);
            }

            return RedirectToAction("Cart");
        }
        public IActionResult Cart()
        {
            // Retrieve items from session
            var cart = GetCart();

            // Calculate Grand Total using LINQ
            ViewBag.Total = cart.Sum(c => c.Price * c.Quantity);

            // Pass the list to the View
            return View(cart);
        }

        [HttpGet]
        public IActionResult Checkout()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var cart = GetCart();
            if (!cart.Any()) return RedirectToAction("Index", "Menu");

            ViewBag.Total = cart.Sum(c => c.Price * c.Quantity);
            return View();
        }

        [HttpPost]
        public IActionResult Checkout(string deliveryAddress, string phoneNumber)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var cart = GetCart();

            var order = new Order
            {
                UserId = userId.Value,
                DeliveryAddress = deliveryAddress,
                PhoneNumber = phoneNumber,
                TotalAmount = cart.Sum(c => c.Price * c.Quantity),
                OrderItems = cart.Select(c => new OrderItem
                {
                    FoodItemId = c.FoodItemId,
                    Quantity = c.Quantity,
                    UnitPrice = c.Price
                }).ToList()
            };

            _context.Orders.Add(order);
            _context.SaveChanges();

            HttpContext.Session.Remove("Cart");
            return RedirectToAction("OrderConfirmation", new { orderId = order.Id });
        }

        public IActionResult OrderConfirmation(int orderId)
        {
            ViewBag.OrderId = orderId;
            return View();
        }

        public IActionResult MyOrders(string status, string sortOrder, DateTime? fromDate, DateTime? toDate)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var orders = _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.FoodItem)
                .Where(o => o.UserId == userId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                orders = orders.Where(o => o.Status == status);
                ViewBag.CurrentStatus = status;
            }

            // Filter by Date Range
            if (fromDate.HasValue)
            {
                orders = orders.Where(o => o.OrderDate >= fromDate.Value);
                ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
            }
            if (toDate.HasValue)
            {
                // Add 1 day to include the entire 'To' date
                orders = orders.Where(o => o.OrderDate <= toDate.Value.AddDays(1));
                ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");
            }

            // Sorting Logic
            ViewBag.CurrentSort = sortOrder;
            orders = sortOrder switch
            {
                "date_asc" => orders.OrderBy(o => o.OrderDate),
                "total_desc" => orders.OrderByDescending(o => o.TotalAmount),
                "total_asc" => orders.OrderBy(o => o.TotalAmount),
                _ => orders.OrderByDescending(o => o.OrderDate) // default: newest first
            };

            ViewBag.StatusList = new List<string> { "All", "Pending", "Confirmed", "Preparing", "OutForDelivery", "Delivered", "Cancelled" };

            return View(orders.ToList());
        }

        public IActionResult TrackOrder(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var order = _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.FoodItem)
                .FirstOrDefault(o => o.Id == id && o.UserId == userId);

            if (order == null) return NotFound();

            return View(order);
        }

        [HttpPost]
        public IActionResult CancelMyOrder(int orderId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var order = _context.Orders.FirstOrDefault(o => o.Id == orderId && o.UserId == userId);

            if (order == null) return NotFound();

            // Only allow cancellation if status is Pending or Confirmed
            if (order.Status != "Pending" && order.Status != "Confirmed")
            {
                TempData["Error"] = "Order cannot be cancelled at this stage!";
                return RedirectToAction("TrackOrder", new { id = orderId });
            }

            order.Status = "Cancelled";
            _context.SaveChanges();

            TempData["Success"] = "Order cancelled successfully!";
            return RedirectToAction("MyOrders");
        }

        public IActionResult GetCartCount()
        {
            // Retrieve the Cart JSON string from the Session
            var cartJson = HttpContext.Session.GetString("Cart");
            var count = 0;

            // Check if the cart is not empty
            if (!string.IsNullOrEmpty(cartJson))
            {
                // Deserialize the JSON back into a List of CartItems
                var cart = JsonConvert.DeserializeObject<List<CartItem>>(cartJson);

                // Sum up the total quantity of all items in the cart
                count = cart.Sum(c => c.Quantity);
            }

            // Return the count as a JSON object for AJAX calls
            return Json(new { count });
        }
    }
    public class CartItem
    {
        public int FoodItemId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; }
    }
}
