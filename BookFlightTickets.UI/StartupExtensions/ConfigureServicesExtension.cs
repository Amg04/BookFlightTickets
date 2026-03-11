using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.Services;
using BookFlightTickets.Infrastructure.Data.DbContext;
using BookFlightTickets.Infrastructure.Data.DBInitializer;
using BookFlightTickets.Infrastructure.Email;
using BookFlightTickets.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Stripe;

namespace BookFlightTickets.UI.StartupExtensions
{
    public static class ConfigureServicesExtension
    {
        public static IServiceCollection ConfigureServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddControllersWithViews()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            });
            services.AddSignalR();
            services.AddRazorPages();

            #region Stripe

            var stripeSettings = configuration.GetSection("Stripe");
            var stripeSecretKey = stripeSettings["SecretKey"];
            if (!string.IsNullOrEmpty(stripeSecretKey))
            {
                StripeConfiguration.ApiKey = stripeSecretKey;
            }
            #endregion

            #region Dbcontext

            services.AddDbContext<BookFilghtsDbContext>(optionsBuilder =>
            {
                optionsBuilder.UseSqlServer(configuration.GetConnectionString("CS"));
            });

            #endregion

            #region Identity

            services.AddIdentity<AppUser, IdentityRole>(options =>
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

            #endregion

            #region IEmailSender

            services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
            services.AddTransient<IEmailSender, SmtpEmailSender>();

            #endregion

            #region Google

            services.AddAuthentication()
                .AddGoogle(options =>
                {
                    options.ClientId = configuration["Authentication:Google:ClientId"]!;
                    options.ClientSecret = configuration["Authentication:Google:ClientSecret"]!;
                    options.SignInScheme = IdentityConstants.ExternalScheme;
                });

            #endregion

            #region Cache

            //services.AddStackExchangeRedisCache(options =>
            //{
            //    options.Configuration = configuration.GetConnectionString("Redis");
            //    options.InstanceName = "BookFlightTickets";
            //});

            var connectionString = configuration.GetConnectionString("Redis");
            try
            {
                var redis = ConnectionMultiplexer.Connect(connectionString);
                Console.WriteLine("✅ Connected to Redis successfully!");
                redis.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Redis connection failed: {ex.Message}");
            }

            #endregion

            services.AddScoped<IBookingService, BookingService>();
            services.AddScoped<IFlightService, FlightService>();
            services.AddScoped<IStripeSessionService, StripeSessionService>();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<IPdfService, RotativaPdfService>();
            services.AddScoped<IRedisCacheService, RedisCacheService>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IDBInitializer, DBInitializer>();


            services.ConfigureApplicationCookie(option =>
            {
                option.LoginPath = $"/Identity/Account/Login";
                option.LogoutPath = $"/Identity/Account/Logout";
                option.AccessDeniedPath = $"/Identity/Account/AccessDenied";
            });

            return services;
        }
    }
}
