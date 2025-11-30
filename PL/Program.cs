using BAL.model;
using BLLProject.Interfaces;
using BLLProject.Repositories;
using DAL.Data;
using DAL.DBInitializer;
using DAL.models;
using DALProject.DBInitializer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using Stripe;

namespace PL
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();


            builder.Services.AddRazorPages().AddRazorRuntimeCompilation();

            // using Stripe
            var stripeSettings = builder.Configuration.GetSection("Stripe");
            StripeConfiguration.ApiKey = stripeSettings["SecretKey"];

            #region Dbcontext

            builder.Services.AddDbContext<BookFilghtsDbContext>(optionsBuilder =>
            {
                optionsBuilder.UseSqlServer(builder.Configuration.GetConnectionString("CS"));
            });

            #endregion

            builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
            {
                options.Password.RequiredUniqueChars = 2;
                options.Password.RequireDigit = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequiredLength = 5;

                options.SignIn.RequireConfirmedAccount = false;
                options.SignIn.RequireConfirmedEmail = false;
                options.SignIn.RequireConfirmedPhoneNumber = false;
            }).AddEntityFrameworkStores<BookFilghtsDbContext>()
                .AddDefaultTokenProviders();

            builder.Services.AddRazorPages();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<IDBInitializer, DBInitializer>();
            builder.Services.AddSingleton<IEmailSender, NoOpEmailSender>();

            builder.Services.ConfigureApplicationCookie(option =>
            {
                option.LoginPath = $"/Identity/Account/Login";
                option.LogoutPath = $"/Identity/Account/Logout";
                option.AccessDeniedPath = $"/Identity/Account/AccessDenied";
            });

            // PDF
            RotativaConfiguration.Setup("wwwroot", wkhtmltopdfRelativePath: "Rotativa");


            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

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

            app.Run();

            async Task SeedDatabaseAsync()
            {
                using (var scope = app.Services.CreateScope())
                {
                    var dbInitializer = scope.ServiceProvider.GetRequiredService<IDBInitializer>();
                    await dbInitializer.Initialize();
                }
            }
        }
    }
}
