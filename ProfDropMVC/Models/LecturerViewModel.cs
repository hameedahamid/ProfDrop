namespace ProfDropMVC.Models
{
    // This will help create the authentication cookie to know which lecturer is logged in
    public class LecturerViewModel
    {
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string ProfileUrl { get; set; } = default!;
    }
}
