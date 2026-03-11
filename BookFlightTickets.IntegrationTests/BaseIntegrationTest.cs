using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.Infrastructure.Data.DbContext;
using BookFlightTickets.Infrastructure.Data.DBInitializer;
using BookFlightTickets.UI;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace BookFlightTickets.IntegrationTests
{
    public abstract class BaseIntegrationTest : IClassFixture<WebApplicationFactory<Program>>
    {
        protected WebApplicationFactory<Program> Factory { get; private set; }
        private readonly string _databaseName; // Unique per test class
        protected BaseIntegrationTest(WebApplicationFactory<Program> factory)
        {
            _databaseName = Guid.NewGuid().ToString(); // unique name
            Factory = factory;
            InitializeFactory(services => { });
        }

        protected void InitializeFactory(Action<IServiceCollection> configureServices)
        {
            Factory = Factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureTestServices(services =>
                {
                    var dbContextDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<BookFilghtsDbContext>));
                    if (dbContextDescriptor != null)
                        services.Remove(dbContextDescriptor);

                    services.AddDbContext<BookFilghtsDbContext>(options =>
                        options.UseInMemoryDatabase(_databaseName));

                    var dbInitializerMock = new Mock<IDBInitializer>();
                    dbInitializerMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
                    services.AddScoped<IDBInitializer>(_ => dbInitializerMock.Object);

                    configureServices(services);
                });
            });
        }

        private HttpClient CreateClientWithAuth<THandler>(string schemeName) where THandler : AuthenticationHandler<AuthenticationSchemeOptions>
        {
            var factoryWithAuth = Factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = schemeName;
                        options.DefaultChallengeScheme = schemeName;
                    }).AddScheme<AuthenticationSchemeOptions, THandler>(schemeName, _ => { });
                });
            });

            return factoryWithAuth.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        protected HttpClient CreateAuthenticatedClient()
        {
            return CreateClientWithAuth<TestAuthHandler>("Test");
        }

        protected HttpClient CreateAdminClient()
        {
            return CreateClientWithAuth<TestAdminAuthHandler>("TestAdmin");
        }

        protected HttpClient CreateUnauthenticatedClient()
        {
            return Factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        protected async Task ExecuteWithDbContextAsync(Func<BookFilghtsDbContext, Task> action)
        {
            using var scope = Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();

            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();

            await action(dbContext);
        }
    }

    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
            : base(options, logger, encoder, clock) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "test-user-id") };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    public class TestAdminAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAdminAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
            : base(options, logger, encoder, clock) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "test-admin-id"),
                new Claim(ClaimTypes.Role, SD.Admin)
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }


}