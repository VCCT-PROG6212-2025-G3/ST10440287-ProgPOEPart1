namespace ProgPOE
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services for the prototype
            builder.Services.AddControllersWithViews();

            // Register prototype services (non-functional)
            builder.Services.AddScoped<IClaimService, ClaimService>();
            builder.Services.AddScoped<IFileService, FileService>();

            // Add session support for user simulation
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline
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

            // Configure routes
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            // Add a simple route for role switching in prototype
            app.MapGet("/switch-role/{role}", (string role) =>
            {
                return $"Switched to {role} role - This is a visual prototype";
            });

            app.Run();
        }
    }
}
