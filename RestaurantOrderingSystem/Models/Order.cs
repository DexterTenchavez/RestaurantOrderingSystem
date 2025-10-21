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

        [Required]
        public string? Item { get; set; } 

        [Required]
        [Range(1, 100, ErrorMessage = "Quantity must be between 1 and 100.")]
        public int Quantity { get; set; }

      
        [DataType(DataType.Currency)]
        public decimal TotalPrice { get; set; }

        [Required]
       
        public string? PaymentMethod { get; set; }
    }
}
