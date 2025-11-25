using System.Collections.Generic;

namespace RestaurantOrderingSystem.Models
{
    public class AdminDashboardViewModel
    {
        public List<Order> Orders { get; set; } = new List<Order>();
        public decimal TotalRevenue { get; set; }
        public int PendingOrders { get; set; }
        public int TodayReservations { get; set; }
        public int TotalOrders { get; set; }

        // Search and Filter properties
        public string? SearchTerm { get; set; }
        public string? StatusFilter { get; set; }
        public string? DateFilter { get; set; }
        public string? ReservationFilter { get; set; }
        public string? PaymentStatusFilter { get; set; }
    }
}