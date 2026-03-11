using BookFlightTickets.Core.Domain.Hubs;
using BookFlightTickets.Infrastructure.Data.DBInitializer;
using BookFlightTickets.UI.StartupExtensions;
using Rotativa.AspNetCore;
using Serilog;

namespace BookFlightTickets.UI
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
           
            builder.Services.ConfigureServices(builder.Configuration);

            #region Serilog

            builder.Host.UseSerilog((HostBuilderContext context, IServiceProvider services, LoggerConfiguration loggerConfiguration) =>
            {
                loggerConfiguration.ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services);
            });

            #endregion        
            
            var app = builder.Build();

            app.UseSerilogRequestLogging();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            #region PDF

            if (!app.Environment.IsEnvironment("Testing"))
            {
                RotativaConfiguration.Setup(app.Environment.WebRootPath, "Rotativa");
            }

            #endregion

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            await SeedDatabaseAsync();

            app.MapRazorPages();
            app.MapControllerRoute(
               name: "default",
               pattern: "{area=Customer}/{controller=Home}/{action=Index}/{id?}");
            app.MapHub<DashboardHub>("/dashboardHub");
            app.Run();

            async Task SeedDatabaseAsync()
            {
                using (var scope = app.Services.CreateScope())
                {
                    var dbInitializer = scope.ServiceProvider.GetRequiredService<IDBInitializer>();
                    await dbInitializer.InitializeAsync();
                }
            }
        }
    }
}
