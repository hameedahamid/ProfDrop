using System.ComponentModel.DataAnnotations;

namespace ProfDropMVC.Models
{
    // Holds the email and password submitted through the lecturer login form.
    public class LoginViewModel
    {
        // The lecturer must enter an email address.
        // It cannot be left blank, must have a valid email format, and can contain up to 100 characters.
        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = default!;

        // The lecturer must enter a password.
        // It cannot be left blank, is treated as password data by the form, and can contain up to 128 characters.
        [Required]
        [DataType(DataType.Password)]
        [StringLength(128)]
        public string Password { get; set; } = default!;
    }
}

