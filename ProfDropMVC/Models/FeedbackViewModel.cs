using System.ComponentModel.DataAnnotations;

namespace ProfDropMVC.Models
{
    public class FeedbackViewModel
    {
        [Required(ErrorMessage = "Please select a course.")]
        public string CourseCode { get; set; } = default!;

        [Required(ErrorMessage = "Please select a category.")]
        public string Category { get; set; } = default!;
        [Required(ErrorMessage = "Please enter your feedback.")]
        [StringLength(2000, ErrorMessage = "Feedback must be 2000 characters or fewer.")]

        public string Message { get; set; } = default!;
        public DateTimeOffset? SubmittedAt { get; set; }
        public List<CourseViewModel> Courses { get; set; } = new List<CourseViewModel>();
    }
}
