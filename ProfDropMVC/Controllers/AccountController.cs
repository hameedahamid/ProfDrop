using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProfDropMVC.Models;
using System.Net;
using System.Security.Claims;

namespace ProfDropMVC.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        // Get the HTTP client used to communicate with the Functions API.
        public AccountController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }


        // Display the login form. This action is available to everyone, including users who are not signed in.
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            // If a lecturer is already signed in, send them to their dashboard instead of showing the login form.
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Dashboard", "Lecturer");
            }

            // Create an empty view model for the login form.
            return View(new LoginViewModel());
        }


        // Process the submitted login form. If the credentials are valid, sign the lecturer in.
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            // Check that the submitted email and password meet the view model's validation rules.
            // If they do not, show the form again with the validation errors.
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var client = _httpClientFactory.CreateClient("ProfDropApi");

                // Send the lecturer's email and password to the API so it can check their login details.
                var response = await client.PostAsJsonAsync("lecturers/login", new
                {
                    email = model.Email,
                    password = model.Password
                });

                // If the API rejects the credentials, show an error on the login form.
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    ModelState.AddModelError("", "Incorrect email or password.");
                    return View(model);
                }

                // Handle any other unsuccessful API response.
                if (!response.IsSuccessStatusCode)
                {
                    ModelState.AddModelError("", "Login is currently unavailable.");
                    return View(model);
                }

                // Read the lecturer's details returned by the API after a successful login.
                var lecturer = await response.Content.ReadFromJsonAsync<LecturerViewModel>();

                // Make sure the API returned a lecturer with an email before continuing.
                if (lecturer == null || string.IsNullOrWhiteSpace(lecturer.Email))
                {
                    ModelState.AddModelError("", "Unable to complete login.");
                    return View(model);
                }

                // Add the lecturer's details to claims, which are stored in their authentication cookie.
                // The claims can be used by the MVC app to identify the lecturer and display their profile.
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier,lecturer.Email),
                    new Claim(ClaimTypes.Name,lecturer.Name),
                    new Claim(ClaimTypes.Email,lecturer.Email),
                    new Claim("ProfileUrl",lecturer.ProfileUrl ?? "")
                };

                // Create an identity and user principal from the lecturer's claims.
                // These identify the lecturer as signed in to the MVC application.
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
               
                var principal = new ClaimsPrincipal(identity);

                // Create a session cookie for the lecturer. IsPersistent = false means the cookie is not set to remain after the browser is closed.
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
                {
                    IsPersistent = false
                });

                // Send the signed-in lecturer to their dashboard.
                return RedirectToAction("Dashboard", "Lecturer");
            }
            // Show an error if the MVC application cannot connect to the Functions API.
            catch (HttpRequestException)
            {
                ModelState.AddModelError("", "Unable to connect to the login service.");
                return View(model);
            }
            // Show an error if the login request takes too long and times out.
            catch (TaskCanceledException)
            {
                ModelState.AddModelError("", "The login request timed out.");
                return View(model);
            }
        }


        // Sign the lecturer out and remove the authentication cookie.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }


        // Show the login page when access to a protected page is denied.
        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied()
        {
            ModelState.AddModelError("", "You do not have permission to view that page.");
            return View("Login", new LoginViewModel());
        }
    }
}

