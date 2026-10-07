using System.ComponentModel.DataAnnotations;

namespace ProfDropMVC.Models
{
    // Holds the feedback form details and the course list shown to students.
    public class FeedbackViewModel
    {
        // Stores the course the student selected. A course must be selected.
        [Required(ErrorMessage = "Please select a course.")]
        public string CourseCode { get; set; } = default!;

        // Stores the feedback category chosen by the student. A category must be selected.
        [Required(ErrorMessage = "Please select a category.")]
        public string Category { get; set; } = default!;

        // Stores the student's feedback message. It is required and can be up to 2,000 characters.
        [Required(ErrorMessage = "Please enter your feedback.")]
        [StringLength(2000, ErrorMessage = "Feedback must be 2000 characters or fewer.")]
        public string Message { get; set; } = default!;

        public DateTimeOffset? SubmittedAt { get; set; }

        // Holds the courses available for students to select from in the feedback form.
        public List<CourseViewModel> Courses { get; set; } = new List<CourseViewModel>();
    }
}
