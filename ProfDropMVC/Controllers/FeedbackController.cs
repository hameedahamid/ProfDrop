using Microsoft.AspNetCore.Mvc;
using ProfDropMVC.Models;
using System.Net.Http.Json;

namespace ProfDropMVC.Controllers
{
    public class FeedbackController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public FeedbackController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // Displays the student feedback form
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("ProfDropApi");

                var model = new FeedbackViewModel
                {
                    Courses = await client.GetFromJsonAsync<List<CourseViewModel>>("courses")
                        ?? new List<CourseViewModel>()
                };

                return View(model);
            }
            catch
            {
                ModelState.AddModelError("", "Unable to load the courses.");
                return View(new FeedbackViewModel());
            }
        }

        // Submits anonymous student feedback
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(FeedbackViewModel model)
        {
            if (!ModelState.IsValid)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient("ProfDropApi");

                    model.Courses = await client.GetFromJsonAsync<List<CourseViewModel>>("courses")
                        ?? new List<CourseViewModel>();
                }
                catch
                {
                    ModelState.AddModelError("", "Unable to load the courses.");
                }

                return View(model);
            }

            try
            {
                var client = _httpClientFactory.CreateClient("ProfDropApi");

                var feedback = new
                {
                    courseCode = model.CourseCode,
                    category = model.Category,
                    message = model.Message
                };

                var response = await client.PostAsJsonAsync("feedback", feedback);

                if (!response.IsSuccessStatusCode)
                {
                    ModelState.AddModelError("", "Unable to submit your feedback.");

                    model.Courses = await client.GetFromJsonAsync<List<CourseViewModel>>("courses")
                        ?? new List<CourseViewModel>();

                    return View(model);
                }

                return RedirectToAction("Success");
            }
            catch
            {
                ModelState.AddModelError("", "Unable to connect to the feedback service.");

                try
                {
                    var client = _httpClientFactory.CreateClient("ProfDropApi");

                    model.Courses = await client.GetFromJsonAsync<List<CourseViewModel>>("courses")
                        ?? new List<CourseViewModel>();
                }
                catch
                {
                    ModelState.AddModelError("", "Unable to reload the courses.");
                }

                return View(model);
            }
        }

        // Displays the feedback submission success page
        [HttpGet]
        public IActionResult Success()
        {
            return View();
        }
    }
}
