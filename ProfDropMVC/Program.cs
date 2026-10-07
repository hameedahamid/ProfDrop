using Microsoft.AspNetCore.Authentication.Cookies;

namespace ProfDropMVC
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            // Configure cookie-based authentication for lecturer sign-in.
            builder.Services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    // Send users who need to sign in to the lecturer login page.
                    options.LoginPath = "/Account/Login";

                    // Send signed-in users to this page if they try to access something they are not allowed to view.
                    options.AccessDeniedPath = "/Account/AccessDenied";

                    // Sign the user out after 30 minutes without extending their session.
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);

                    // Extend the cookie's expiry while the user is active.
                    options.SlidingExpiration = true;
                });

            // Register a named HTTP client for requests from the MVC app to the ProfDrop Functions API.
            builder.Services.AddHttpClient("ProfDropApi", client =>
            {
                // Set the API's base address from configuration.
                // Stop the application from starting if the address is missing.
                client.BaseAddress = new Uri(builder.Configuration["ProfDropApi:BaseUrl"] ?? throw new InvalidOperationException("ProfDropApi:BaseUrl is missing."));
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            // Check the authentication cookie and set the current user's identity.
            app.UseAuthentication();
            // Apply authorization rules to the incoming request.
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
