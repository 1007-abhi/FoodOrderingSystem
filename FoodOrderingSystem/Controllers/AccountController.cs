using FoodOrderingSystem.data;
using FoodOrderingSystem.Dto;
using FoodOrderingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodOrderingSystem.Controllers
{
    public class AccountController(AppDbContext _context) : Controller
    {
        public IActionResult Register()
        {
            return View();
        }
        [HttpPost]
        public IActionResult Register(RegisterViewModel dto)
        {
            if (ModelState.IsValid)
            {
                var user = new User
                {
                    Username = dto.Username,
                    Email = dto.Email,
                    Password = dto.Password, // In production, hash this!
                    FullName = dto.FullName,
                    Address = dto.Address,
                    Phone = dto.Phone
                };

                _context.Users.Add(user);
                _context.SaveChanges();

                return RedirectToAction("Login");
            }

            return View(dto);
        }
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(LoginViewModel dto)
        {
            var user = _context.Users.FirstOrDefault(u =>
                u.Username == dto.Username && u.Password == dto.Password);

            if (user != null)
            {
                HttpContext.Session.SetInt32("UserId", user.Id);
                HttpContext.Session.SetString("Username", user.Username);
                HttpContext.Session.SetString("IsAdmin", user.IsAdmin.ToString());

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Invalid username or password");
            return View(dto);
        }

        [HttpGet]
        public IActionResult Profile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var user = _context.Users.Find(userId);
            if (user == null) return NotFound();

            var model = new ProfileViewModel
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FullName = user.FullName,
                Address = user.Address,
                Phone = user.Phone
            };

            return View(model);
        }

        [HttpPost]
        public IActionResult Profile(ProfileViewModel dto)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            if (ModelState.IsValid)
            {
                var user = _context.Users.Find(userId);
                if (user == null) return NotFound();

                // Check if email is already taken by another user
                var emailExists = _context.Users.Any(u => u.Email == dto.Email && u.Id != userId);
                if (emailExists)
                {
                    ModelState.AddModelError("Email", "Email is already registered to another account.");
                    return View(dto);
                }

                user.FullName = dto.FullName;
                user.Email = dto.Email;
                user.Phone = dto.Phone;
                user.Address = dto.Address;

                _context.SaveChanges();
                TempData["Success"] = "Profile updated successfully!";
                return RedirectToAction("Profile");
            }

            return View(dto);
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ChangePassword(ChangePasswordViewModel model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            if (ModelState.IsValid)
            {
                var user = _context.Users.Find(userId);
                if (user.Password != model.CurrentPassword) // In production, verify hash!
                {
                    ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                    return View(model);
                }

                user.Password = model.NewPassword; // In production, hash the password!
                _context.SaveChanges();

                TempData["Success"] = "Password changed successfully!";
                return RedirectToAction("Profile");
            }

            return View(model);
        }

        [HttpPost]
        public IActionResult DeleteAccount()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var user = _context.Users.Find(userId);
            if (user == null) return NotFound();

            // Optional: Check if user has pending orders
            var pendingOrders = _context.Orders.Any(o => o.UserId == userId &&
                (o.Status == "Pending" || o.Status == "Confirmed" || o.Status == "Preparing"));

            if (pendingOrders)
            {
                TempData["Error"] = "Cannot delete account while you have active orders!";
                return RedirectToAction("Profile");
            }

            _context.Users.Remove(user);
            _context.SaveChanges();

            HttpContext.Session.Clear();
            TempData["Success"] = "Your account has been deleted.";
            return RedirectToAction("Index", "Home");
        }
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

    }
}
