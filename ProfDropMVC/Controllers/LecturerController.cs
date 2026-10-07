using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProfDropMVC.Models;
using System.Security.Claims;

namespace ProfDropMVC.Controllers
{
    // Require the user to be signed in before accessing any action in this controller.
    [Authorize]
    public class LecturerController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public LecturerController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // Display the signed-in lecturer's dashboard and load their courses.
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            // Read the lecturer's email from their authentication claims.
            // This email is used to request that lecturer's courses from the API.
            string? lecturerEmail = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(lecturerEmail))
            {
                return Forbid();
            }

            // Fill in the lecturer details that were saved in the authentication cookie.
            var model = new DashboardViewModel
            {
                Name = User.Identity?.Name ?? "Lecturer",
                Email = User.FindFirstValue(ClaimTypes.Email) ?? "",
                ProfileUrl = User.FindFirstValue("ProfileUrl") ?? ""
            };

            try
            {
                var client = _httpClientFactory.CreateClient("ProfDropApi");

                // Request courses for this lecturer and store them in the dashboard model.
                // Use an empty list if the API does not return any courses.
                model.Courses = await client.GetFromJsonAsync<List<CourseViewModel>>($"lecturers/{Uri.EscapeDataString(lecturerEmail)}/courses") ?? new List<CourseViewModel>();
            }
            // Show an error if the courses could not be loaded.
            catch
            {
                ModelState.AddModelError("", "Unable to load your courses.");
            }
            // Send the lecturer details and any courses to the dashboard view.
            return View(model);
        }


        // Display feedback for a course and optionally filter it by category.
        [HttpGet]
        public async Task<IActionResult> Feedback(string courseCode, string? category)
        {
            // Read the lecturer's email from their authentication claims.
            // The API uses it to check that the course belongs to this lecturer.
            string? lecturerEmail = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(lecturerEmail))
            {
                return Forbid();
            }

            // Return to the dashboard if no course code was supplied.
            if (string.IsNullOrWhiteSpace(courseCode))
            {
                return RedirectToAction("Dashboard");
            }

            // Pass the course and selected category to the view so it can display them.
            ViewBag.CourseCode = courseCode;
            ViewBag.SelectedCategory = category ?? "All";

            try
            {
                var client = _httpClientFactory.CreateClient("ProfDropApi");

                // Request feedback for this course through the lecturer-specific API route.
                // The API checks whether this lecturer is allowed to view the course.
                var response = await client.GetAsync($"lecturers/{Uri.EscapeDataString(lecturerEmail)}" + $"/courses/{Uri.EscapeDataString(courseCode)}/feedback");

                // Stop lecturers from viewing courses that are not theirs.
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return Forbid();
                }

                // Show an error and an empty feedback list if the API request failed.
                if (!response.IsSuccessStatusCode)
                {
                    ModelState.AddModelError("", "Unable to load the feedback.");
                    return View(new List<FeedbackViewModel>());
                }

                // Read the feedback returned by the API.
                // Use an empty list if the API does not return any feedback.
                var feedback = await response.Content.ReadFromJsonAsync<List<FeedbackViewModel>>() ?? new List<FeedbackViewModel>();

                // Sort the feedback so the most recently submitted entries appear first.
                feedback = feedback.OrderByDescending(f => f.SubmittedAt).ToList();

                // Filter by category if the lecturer selects one.
                if (!string.IsNullOrWhiteSpace(category) && category != "All")
                {
                    feedback = feedback.Where(f => f.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
                }
                // Send the sorted and filtered feedback to the view.
                return View(feedback);
            }
            // Show an error and an empty feedback list if the API cannot be reached.
            catch
            {
                ModelState.AddModelError("", "Unable to connect to the feedback service.");
                return View(new List<FeedbackViewModel>());
            }
        }
    }
}

