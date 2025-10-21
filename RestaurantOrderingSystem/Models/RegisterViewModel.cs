using System.ComponentModel.DataAnnotations;

namespace RestaurantOrderingSystem.Models
{
    public class RegisterViewModel
    {
        [Required]

        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string FullName { get; set; } = null!;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = null!;

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match")]

        public string ConfirmPassword { get; set; } = null!;
    }
}
