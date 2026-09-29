using System.ComponentModel.DataAnnotations;

namespace ProfDropMVC.Models
{
    public class LoginViewModel
    {
        [Required]
        // Validates email address
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = default!;

        [Required]
        // Tells .NET that this property contains a password
        [DataType(DataType.Password)]
        [StringLength(128)]
        public string Password { get; set; } = default!;
    }
}

