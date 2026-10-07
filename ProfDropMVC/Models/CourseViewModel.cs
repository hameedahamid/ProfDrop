namespace ProfDropMVC.Models
{
    // Holds course and lecturer details used by the MVC views.
    public class CourseViewModel
    {
        public string CourseCode { get; set; } = default!;
        public string CourseName { get; set; } = default!;
        public string LecturerEmail { get; set; } = default!;
        public string LecturerName { get; set; } = default!;
        public string? ProfileUrl { get; set; }

    }
}

