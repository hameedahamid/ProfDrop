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


        // Display the lecturer login page.
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            // Send logged-in lecturers straight to their dashboard.
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Dashboard", "Lecturer");
            }

            return View(new LoginViewModel());
        }


        // Check the lecturer's login details and create the authentication cookie.
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            // Return the form if validation fails.
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var client = _httpClientFactory.CreateClient("ProfDropApi");

                // Send the email and password to the Azure Function.
                var response = await client.PostAsJsonAsync("lecturers/login", new
                {
                    email = model.Email,
                    password = model.Password
                });

                // Show a simple message when the login details are incorrect.
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

                // Get the lecturer details returned by the API.
                var lecturer = await response.Content.ReadFromJsonAsync<LecturerViewModel>();

                if (lecturer == null || string.IsNullOrWhiteSpace(lecturer.Email))
                {
                    ModelState.AddModelError("", "Unable to complete login.");
                    return View(model);
                }

                // Store the lecturer's details in secure authentication claims.
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier,lecturer.Email),
                    new Claim(ClaimTypes.Name,lecturer.Name),
                    new Claim(ClaimTypes.Email,lecturer.Email),
                    new Claim("ProfileUrl",lecturer.ProfileUrl ?? "")
                };

                // Create the lecturer's authentication identity.
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                // Sign the lecturer in using a non-persistent cookie.
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
                {
                    IsPersistent = false
                });

                return RedirectToAction("Dashboard", "Lecturer");
            }
            catch (HttpRequestException)
            {
                ModelState.AddModelError("", "Unable to connect to the login service.");
                return View(model);
            }
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

