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
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllersWithViews();

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddScoped<IClaimService, ClaimService>();
            builder.Services.AddScoped<IFileService, FileService>();

            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            builder.Services.AddHttpContextAccessor();

            var app = builder.Build();

            // Initialize database
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var logger = services.GetRequiredService<ILogger<Program>>();

                try
                {
                    var context = services.GetRequiredService<ApplicationDbContext>();

                    logger.LogInformation("Deleting existing database...");
                    context.Database.EnsureDeleted();

                    logger.LogInformation("Creating new database...");
                    context.Database.EnsureCreated();

                    logger.LogInformation("Seeding database...");
                    SeedDatabase(context, logger);

                    logger.LogInformation("Database initialized successfully!");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred creating/seeding the database.");
                    logger.LogError($"Inner Exception: {ex.InnerException?.Message}");
                    throw; // Re-throw to see the full error
                }
            }

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseSession();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }

        private static void SeedDatabase(ApplicationDbContext context, ILogger logger)
        {
            try
            {
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

                // Optional: Seed a test claim
                if (!context.Claims.Any())
                {
                    logger.LogInformation("Seeding test claim...");

                    var testClaim = new Claim
                    {
                        LecturerId = 1,
                        MonthYear = "2024-09",
                        HoursWorked = 100.0m,
                        HourlyRate = 450.00m,
                        Status = ClaimStatus.Approved,
                        SubmissionDate = DateTime.Now.AddDays(-30),
                        CoordinatorApprovalDate = DateTime.Now.AddDays(-25),
                        ManagerApprovalDate = DateTime.Now.AddDays(-20),
                        LecturerNotes = "September teaching hours",
                        CoordinatorNotes = "Approved by coordinator",
                        ManagerNotes = "Final approval granted"
                    };

                    context.Claims.Add(testClaim);
                    context.SaveChanges();

                    logger.LogInformation("Test claim seeded successfully");
                }
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
//Refernces
// - ASP.NET Core Documentation: https://docs.microsoft.com/en-us/aspnet/core/?view=aspnetcore-6.0
// - Entity Framework Core Documentation: https://docs.microsoft.com/en-us/ef/core/
// - Bootstrap Documentation: https://getbootstrap.com/docs/5.1/getting-started/introduction/
// - jQuery Documentation: https://api.jquery.com/
// - ChatGPT https://chatgpt.com/share/68cb0efe-b6d4-8001-8e7b-dd56bf04cc8a
