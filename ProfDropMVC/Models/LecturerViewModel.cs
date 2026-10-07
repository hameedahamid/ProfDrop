namespace ProfDropMVC.Models
{
    // Holds lecturer details returned by the API for use in the MVC application.
    public class LecturerViewModel
    {
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string ProfileUrl { get; set; } = default!;
    }
}
