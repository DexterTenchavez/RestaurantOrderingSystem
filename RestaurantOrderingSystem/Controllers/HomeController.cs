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
                    return RedirectToAction("UserHome");
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
                    return RedirectToAction("UserHome");
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

        // ✅ User Dashboard with Search and Filter
        [Authorize]
        public async Task<IActionResult> UserDashboard(string search, string status, string dateFilter, string reservationFilter)
        {
            var user = await _userManager.GetUserAsync(User);

            var ordersQuery = _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.TableReservation)
                .Where(o => o.UserId == user.Id);

            // Apply search filter
            if (!string.IsNullOrEmpty(search))
            {
                ordersQuery = ordersQuery.Where(o =>
                    o.OrderNo.Contains(search) ||
                    o.Customer.Contains(search) ||
                    o.OrderItems.Any(i => i.ItemName.Contains(search)));
            }

            // Apply status filter
            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                ordersQuery = ordersQuery.Where(o => o.Status == status);
            }

            // Apply date filter
            if (!string.IsNullOrEmpty(dateFilter))
            {
                var today = DateTime.Today;
                switch (dateFilter)
                {
                    case "today":
                        ordersQuery = ordersQuery.Where(o => o.OrderDate.Date == today);
                        break;
                    case "week":
                        ordersQuery = ordersQuery.Where(o => o.OrderDate >= today.AddDays(-7));
                        break;
                    case "month":
                        ordersQuery = ordersQuery.Where(o => o.OrderDate >= today.AddDays(-30));
                        break;
                }
            }

            // Apply reservation filter
            if (!string.IsNullOrEmpty(reservationFilter))
            {
                if (reservationFilter == "with")
                {
                    ordersQuery = ordersQuery.Where(o => o.TableReservation != null);
                }
                else if (reservationFilter == "without")
                {
                    ordersQuery = ordersQuery.Where(o => o.TableReservation == null);
                }
            }

            var orders = await ordersQuery.OrderByDescending(o => o.OrderDate).ToListAsync();

            var viewModel = new UserDashboardViewModel
            {
                Orders = orders,
                User = user,
                SearchTerm = search,
                StatusFilter = status,
                DateFilter = dateFilter,
                ReservationFilter = reservationFilter
            };

            return View(viewModel);
        }

        // ✅ Create Order with Multiple Items & Reservation - GET
        [Authorize]
        public async Task<IActionResult> CreateOrder()
        {
            var user = await _userManager.GetUserAsync(User);
            var lastOrder = _context.Orders
                .OrderByDescending(o => o.Id)
                .FirstOrDefault();

            int nextNumber = (lastOrder == null) ? 1001 : lastOrder.Id + 1;

            var model = new CreateOrderViewModel
            {
                OrderNo = $"ORD-{nextNumber:D4}",
                Customer = user?.FullName ?? user?.UserName ?? "Customer", // Use FullName instead of email
                OrderItems = new List<OrderItemViewModel>(), // Empty for visual cards
                ReservationDate = DateTime.Today,
                ReservationTime = TimeSpan.FromHours(18),
                NumberOfGuests = 2
            };

            return View(model);
        }

        // ✅ Create Order with Multiple Items & Reservation - POST (FIXED)
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

                // Process order items from form collection
                var form = await Request.ReadFormAsync();
                var orderItems = new List<OrderItem>();

                // Find all order item fields
                var itemNameKeys = form.Keys.Where(k => k.StartsWith("OrderItems[") && k.Contains("].ItemName"));

                foreach (var itemNameKey in itemNameKeys)
                {
                    var indexMatch = System.Text.RegularExpressions.Regex.Match(itemNameKey, @"OrderItems\[(\d+)\]\.ItemName");
                    if (indexMatch.Success)
                    {
                        var index = indexMatch.Groups[1].Value;
                        var itemName = form[$"OrderItems[{index}].ItemName"];
                        var quantityStr = form[$"OrderItems[{index}].Quantity"];
                        var unitPriceStr = form[$"OrderItems[{index}].UnitPrice"];

                        if (!string.IsNullOrEmpty(itemName) &&
                            int.TryParse(quantityStr, out int quantity) &&
                            quantity > 0 &&
                            decimal.TryParse(unitPriceStr, out decimal unitPrice))
                        {
                            var orderItem = new OrderItem
                            {
                                ItemName = itemName,
                                Quantity = quantity,
                                UnitPrice = unitPrice
                            };
                            orderItems.Add(orderItem);
                            order.TotalPrice += orderItem.TotalPrice;
                        }
                    }
                }

                // Add order items to the order
                foreach (var item in orderItems)
                {
                    order.OrderItems.Add(item);
                }

                // ✅ FIX: Create table reservation if table is selected (removed NeedTableReservation check)
                if (!string.IsNullOrEmpty(model.TableNumber))
                {
                    var reservation = new TableReservation
                    {
                        CustomerName = model.Customer ?? user.FullName ?? user.UserName ?? "Customer",
                        CustomerEmail = user.Email ?? "customer@example.com",
                        CustomerPhone = user.PhoneNumber ?? "000-000-0000",
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


        // ✅ User Edit Order - GET
        [Authorize]
        public async Task<IActionResult> UserEdit(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.TableReservation)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound();

            var user = await _userManager.GetUserAsync(User);

            // Ensure user can only edit their own orders
            if (order.UserId != user.Id && !User.IsInRole("Admin"))
                return Forbid();

            return View(order);
        }

        // ✅ User Edit Order - POST
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserEdit(Order model)
        {
            if (ModelState.IsValid)
            {
                var existingOrder = await _context.Orders
                    .Include(o => o.OrderItems)
                    .Include(o => o.TableReservation)
                    .FirstOrDefaultAsync(o => o.Id == model.Id);

                if (existingOrder == null)
                    return NotFound();

                var user = await _userManager.GetUserAsync(User);

                // Ensure user can only edit their own orders
                if (existingOrder.UserId != user.Id && !User.IsInRole("Admin"))
                    return Forbid();

                // Only allow editing of certain fields for users
                existingOrder.Customer = model.Customer;
                existingOrder.PaymentMethod = model.PaymentMethod;
                existingOrder.TotalPrice = 0;

                // Remove existing order items
                _context.OrderItems.RemoveRange(existingOrder.OrderItems);

                // Process order items from form collection
                var form = await Request.ReadFormAsync();
                var orderItems = new List<OrderItem>();

                // Find all order item fields
                var itemNameKeys = form.Keys.Where(k => k.StartsWith("OrderItems[") && k.Contains("].ItemName"));

                foreach (var itemNameKey in itemNameKeys)
                {
                    var indexMatch = System.Text.RegularExpressions.Regex.Match(itemNameKey, @"OrderItems\[(\d+)\]\.ItemName");
                    if (indexMatch.Success)
                    {
                        var index = indexMatch.Groups[1].Value;
                        var itemName = form[$"OrderItems[{index}].ItemName"];
                        var quantityStr = form[$"OrderItems[{index}].Quantity"];
                        var unitPriceStr = form[$"OrderItems[{index}].UnitPrice"];

                        if (!string.IsNullOrEmpty(itemName) &&
                            int.TryParse(quantityStr, out int quantity) &&
                            quantity > 0 &&
                            decimal.TryParse(unitPriceStr, out decimal unitPrice))
                        {
                            var orderItem = new OrderItem
                            {
                                ItemName = itemName,
                                Quantity = quantity,
                                UnitPrice = unitPrice,
                                OrderId = model.Id
                            };
                            orderItems.Add(orderItem);
                            existingOrder.TotalPrice += orderItem.TotalPrice;
                        }
                    }
                }

                // Add updated order items
                foreach (var item in orderItems)
                {
                    existingOrder.OrderItems.Add(item);
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Order updated successfully!";
                return RedirectToAction("UserDashboard");
            }

            return View(model);
        }


        // ✅ Edit Order - GET (Admin)
        [Authorize(Roles = "Admin")]
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

        // ✅ Edit Order - POST (Admin)
        [Authorize(Roles = "Admin")]
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

                // Process order items from form collection
                var form = await Request.ReadFormAsync();
                var orderItems = new List<OrderItem>();

                // Find all order item fields
                var itemNameKeys = form.Keys.Where(k => k.StartsWith("OrderItems[") && k.Contains("].ItemName"));

                foreach (var itemNameKey in itemNameKeys)
                {
                    var indexMatch = System.Text.RegularExpressions.Regex.Match(itemNameKey, @"OrderItems\[(\d+)\]\.ItemName");
                    if (indexMatch.Success)
                    {
                        var index = indexMatch.Groups[1].Value;
                        var itemName = form[$"OrderItems[{index}].ItemName"];
                        var quantityStr = form[$"OrderItems[{index}].Quantity"];
                        var unitPriceStr = form[$"OrderItems[{index}].UnitPrice"];
                        var itemIdStr = form[$"OrderItems[{index}].Id"];

                        if (!string.IsNullOrEmpty(itemName) &&
                            int.TryParse(quantityStr, out int quantity) &&
                            quantity > 0 &&
                            decimal.TryParse(unitPriceStr, out decimal unitPrice))
                        {
                            var orderItem = new OrderItem
                            {
                                Id = int.TryParse(itemIdStr, out int id) ? id : 0,
                                ItemName = itemName,
                                Quantity = quantity,
                                UnitPrice = unitPrice,
                                OrderId = model.Id
                            };
                            orderItems.Add(orderItem);
                            existingOrder.TotalPrice += orderItem.TotalPrice;
                        }
                    }
                }

                // Remove existing order items and add updated ones
                _context.OrderItems.RemoveRange(existingOrder.OrderItems);
                foreach (var item in orderItems)
                {
                    existingOrder.OrderItems.Add(item);
                }

                // Update table reservation if exists
                if (existingOrder.TableReservation != null && model.TableReservation != null)
                {
                    existingOrder.TableReservation.TableNumber = model.TableReservation.TableNumber;
                    existingOrder.TableReservation.NumberOfGuests = model.TableReservation.NumberOfGuests;
                    existingOrder.TableReservation.ReservationDate = model.TableReservation.ReservationDate;
                    existingOrder.TableReservation.ReservationTime = model.TableReservation.ReservationTime;
                    existingOrder.TableReservation.SpecialRequests = model.TableReservation.SpecialRequests;
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

        [Authorize]
        public async Task<IActionResult> UserHome()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.UserFullName = user?.FullName ?? user?.UserName ?? "Customer";
            return View();
        }


        // ✅ Get Available Tables
        [HttpGet]
        public async Task<JsonResult> GetAvailableTables(DateTime date, string time)
        {
            try
            {
                var reservationTime = TimeSpan.Parse(time);
                var reservationDateTime = date.Add(reservationTime);

                // Get tables that are reserved for the same date and time
                var reservedTables = await _context.Orders
                    .Include(o => o.TableReservation)
                    .Where(o => o.TableReservation != null &&
                               o.TableReservation.ReservationDate.Date == date.Date &&
                               o.TableReservation.ReservationTime == reservationTime &&
                               o.Status != "Cancelled" &&
                               o.Status != "Completed")
                    .Select(o => o.TableReservation.TableNumber)
                    .ToListAsync();

                return Json(reservedTables);
            }
            catch (Exception ex)
            {
                return Json(new List<string>());
            }
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
                new FoodItem { Name = "Iced Tea", Price = 12 },
                 new FoodItem { Name = "Mini Sandwiches", Price = 18 },
        new FoodItem { Name = "Grilled Asparagus", Price = 20 },
        new FoodItem { Name = "Baked Mac & Cheese", Price = 25 },
        new FoodItem { Name = "Leche Flan", Price = 16 },
        new FoodItem { Name = "Chocolate Pudding", Price = 14 }
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