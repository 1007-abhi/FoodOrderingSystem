using FoodOrderingSystem.data;
using FoodOrderingSystem.Dto;
using FoodOrderingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Razorpay.Api;
using System.Security.Cryptography;
using System.Text;
using Order = FoodOrderingSystem.Models.Order;

namespace FoodOrderingSystem.Controllers
{
    public class OrderController(
        AppDbContext _context,
        IConfiguration _configuration) : Controller
    {

        // =========================================================
        // ADD TO CART
        // =========================================================

        public IActionResult AddToCart(int foodItemId, int quantity = 1)
        {
            var cart = GetCart();

            var existingItem =
                cart.FirstOrDefault(c => c.FoodItemId == foodItemId);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                var foodItem = _context.FoodItems.Find(foodItemId);

                if (foodItem == null)
                {
                    return NotFound();
                }

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


        // =========================================================
        // GET CART
        // =========================================================

        private List<CartItem> GetCart()
        {
            var cartJson =
                HttpContext.Session.GetString("Cart");

            if (string.IsNullOrEmpty(cartJson))
            {
                return new List<CartItem>();
            }

            return JsonConvert.DeserializeObject<List<CartItem>>(
                cartJson
            ) ?? new List<CartItem>();
        }


        // =========================================================
        // SAVE CART
        // =========================================================

        private void SaveCart(List<CartItem> cart)
        {
            HttpContext.Session.SetString(
                "Cart",
                JsonConvert.SerializeObject(cart)
            );
        }


        // =========================================================
        // UPDATE QUANTITY
        // =========================================================

        public IActionResult UpdateQuantity(
            int foodItemId,
            int quantity)
        {
            var cart = GetCart();

            var item =
                cart.FirstOrDefault(
                    c => c.FoodItemId == foodItemId);

            if (item != null && quantity > 0)
            {
                item.Quantity = quantity;

                SaveCart(cart);
            }

            return RedirectToAction("Cart");
        }


        // =========================================================
        // REMOVE FROM CART
        // =========================================================

        public IActionResult RemoveFromCart(int foodItemId)
        {
            var cart = GetCart();

            var item =
                cart.FirstOrDefault(
                    c => c.FoodItemId == foodItemId);

            if (item != null)
            {
                cart.Remove(item);

                SaveCart(cart);
            }

            return RedirectToAction("Cart");
        }


        // =========================================================
        // CART
        // =========================================================

        public IActionResult Cart()
        {
            var cart = GetCart();

            ViewBag.Total =
                cart.Sum(c => c.Price * c.Quantity);

            return View(cart);
        }


        // =========================================================
        // CHECKOUT - GET
        // =========================================================

        [HttpGet]
        public IActionResult Checkout()
        {
            var userId =
                HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var cart = GetCart();

            if (!cart.Any())
            {
                return RedirectToAction(
                    "Index",
                    "Menu");
            }

            ViewBag.Total =
                cart.Sum(c => c.Price * c.Quantity);

            return View();
        }


        // =========================================================
        // OLD CHECKOUT - KEEP AS BACKUP
        // =========================================================

        [HttpPost]
        public IActionResult Checkout(
            string deliveryAddress,
            string phoneNumber)
        {
            var userId =
                HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var cart = GetCart();

            if (!cart.Any())
            {
                return RedirectToAction(
                    "Cart");
            }

            var order = new Order
            {
                UserId = userId.Value,

                DeliveryAddress = deliveryAddress,

                PhoneNumber = phoneNumber,

                TotalAmount =
                    cart.Sum(c => c.Price * c.Quantity),

                PaymentMethod = "COD",

                PaymentStatus = "Pending",

                Status = "Pending",

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

            return RedirectToAction(
                "OrderConfirmation",
                new { orderId = order.Id });
        }


        // =========================================================
        // CREATE RAZORPAY ORDER
        // =========================================================

        [HttpPost]
        public IActionResult CreateRazorpayOrder(
            string deliveryAddress,
            string phoneNumber)
        {
            try
            {
                // Check login
                var userId = HttpContext.Session.GetInt32("UserId");

                if (userId == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Please login first."
                    });
                }


                // Validate address
                if (string.IsNullOrWhiteSpace(deliveryAddress))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Delivery address is required."
                    });
                }


