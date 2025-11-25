using System.Collections.Generic;

namespace RestaurantOrderingSystem.Models
{
    public class UserDashboardViewModel
    {
        public List<Order> Orders { get; set; } = new List<Order>();
        public ApplicationUser User { get; set; } = null!;

        // Search and Filter properties
        public string? SearchTerm { get; set; }
        public string? StatusFilter { get; set; }
        public string? DateFilter { get; set; }
        public string? ReservationFilter { get; set; }
        public string? PaymentStatusFilter { get; set; }
    }
}