using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        // One order can have multiple order items
        public List<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        [DataType(DataType.Currency)]
        public decimal TotalPrice { get; set; }

        [Required]
        public string? PaymentMethod { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime OrderDate { get; set; } = DateTime.Now;

        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        // Table reservation reference
        public int? TableReservationId { get; set; }
        public TableReservation? TableReservation { get; set; }

        // Helper method to calculate total
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