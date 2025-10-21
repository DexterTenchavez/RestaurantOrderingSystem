using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantOrderingSystem.Models;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace RestaurantOrderingSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public HomeController(
            ILogger<HomeController> logger,
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
        }

        // ✅ Login Page
        [AllowAnonymous]
        public IActionResult Index() => View();

        // ✅ Login (POST)
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(LoginViewModel model)
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
                    return RedirectToAction("Dashboard");
                else
                    return RedirectToAction("Create");
            }

            ModelState.AddModelError("", "Invalid login attempt.");
            return View(model);
        }

        // ✅ Register Page
        [AllowAnonymous]
        public IActionResult Register() => View();

        // ✅ Register (POST)
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
                    FullName = model.FullName
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Ensure roles exist
                    if (!await _roleManager.RoleExistsAsync("Admin"))
                        await _roleManager.CreateAsync(new IdentityRole("Admin"));
                    if (!await _roleManager.RoleExistsAsync("Customer"))
                        await _roleManager.CreateAsync(new IdentityRole("Customer"));

                    // First user becomes admin automatically
                    if (_userManager.Users.Count() == 1)
                        await _userManager.AddToRoleAsync(user, "Admin");
                    else
                        await _userManager.AddToRoleAsync(user, "Customer");

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Index");
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        // ✅ Dashboard (Admin only)
        [Authorize(Roles = "Admin")]
        public IActionResult Dashboard()
        {
            var orders = _context.Order
                .OrderByDescending(o => o.Id)
                .ToList();

            return View(orders);
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

        // ✅ Create Order (GET)
        [Authorize]
        public IActionResult Create()
        {
            var lastOrder = _context.Order
                .OrderByDescending(o => o.Id)
                .FirstOrDefault();

            int nextNumber = (lastOrder == null) ? 1001 : lastOrder.Id + 1;

            var model = new Order
            {
                OrderNo = $"ORD-{nextNumber:D4}"
            };

            return View(model);
        }

        // ✅ Create Order (POST)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Order model)
        {
            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(model.OrderNo))
                {
                    int nextId = _context.Order.Count() + 1;
                    model.OrderNo = $"ORD-{1000 + nextId}";
                }

                // Auto pricing logic (example)
                decimal pricePerItem = 0;
                switch (model.Item)
                {
                    case "Fried Chicken":
                        pricePerItem = 25;
                        break;
                    case "Burger Steak":
                        pricePerItem = 30;
                        break;
                    case "Spaghetti":
                        pricePerItem = 20;
                        break;
                    case "Pancit Canton":
                        pricePerItem = 18;
                        break;
                    case "Sisig":
                        pricePerItem = 28;
                        break;
                    case "Lechon Kawali":
                        pricePerItem = 32;
                        break;
                    case "Adobo":
                        pricePerItem = 27;
                        break;
                    case "Beef Tapa":
                        pricePerItem = 35;
                        break;
                    case "Sinigang":
                        pricePerItem = 33;
                        break;
                    case "Halo-Halo":
                        pricePerItem = 15;
                        break;
                    default:
                        pricePerItem = 0;
                        break;
                }


                model.TotalPrice = pricePerItem * model.Quantity;

                _context.Add(model);
                await _context.SaveChangesAsync();

                return RedirectToAction("Receipt", new { id = model.Id });
            }

            return View(model);
        }

        // ✅ Receipt Page
        [Authorize]
        public IActionResult Receipt(int id)
        {
            var order = _context.Order.FirstOrDefault(o => o.Id == id);
            if (order == null)
                return NotFound();

            return View(order);
        }

        // ✅ Edit Order (Admin only)
        [Authorize(Roles = "Admin")]
        public IActionResult Edit(int id)
        {
            var order = _context.Order.FirstOrDefault(o => o.Id == id);
            if (order == null)
                return NotFound();

            return View(order);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Order model)
        {
            if (ModelState.IsValid)
            {
                var order = await _context.Order.FindAsync(model.Id);
                if (order == null)
                    return NotFound();

                order.Customer = model.Customer;
                order.Item = model.Item;
                order.Quantity = model.Quantity;
                order.TotalPrice = model.TotalPrice;
                order.PaymentMethod = model.PaymentMethod;

                await _context.SaveChangesAsync();
                return RedirectToAction("Dashboard");
            }

            return View(model);
        }

        // ✅ Delete Order (Admin only)
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _context.Order.FindAsync(id);
            if (order == null)
                return NotFound();

            _context.Order.Remove(order);
            await _context.SaveChangesAsync();
            return RedirectToAction("Dashboard");
        }

        // ✅ Privacy Page
        public IActionResult Privacy() => View();

        // ✅ Error Page
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
