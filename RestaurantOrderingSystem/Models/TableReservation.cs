using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantOrderingSystem.Models
{
    public class TableReservation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string CustomerName { get; set; } = null!;

        [Required]
        [EmailAddress]
        public string CustomerEmail { get; set; } = null!;

        [Required]
        [Phone]
        public string CustomerPhone { get; set; } = null!;

        [Required]
        public string TableNumber { get; set; } = null!;

        [Required]
        [Range(1, 20)]
        public int NumberOfGuests { get; set; }

        [Required]
        public DateTime ReservationDate { get; set; }

        [Required]
        public TimeSpan ReservationTime { get; set; }

        public string? SpecialRequests { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        // Navigation property to Order (one reservation can have one order)
        public Order? Order { get; set; }
    }
}