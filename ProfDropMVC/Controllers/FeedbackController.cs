using Microsoft.AspNetCore.Mvc;
using ProfDropMVC.Models;
using System.Net.Http.Json;

namespace ProfDropMVC.Controllers
{
    public class FeedbackController : Controller
    {
        // Used to create the HTTP client that communicates with the ProfDrop API
        private readonly IHttpClientFactory _httpClientFactory;

        // Constructor used to receive the HTTP client factory
        public FeedbackController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // GET endpoint used to display the student feedback form
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                // Create the HTTP client configured to communicate with the ProfDrop API
                var client = _httpClientFactory.CreateClient("ProfDropApi");

                // Create the feedback view model
                var model = new FeedbackViewModel
                {
                    // Get all available courses from the ProfDrop API
                    // These courses are displayed in the feedback form for students to select from
                    Courses = await client.GetFromJsonAsync<List<CourseViewModel>>("courses") ?? new List<CourseViewModel>()
                };

                // Display the feedback form view with the available courses
                return View(model);
            }
            catch
            {
                // Show an error if the courses could not be loaded from the ProfDrop API
                ModelState.AddModelError("", "Unable to load the courses.");
                // Return an empty feedback form
                return View(new FeedbackViewModel());
            }
        }

        // POST endpoint used when a student submits anonymous feedback
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(FeedbackViewModel model)
        {
            // Check whether the feedback form passed the MVC validation rules
            if (!ModelState.IsValid)
            {
                try
                {
                    // Create the HTTP client used to communicate with the ProfDrop API
                    var client = _httpClientFactory.CreateClient("ProfDropApi");

                    // // Reload the courses so they are still available in the form when validation fails
                    model.Courses = await client.GetFromJsonAsync<List<CourseViewModel>>("courses") ?? new List<CourseViewModel>();
                }
                catch
                {
                    // Show an error if the courses could not be loaded from the ProfDrop API again
                    ModelState.AddModelError("", "Unable to load the courses.");
                }

                // Return the form with the validation errors
                return View(model);
            }

            try
            {
                // Create the HTTP client used to communicate with the ProfDrop API
                var client = _httpClientFactory.CreateClient("ProfDropApi");

                // Create the JSON object that will be sent to the ProfDrop API
                var feedback = new
                {
                    // Send the selected course code
                    courseCode = model.CourseCode,
                    // Send the selected feedback category
                    category = model.Category,
                    // Send the anonymous feedback message
                    message = model.Message
                };

                // Send the feedback to the CreateFeedback API endpoint
                var response = await client.PostAsJsonAsync("feedback", feedback);

                // Check whether ProfDrop API successfully saved the feedback
                if (!response.IsSuccessStatusCode)
                {
                    // Show an error if the API returned an unsuccessful status code
                    ModelState.AddModelError("", "Unable to submit your feedback.");

                    // Reload the courses so that the student can try to submit the feedback again
                    model.Courses = await client.GetFromJsonAsync<List<CourseViewModel>>("courses") ?? new List<CourseViewModel>();

                    // Return to the feedback form
                    return View(model);
                }

                // Redirect the student to the success page after successful feedback submission
                return RedirectToAction("Success");
            }
            catch
            {
                // Show an error if the MVC application cannot connect to the ProfDrop API
                ModelState.AddModelError("", "Unable to connect to the feedback service.");

                try
                {
                    // Create the HTTP client again so the course can be reloaded
                    var client = _httpClientFactory.CreateClient("ProfDropApi");

                    // Reload the available courses for the feedback form
                    model.Courses = await client.GetFromJsonAsync<List<CourseViewModel>>("courses") ?? new List<CourseViewModel>();
                }
                catch
                {
                    // Show an additional error if the courses also cannot be loaded
                    ModelState.AddModelError("", "Unable to reload the courses.");
                }

                // Return the feedback form with the error messages
                return View(model);
            }
        }

        // GET endpoint used to display the successful feedback submission page
        [HttpGet]
        public IActionResult Success()
        {
            // Display the success view
            return View();
        }
    }
}
