using FoodOrderingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FoodOrderingSystem.data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<FoodItem> FoodItems { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Seed initial data
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Pizza", Description = "Delicious pizzas" },
                new Category { Id = 2, Name = "Burgers", Description = "Juicy burgers" },
                new Category { Id = 3, Name = "Drinks", Description = "Refreshing beverages" }
            );

            modelBuilder.Entity<FoodItem>().HasData(
                new FoodItem { Id = 1, Name = "Margherita Pizza", Description = "Classic tomato and cheese", Price = 12.99m, CategoryId = 1, ImageUrl = "/images/Margheritapizza.jpg" },
                new FoodItem { Id = 2, Name = "Pepperoni Pizza", Description = "Spicy pepperoni with cheese", Price = 14.99m, CategoryId = 1, ImageUrl = "/images/Pepperonipizza.jpg" },
                new FoodItem { Id = 3, Name = "Cheeseburger", Description = "Beef patty with cheese", Price = 9.99m, CategoryId = 2, ImageUrl = "/images/Cheeseburger.jpg" }
            );

            modelBuilder.Entity<User>().HasData(
               new User
               {
                   Id = 1,
                   Username = "admin",
                   Email = "admin@foodorder.com",
                   Password = "admin123",  // In production, use hashed password!
                   FullName = "Administrator",
                   Address = "Admin Office",
                   Phone = "1234567890",
                   IsAdmin = true
               }
            );
        }


    }
}
