namespace ProfDropMVC.Models
{
    // Holds the lecturer's details and courses displayed on the dashboard.
    public class DashboardViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ProfileUrl { get; set; } = string.Empty;

        // Holds the courses assigned to the lecturer.
        public List<CourseViewModel> Courses { get; set; } = new();
    }
}
