using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantOrderingSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RestaurantOrderingSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public HomeController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // ✅ Landing Page
        [AllowAnonymous]
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
        // ✅ Login Page
        [AllowAnonymous]
        public IActionResult Login() => View();

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                isPersistent: false,
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);

                if (await _userManager.IsInRoleAsync(user, "Admin"))
                    return RedirectToAction("AdminDashboard");
                else
                    return RedirectToAction("UserDashboard");
            }

            ModelState.AddModelError("", "Invalid login attempt.");
            return View(model);
        }

        // ✅ Register Page
        [AllowAnonymous]
        public IActionResult Register() => View();

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    BirthDate = model.BirthDate,
                    Address = model.Address,
                    PhoneNumber = model.PhoneNumber
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Customer");
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("UserDashboard");
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        // ✅ Admin Dashboard
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminDashboard()
        {
            var orders = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                .Include(o => o.TableReservation)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var totalRevenue = orders.Where(o => o.Status == "Completed").Sum(o => o.TotalPrice);
            var pendingOrders = orders.Count(o => o.Status == "Pending");
            var todayReservations = orders.Count(o => o.TableReservation != null && o.TableReservation.ReservationDate.Date == DateTime.Today);

            var viewModel = new AdminDashboardViewModel
            {
                Orders = orders,
                TotalRevenue = totalRevenue,
                PendingOrders = pendingOrders,
                TodayReservations = todayReservations,
                TotalOrders = orders.Count
            };

            return View(viewModel);
        }

        // ✅ User Dashboard
        [Authorize]
        public async Task<IActionResult> UserDashboard()
        {
            var user = await _userManager.GetUserAsync(User);

            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.TableReservation)
                .Where(o => o.UserId == user.Id)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var viewModel = new UserDashboardViewModel
            {
                Orders = orders,
                User = user
            };

            return View(viewModel);
        }

        // ✅ Create Order with Multiple Items & Reservation - GET
        [Authorize]
        public IActionResult CreateOrder()
        {
            var lastOrder = _context.Orders
                .OrderByDescending(o => o.Id)
                .FirstOrDefault();

            int nextNumber = (lastOrder == null) ? 1001 : lastOrder.Id + 1;

            var model = new CreateOrderViewModel
            {
                OrderNo = $"ORD-{nextNumber:D4}",
                Customer = User.Identity?.Name,
                OrderItems = new List<OrderItemViewModel>
                {
                    new OrderItemViewModel { ItemName = "", Quantity = 1 }
                },
                ReservationDate = DateTime.Today,
                ReservationTime = TimeSpan.FromHours(18),
                NumberOfGuests = 2
            };

            return View(model);
        }

        // ✅ Create Order with Multiple Items & Reservation - POST
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOrder(CreateOrderViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);

                // Create the main order
                var order = new Order
                {
                    OrderNo = model.OrderNo,
                    Customer = model.Customer,
                    UserId = user.Id,
                    Status = "Pending",
                    OrderDate = DateTime.Now,
                    PaymentMethod = model.PaymentMethod,
                    TotalPrice = 0
                };

                // Add order items
                foreach (var itemModel in model.OrderItems.Where(i => !string.IsNullOrEmpty(i.ItemName) && i.Quantity > 0))
                {
                    var unitPrice = GetFoodItemPrice(itemModel.ItemName);
                    var orderItem = new OrderItem
                    {
                        ItemName = itemModel.ItemName,
                        Quantity = itemModel.Quantity,
                        UnitPrice = unitPrice
                    };
                    order.OrderItems.Add(orderItem);
                    order.TotalPrice += orderItem.TotalPrice;
                }

                // Create table reservation if needed
                if (model.NeedTableReservation && !string.IsNullOrEmpty(model.TableNumber))
                {
                    var reservation = new TableReservation
                    {
                        CustomerName = model.Customer ?? user.FullName,
                        CustomerEmail = user.Email ?? "",
                        CustomerPhone = user.PhoneNumber ?? "",
                        TableNumber = model.TableNumber,
                        NumberOfGuests = model.NumberOfGuests ?? 2,
                        ReservationDate = model.ReservationDate ?? DateTime.Today,
                        ReservationTime = model.ReservationTime ?? TimeSpan.FromHours(18),
                        SpecialRequests = model.SpecialRequests,
                        UserId = user.Id,
                        Status = "Pending",
                        CreatedAt = DateTime.Now
                    };

                    order.TableReservation = reservation;
                }

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Order created successfully!";
                return RedirectToAction("Receipt", new { id = order.Id });
            }

            return View(model);
        }

        // ✅ Edit Order - GET
        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.TableReservation)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound();

            return View(order);
        }

        // ✅ Edit Order - POST
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Order model)
        {
            if (ModelState.IsValid)
            {
                var existingOrder = await _context.Orders
                    .Include(o => o.OrderItems)
                    .Include(o => o.TableReservation)
                    .FirstOrDefaultAsync(o => o.Id == model.Id);

                if (existingOrder == null)
                    return NotFound();

                // Update order properties
                existingOrder.Customer = model.Customer;
                existingOrder.PaymentMethod = model.PaymentMethod;
                existingOrder.Status = model.Status;
                existingOrder.TotalPrice = 0;

                // Remove existing order items
                _context.OrderItems.RemoveRange(existingOrder.OrderItems);

                // Add updated order items
                foreach (var item in model.OrderItems.Where(i => !string.IsNullOrEmpty(i.ItemName) && i.Quantity > 0))
                {
                    var orderItem = new OrderItem
                    {
                        ItemName = item.ItemName,
                        Quantity = item.Quantity,
                        UnitPrice = GetFoodItemPrice(item.ItemName),
                        OrderId = model.Id
                    };
                    existingOrder.OrderItems.Add(orderItem);
                    existingOrder.TotalPrice += orderItem.TotalPrice;
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Order updated successfully!";
                return RedirectToAction("AdminDashboard");
            }

            return View(model);
        }

        // ✅ Delete Order (Admin only)
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.TableReservation)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound();

            // Remove order items first (due to foreign key constraint)
            _context.OrderItems.RemoveRange(order.OrderItems);

            // Remove table reservation if exists
            if (order.TableReservation != null)
            {
                _context.TableReservations.Remove(order.TableReservation);
            }

            // Remove the order
            _context.Orders.Remove(order);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Order deleted successfully!";
            return RedirectToAction("AdminDashboard");
        }

        // ✅ Cancel Order (User)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            var user = await _userManager.GetUserAsync(User);

            if (order == null || order.UserId != user.Id)
                return NotFound();

            order.Status = "Cancelled";

            // Also cancel the reservation if exists
            if (order.TableReservationId.HasValue)
            {
                var reservation = await _context.TableReservations.FindAsync(order.TableReservationId);
                if (reservation != null)
                {
                    reservation.Status = "Cancelled";
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Order cancelled successfully!";
            return RedirectToAction("UserDashboard");
        }

        // ✅ Update Order Status (Admin)
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(int id, string status)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
                return NotFound();

            order.Status = status;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Order status updated to {status}!";
            return RedirectToAction("AdminDashboard");
        }

        // ✅ Receipt
        [Authorize]
        public async Task<IActionResult> Receipt(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.TableReservation)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound();

            var user = await _userManager.GetUserAsync(User);

            if (!User.IsInRole("Admin") && order.UserId != user.Id)
                return Forbid();

            return View(order);
        }

        // ✅ Logout
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogOff()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index");
        }

        // ✅ Helper Methods
        private List<FoodItem> GetFoodItems()
        {
            return new List<FoodItem>
            {
                new FoodItem { Name = "Fried Chicken", Price = 25 },
                new FoodItem { Name = "Burger Steak", Price = 30 },
                new FoodItem { Name = "Spaghetti", Price = 20 },
                new FoodItem { Name = "Pancit Canton", Price = 18 },
                new FoodItem { Name = "Sisig", Price = 28 },
                new FoodItem { Name = "Lechon Kawali", Price = 32 },
                new FoodItem { Name = "Adobo", Price = 27 },
                new FoodItem { Name = "Beef Tapa", Price = 35 },
                new FoodItem { Name = "Sinigang", Price = 33 },
                new FoodItem { Name = "Halo-Halo", Price = 15 },
                new FoodItem { Name = "Caesar Salad", Price = 22 },
                new FoodItem { Name = "Garlic Rice", Price = 12 },
                new FoodItem { Name = "Soft Drinks", Price = 15 },
                new FoodItem { Name = "Iced Tea", Price = 12 }
            };
        }

        private decimal GetFoodItemPrice(string itemName)
        {
            var foodItem = GetFoodItems().FirstOrDefault(f => f.Name == itemName);
            return foodItem?.Price ?? 0;
        }
    }

    public class FoodItem
    {
        public string Name { get; set; } = null!;
        public decimal Price { get; set; }
    }
}