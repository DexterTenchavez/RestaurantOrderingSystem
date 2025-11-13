using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace RestaurantOrderingSystem.Models
{
    public class CreateOrderViewModel
    {
        public string? OrderNo { get; set; }

        [Required]
        public string? Customer { get; set; }

        public List<OrderItemViewModel> OrderItems { get; set; } = new List<OrderItemViewModel>();

        [Required]
        public string? PaymentMethod { get; set; }

        public bool NeedTableReservation { get; set; }

        public string? TableNumber { get; set; }

        public DateTime? ReservationDate { get; set; }

        public TimeSpan? ReservationTime { get; set; }

        public int? NumberOfGuests { get; set; }

        public string? SpecialRequests { get; set; }

        public decimal TotalPrice => OrderItems.Sum(item => item.TotalPrice);
    }

    public class OrderItemViewModel
    {
        public string? ItemName { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice => UnitPrice * Quantity;
    }
}