using Microsoft.EntityFrameworkCore;
using ProgPOE.Data;
using ProgPOE.Models;
using ProgPOE.Services;

namespace ProgPOE
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Create builder for configuring services and middleware
            var builder = WebApplication.CreateBuilder(args);

            // Add MVC controllers with views
            builder.Services.AddControllersWithViews();

            // Configure Entity Framework Core with SQLite using connection string
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Register services for dependency injection
            builder.Services.AddScoped<IClaimService, ClaimService>();
            builder.Services.AddScoped<IFileService, FileService>();

            // Configure session options
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout
                options.Cookie.HttpOnly = true; // Prevent client-side scripts from accessing cookie
                options.Cookie.IsEssential = true; // Required for GDPR compliance
            });

            // Make HttpContext available in services
            builder.Services.AddHttpContextAccessor();

            // Build the application
            var app = builder.Build();

            // Initialize database and uploads directory
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var logger = services.GetRequiredService<ILogger<Program>>();
                var environment = services.GetRequiredService<IWebHostEnvironment>();

                try
                {
                    var context = services.GetRequiredService<ApplicationDbContext>();

                    // Delete existing database (for development/testing purposes)
                    logger.LogInformation("Deleting existing database...");
                    context.Database.EnsureDeleted();

                    // Create new database
                    logger.LogInformation("Creating new database...");
                    context.Database.EnsureCreated();

                    // Seed database with default users
                    logger.LogInformation("Seeding database...");
                    SeedDatabase(context, logger);

                    logger.LogInformation("Database initialized successfully!");

                    // Ensure uploads directory exists
                    var uploadsPath = Path.Combine(environment.ContentRootPath, "uploads");
                    if (!Directory.Exists(uploadsPath))
                    {
                        Directory.CreateDirectory(uploadsPath);
                        logger.LogInformation($"Created uploads directory at: {uploadsPath}");
                    }
                    else
                    {
                        logger.LogInformation($"Uploads directory exists at: {uploadsPath}");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred creating/seeding the database.");
                    logger.LogError($"Inner Exception: {ex.InnerException?.Message}");
                    throw;
                }
            }

            // Configure middleware pipeline
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error"); // Custom error page
                app.UseHsts(); // Enforce HTTPS in production
            }

            app.UseHttpsRedirection(); // Redirect HTTP to HTTPS
            app.UseStaticFiles(); // Serve static files (CSS, JS, images)

            app.UseRouting(); // Enable endpoint routing

            app.UseSession(); // Enable session state
            app.UseAuthorization(); // Enable authorization middleware

            // Map default controller route: Home/Index
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            // Run the application
            app.Run();
        }

        // Seed the database with default users
        private static void SeedDatabase(ApplicationDbContext context, ILogger logger)
        {
            try
            {
                // Check if users already exist
                if (!context.Users.Any())
                {
                    logger.LogInformation("Seeding users...");

                    var users = new List<User>
                    {
                        new User
                        {
                            Username = "john.smith",
                            Email = "john.smith@university.ac.za",
                            FirstName = "John",
                            LastName = "Smith",
                            Role = UserRole.Lecturer,
                            Department = "Computer Science",
                            DefaultHourlyRate = 450.00m,
                            CreatedDate = DateTime.Now,
                            IsActive = true
                        },
                        new User
                        {
                            Username = "jane.wilson",
                            Email = "jane.wilson@university.ac.za",
                            FirstName = "Jane",
                            LastName = "Wilson",
                            Role = UserRole.ProgrammeCoordinator,
                            Department = "Computer Science",
                            DefaultHourlyRate = null,
                            CreatedDate = DateTime.Now,
                            IsActive = true
                        },
                        new User
                        {
                            Username = "mike.johnson",
                            Email = "mike.johnson@university.ac.za",
                            FirstName = "Mike",
                            LastName = "Johnson",
                            Role = UserRole.AcademicManager,
                            Department = "Computer Science",
                            DefaultHourlyRate = null,
                            CreatedDate = DateTime.Now,
                            IsActive = true
                        }
                    };

                    context.Users.AddRange(users);
                    context.SaveChanges();

                    logger.LogInformation($"Seeded {users.Count} users successfully");
                }

                logger.LogInformation("Database seeding completed - no test claims added");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in SeedDatabase");
                logger.LogError($"Inner Exception: {ex.InnerException?.Message}");
                throw;
            }
        }
    }
}

// References
// - ASP.NET Core Documentation: https://docs.microsoft.com/en-us/aspnet/core/?view=aspnetcore-6.0
// - Entity Framework Core Documentation: https://docs.microsoft.com/en-us/ef/core/
// - Bootstrap Documentation: https://getbootstrap.com/docs/5.1/getting-started/introduction/
// - jQuery Documentation: https://api.jquery.com/
// - File Upload Security: https://owasp.org/www-community/vulnerabilities/Unrestricted_File_Upload
// - ChatGPT: https://chatgpt.com/share/68cb0efe-b6d4-8001-8e7b-dd56bf04cc8a
