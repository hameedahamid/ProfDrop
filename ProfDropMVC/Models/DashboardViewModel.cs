namespace ProfDropMVC.Models
{
    public class DashboardViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ProfileUrl { get; set; } = string.Empty;
        public List<CourseViewModel> Courses { get; set; } = new();
    }
}