                // Validate phone
                if (string.IsNullOrWhiteSpace(phoneNumber))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Phone number is required."
                    });
                }


                // Get session cart
                var cart = GetCart();

                if (!cart.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "Your cart is empty."
                    });
                }


                // Calculate total on SERVER
                decimal total = cart.Sum(c => c.Price * c.Quantity);
                if (total <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Invalid order amount."
                    });
                }


                // Convert INR to paise
                int amountInPaise = Convert.ToInt32(Math.Round(total * 100,MidpointRounding.AwayFromZero));
                // Razorpay credentials
                string keyId =_configuration["Razorpay:KeyId"];

                string keySecret =_configuration["Razorpay:KeySecret"];

                if (string.IsNullOrWhiteSpace(keyId) ||
                    string.IsNullOrWhiteSpace(keySecret))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Razorpay configuration is missing."
                    });
                }


                // Create Razorpay client
                RazorpayClient client =
     new RazorpayClient(
         keyId,
         keySecret);

                // Razorpay order options
                Dictionary<string, object> options =
                    new Dictionary<string, object>();

                options.Add(
                    "amount",
                    amountInPaise);

                options.Add(
                    "currency",
                    "INR");

                options.Add(
                    "receipt",
                    "FOOD_" + DateTime.Now.Ticks);


                // Create Razorpay order
                Razorpay.Api.Order razorpayOrder =
                    client.Order.Create(options);


                string razorpayOrderId =
                    razorpayOrder["id"].ToString();


                return Json(new
                {
                    success = true,

                    key = keyId,

                    orderId = razorpayOrderId,

                    amount = amountInPaise,

                    currency = "INR",

                    name = "Food Ordering System",

                    description = "Food Order Payment",

                    customerName = User.Identity?.Name ?? "",

                    phoneNumber = phoneNumber
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message =
                        "Unable to create Razorpay order: "
                        + ex.Message
                });
            }
        }


        // =========================================================
        // VERIFY RAZORPAY PAYMENT
        // =========================================================

        [HttpPost]
        public IActionResult VerifyRazorpayPayment(
            string razorpay_order_id,
            string razorpay_payment_id,
            string razorpay_signature,
            string deliveryAddress,
            string phoneNumber)
        {
            try
            {
                // Check login
                var userId =
                    HttpContext.Session.GetInt32("UserId");

                if (userId == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Please login first."
                    });
                }


                // Validate Razorpay response
                if (string.IsNullOrWhiteSpace(
                    razorpay_order_id) ||
                    string.IsNullOrWhiteSpace(
                    razorpay_payment_id) ||
                    string.IsNullOrWhiteSpace(
                    razorpay_signature))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Invalid payment response."
                    });
                }


                // Get Razorpay secret
                string keySecret =
                    _configuration[
                        "Razorpay:KeySecret"];


                // ---------------------------------------------
                // VERIFY SIGNATURE
                // ---------------------------------------------

                string payload =
                    razorpay_order_id
                    + "|"
                    + razorpay_payment_id;


                byte[] secretBytes =
                    Encoding.UTF8.GetBytes(
                        keySecret);


                byte[] payloadBytes =
                    Encoding.UTF8.GetBytes(
                        payload);


                using var hmac =
                    new HMACSHA256(
                        secretBytes);


                byte[] hash =
                    hmac.ComputeHash(
                        payloadBytes);


                string generatedSignature =
                    Convert.ToHexString(
                        hash)
                    .ToLower();


                bool isValid =
                    string.Equals(
                        generatedSignature,
                        razorpay_signature,
                        StringComparison.OrdinalIgnoreCase);


                if (!isValid)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Payment verification failed."
                    });
                }


                // ---------------------------------------------
                // GET CART
                // ---------------------------------------------

                var cart = GetCart();

                if (!cart.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "Cart is empty."
                    });
                }


                // ---------------------------------------------
                // CALCULATE TOTAL AGAIN ON SERVER
                // ---------------------------------------------

                decimal total =
                    cart.Sum(
                        c => c.Price * c.Quantity);


                // ---------------------------------------------
                // CREATE DATABASE ORDER
                // ---------------------------------------------

                var order = new Order
                {
                    UserId = userId.Value,

                    DeliveryAddress =
                        deliveryAddress,

                    PhoneNumber =
                        phoneNumber,

                    TotalAmount =
                        total,

                    Status = "Confirmed",

                    PaymentMethod =
                        "Razorpay",

                    PaymentStatus =
                        "Paid",

                    RazorpayOrderId =
                        razorpay_order_id,

                    RazorpayPaymentId =
                        razorpay_payment_id,

                    RazorpaySignature =
                        razorpay_signature,

                    OrderItems =
                        cart.Select(c =>
                            new OrderItem
                            {
                                FoodItemId =
                                    c.FoodItemId,

                                Quantity =
                                    c.Quantity,

                                UnitPrice =
                                    c.Price

                            }).ToList()
                };


                // Save order
                _context.Orders.Add(order);

                _context.SaveChanges();


                // Clear cart
                HttpContext.Session.Remove("Cart");


                // Success
                return Json(new
                {
                    success = true,

                    orderId = order.Id
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Payment verification error: "
                        + ex.Message
                });
            }
        }


        // =========================================================
        // CASH ON DELIVERY
        // =========================================================

        [HttpPost]
        public IActionResult PlaceCODOrder(
            string deliveryAddress,
            string phoneNumber)
        {
            try
            {
                var userId =
                    HttpContext.Session.GetInt32("UserId");

                if (userId == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Please login first."
                    });
                }


                var cart = GetCart();

                if (!cart.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "Your cart is empty."
                    });
                }


                decimal total =
                    cart.Sum(
                        c => c.Price * c.Quantity);


                var order = new Order
                {
                    UserId = userId.Value,

                    DeliveryAddress =
                        deliveryAddress,

                    PhoneNumber =
                        phoneNumber,

                    TotalAmount =
                        total,

                    Status =
                        "Pending",

                    PaymentMethod =
                        "COD",

                    PaymentStatus =
                        "Pending",

                    OrderItems =
                        cart.Select(c =>
                            new OrderItem
                            {
                                FoodItemId =
                                    c.FoodItemId,

                                Quantity =
                                    c.Quantity,

                                UnitPrice =
                                    c.Price

                            }).ToList()
                };


                _context.Orders.Add(order);

                _context.SaveChanges();


                HttpContext.Session.Remove("Cart");


                return Json(new
                {
                    success = true,

                    orderId = order.Id
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Unable to place order: "
                        + ex.Message
                });
            }
        }


        // =========================================================
        // ORDER CONFIRMATION
        // =========================================================

        public IActionResult OrderConfirmation(
            int orderId)
        {
            var userId =
                HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var order =
                _context.Orders
                    .FirstOrDefault(
                        o => o.Id == orderId &&
                             o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            ViewBag.OrderId =
                orderId;

            ViewBag.PaymentMethod =
                order.PaymentMethod;

            ViewBag.PaymentStatus =
                order.PaymentStatus;

            ViewBag.Total =
                order.TotalAmount;

            return View();
        }


        // =========================================================
        // MY ORDERS
        // =========================================================

        public IActionResult MyOrders(
            string status,
            string sortOrder,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var userId =
                HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var orders =
                _context.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.FoodItem)
                    .Where(o => o.UserId == userId)
                    .AsQueryable();


            if (!string.IsNullOrEmpty(status) &&
                status != "All")
            {
                orders =
                    orders.Where(
                        o => o.Status == status);

                ViewBag.CurrentStatus =
                    status;
            }


            if (fromDate.HasValue)
            {
                orders =
                    orders.Where(
                        o => o.OrderDate >=
                             fromDate.Value);

                ViewBag.FromDate =
                    fromDate.Value.ToString(
                        "yyyy-MM-dd");
            }


            if (toDate.HasValue)
            {
                orders = orders.Where(o => o.OrderDate <= toDate.Value.AddDays(1));

                ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");
            }


            ViewBag.CurrentSort = sortOrder;

            orders =sortOrder switch
                {
                    "date_asc" =>orders.OrderBy(o => o.OrderDate),

                    "total_desc" =>orders.OrderByDescending(o => o.TotalAmount),

                    "total_asc" =>orders.OrderBy(o => o.TotalAmount),

                    _ =>orders.OrderByDescending(o => o.OrderDate)
                };


            ViewBag.StatusList = new List<string>{
                    "All",
                    "Pending",
                    "Confirmed",
                    "Preparing",
                    "OutForDelivery",
                    "Delivered",
                    "Cancelled"
                };
            return View(orders.ToList());
        }


        // =========================================================
        // TRACK ORDER
        // =========================================================

        public IActionResult TrackOrder(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }


            var order =
                _context.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.FoodItem)
                    .FirstOrDefault(
                        o => o.Id == id &&
                             o.UserId == userId);


            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }


        // =========================================================
        // CANCEL ORDER
        // =========================================================

        [HttpPost]
        public IActionResult CancelMyOrder(
            int orderId)
        {
            var userId =
                HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }


            var order =
                _context.Orders.FirstOrDefault(
                    o => o.Id == orderId &&
                         o.UserId == userId);


            if (order == null){ return NotFound();}

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


        // =========================================================
        // CART COUNT
        // =========================================================

        public IActionResult GetCartCount()
        {
            var cartJson = HttpContext.Session.GetString("Cart");
            var count = 0;

            if (!string.IsNullOrEmpty(cartJson))
            {
                var cart =JsonConvert.DeserializeObject<List<CartItem>>(cartJson);
                if (cart != null)
                {
                    count = cart.Sum(c => c.Quantity);
                }
            }
            return Json(new{count});
        }
    }


    // =============================================================
    // CART ITEM
    // =============================================================

    public class CartItem
    {
        public int FoodItemId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; }
    }
}