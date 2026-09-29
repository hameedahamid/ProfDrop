using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProfDropMVC.Models;
using System.Security.Claims;

namespace ProfDropMVC.Controllers
{
    [Authorize]
    public class LecturerController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public LecturerController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // Displays the logged-in lecturer's dashboard and courses
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            // Get the lecturer's email from the secure login cookie
            string? lecturerEmail = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(lecturerEmail))
            {
                return Forbid();
            }

            // Build the dashboard using details stored in the login cookie
            var model = new DashboardViewModel
            {
                Name = User.Identity?.Name ?? "Lecturer",
                Email = User.FindFirstValue(ClaimTypes.Email) ?? "",
                ProfileUrl = User.FindFirstValue("ProfileUrl") ?? ""
            };

            try
            {
                var client = _httpClientFactory.CreateClient("ProfDropApi");

                // Load only the courses belonging to this lecturer
                model.Courses = await client.GetFromJsonAsync<List<CourseViewModel>>($"lecturers/{Uri.EscapeDataString(lecturerEmail)}/courses") ?? new List<CourseViewModel>();
            }
            catch
            {
                ModelState.AddModelError("", "Unable to load your courses.");
            }
            return View(model);
        }


        // Displays feedback for one course belonging to the lecturer
        [HttpGet]
        public async Task<IActionResult> Feedback(string courseCode, string? category)
        {
            // Get the lecturer's email from the login cookie
            string? lecturerEmail = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(lecturerEmail))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(courseCode))
            {
                return RedirectToAction("Dashboard");
            }

            ViewBag.CourseCode = courseCode;
            ViewBag.SelectedCategory = category ?? "All";

            try
            {
                var client = _httpClientFactory.CreateClient("ProfDropApi");

                // Request feedback through the lecturer-specific API route
                var response = await client.GetAsync($"lecturers/{Uri.EscapeDataString(lecturerEmail)}" + $"/courses/{Uri.EscapeDataString(courseCode)}/feedback");

                // Stop lecturers from viewing courses that are not theirs
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return Forbid();
                }

                if (!response.IsSuccessStatusCode)
                {
                    ModelState.AddModelError("", "Unable to load the feedback.");
                    return View(new List<FeedbackViewModel>());
                }

                var feedback = await response.Content.ReadFromJsonAsync<List<FeedbackViewModel>>() ?? new List<FeedbackViewModel>();

                // Show newest feedback first
                feedback = feedback.OrderByDescending(f => f.SubmittedAt).ToList();

                // Filter by category when the lecturer selects one
                if (!string.IsNullOrWhiteSpace(category) && category != "All")
                {
                    feedback = feedback.Where(f => f.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
                }
                return View(feedback);
            }
            catch
            {
                ModelState.AddModelError("", "Unable to connect to the feedback service.");
                return View(new List<FeedbackViewModel>());
            }
        }
    }
}

