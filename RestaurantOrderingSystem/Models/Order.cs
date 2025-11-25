using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace RestaurantOrderingSystem.Models
{
    public class Order
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string? OrderNo { get; set; }

        [Required]
        public string? Customer { get; set; }

        public List<OrderItem> OrderItems { get; set; } = new();

        [DataType(DataType.Currency)]
        public decimal TotalPrice { get; set; }

        [Required]
        public string? PaymentMethod { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime OrderDate { get; set; } = DateTime.Now;

        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        public int? TableReservationId { get; set; }
        public TableReservation? TableReservation { get; set; }

        // Payment Confirmation Properties - MOVED FROM OrderItem TO Order
        public string? PaymentReference { get; set; }
        public string? OfficialReceiptNo { get; set; }
        public bool IsPaymentConfirmed { get; set; } = false;
        public DateTime? PaymentConfirmedAt { get; set; }
        public string? PaymentConfirmedBy { get; set; }

        public void CalculateTotal()
        {
            TotalPrice = OrderItems.Sum(item => item.TotalPrice);
        }
    }

    public class OrderItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string? ItemName { get; set; }

        [Required]
        [Range(1, 100)]
        public int Quantity { get; set; }

        [DataType(DataType.Currency)]
        public decimal UnitPrice { get; set; }

        [DataType(DataType.Currency)]
        public decimal TotalPrice => UnitPrice * Quantity;

        public int OrderId { get; set; }
        public Order? Order { get; set; }
    }
}