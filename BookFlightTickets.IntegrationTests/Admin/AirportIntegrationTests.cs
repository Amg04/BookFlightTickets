using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.ViewModels;
using BookFlightTickets.Infrastructure.Data.DbContext;
using BookFlightTickets.UI;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BookFlightTickets.IntegrationTests.Admin
{
    public class AirportIntegrationTests : BaseIntegrationTest
    {
        private readonly Mock<IRedisCacheService> _cacheServiceMock;

        public AirportIntegrationTests(WebApplicationFactory<Program> factory) : base(factory)
        {
            _cacheServiceMock = new Mock<IRedisCacheService>();

            InitializeFactory(services =>
            {
                services.AddScoped(_ => _cacheServiceMock.Object);
            });
        }

        #region Index (GET)

        [Fact]
        public async Task Index_WhenAirportsExist_ReturnsViewWithAirports()
        {
            // Arrange
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                dbContext.Airports.Add(new Airport { Name = "Cairo International", Code = "CAI", City = "Cairo", Country = "Egypt" });
                dbContext.Airports.Add(new Airport { Name = "Dubai International", Code = "DXB", City = "Dubai", Country = "UAE" });
                await dbContext.SaveChangesAsync();
            });

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<List<AirportViewModel>>>>(),
                It.IsAny<TimeSpan?>()))
                .Returns((string key, Func<Task<List<AirportViewModel>>> factory, TimeSpan? expiry) => factory());

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Airport/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Cairo International", content);
            Assert.Contains("Dubai International", content);
        }

        [Fact]
        public async Task Index_WhenNoAirports_ReturnsViewWithInfoMessage()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<List<AirportViewModel>>>>(),
                It.IsAny<TimeSpan?>()))
                .Returns((string key, Func<Task<List<AirportViewModel>>> factory, TimeSpan? expiry) => factory());

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Airport/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("No Airports Found!", content);
        }

        [Fact]
        public async Task Index_WhenExceptionThrown_DisplaysErrorMessage()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<AirportViewModel>>>>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new Exception("Cache error"));

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Airport/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("An error occurred while retrieving airports", content);
        }

        [Fact]
        public async Task Index_UnauthorizedUser_ReturnsRedirectToLogin()
        {
            var client = CreateUnauthenticatedClient();
            var response = await client.GetAsync("/Admin/Airport/Index");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location;
            Assert.NotNull(location);
            Assert.Equal("/Identity/Account/Login", location.LocalPath);
        }

        [Fact]
        public async Task Index_UserWithoutAdminRole_ReturnsForbidden()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/Admin/Airport/Index");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        #endregion

        #region Create (GET)

        [Fact]
        public async Task Create_Get_ReturnsView()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/Airport/Create");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Create Airport", content);
        }

        #endregion

        #region Create (POST)

        [Fact]
        public async Task Create_Post_ValidModel_RedirectsToIndexAndInvalidatesCache()
        {
            var client = CreateAdminClient();

            var getResponse = await client.GetAsync("/Admin/Airport/Create");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Name"] = "Test Airport",
                ["Code"] = "TST",
                ["City"] = "Test City",
                ["Country"] = "Test Country"
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            _cacheServiceMock.Setup(c => c.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            var response = await client.PostAsync("/Admin/Airport/Create", content);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Admin/Airport", response.Headers.Location?.OriginalString);
            _cacheServiceMock.Verify(c => c.RemoveAsync("airports:all"), Times.AtLeastOnce);

            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();
                var airport = await dbContext.Airports.FirstOrDefaultAsync(a => a.Name == "Test Airport");
                Assert.NotNull(airport);
                Assert.Equal("TST", airport.Code);
                Assert.Equal("Test City", airport.City);
                Assert.Equal("Test Country", airport.Country);
            }
        }

        [Fact]
        public async Task Create_Post_InvalidModel_ReturnsViewWithErrors()
        {
            var client = CreateAdminClient();

            var getResponse = await client.GetAsync("/Admin/Airport/Create");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Name"] = "", // invalid
                ["Code"] = "TST",
                ["City"] = "Test City",
                ["Country"] = "Test Country"
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            // Act
            var response = await client.PostAsync("/Admin/Airport/Create", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Create Airport", responseContent);
        }

        #endregion

        #region Edit (GET)

        [Fact]
        public async Task Edit_Get_ValidId_ReturnsViewWithAirport()
        {
            int airportId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airport = new Airport { Name = "Test Airport", Code = "TST", City = "Test City", Country = "Test Country" };
                dbContext.Airports.Add(airport);
                await dbContext.SaveChangesAsync();
                airportId = airport.Id;
            });

            _cacheServiceMock.Setup(c => c.GetAsync<AirportViewModel>(It.IsAny<string>()))
                .ReturnsAsync((AirportViewModel)null);

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync($"/Admin/Airport/Edit/{airportId}");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Test Airport", content);
        }

        [Fact]
        public async Task Edit_Get_InvalidId_ReturnsNotFound()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/Airport/Edit/999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Edit_Get_WithoutId_ReturnsBadRequest()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/Airport/Edit/");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        #endregion

        #region Edit (POST)

        [Fact]
        public async Task Edit_Post_ValidModel_RedirectsToIndexAndInvalidatesCache()
        {
            int airportId = 0;
            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();
                await dbContext.Database.EnsureCreatedAsync();

                var airport = new Airport { Name = "Old Name", Code = "OLD", City = "Old City", Country = "Old Country" };
                dbContext.Airports.Add(airport);
                await dbContext.SaveChangesAsync();
                airportId = airport.Id;
            }

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/Airport/Edit/{airportId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Id"] = airportId.ToString(),
                ["Name"] = "New Name",
                ["Code"] = "NEW",
                ["City"] = "New City",
                ["Country"] = "New Country"
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            _cacheServiceMock.Setup(c => c.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            var response = await client.PostAsync($"/Admin/Airport/Edit/{airportId}", content);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Admin/Airport", response.Headers.Location?.OriginalString);
            _cacheServiceMock.Verify(c => c.RemoveAsync("airports:all"), Times.AtLeastOnce);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"airport:id:{airportId}"), Times.AtLeastOnce);

            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();
                var airport = await dbContext.Airports.FindAsync(airportId);
                Assert.NotNull(airport);
                Assert.Equal("New Name", airport.Name);
                Assert.Equal("NEW", airport.Code);
                Assert.Equal("New City", airport.City);
                Assert.Equal("New Country", airport.Country);
            }
        }

        [Fact]
        public async Task Edit_Post_IdMismatch_ReturnsNotFound()
        {
            int airportId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airport = new Airport { Name = "Test", Code = "TER", City = "City", Country = "Country" };
                dbContext.Airports.Add(airport);
                await dbContext.SaveChangesAsync();
                airportId = airport.Id;
            });

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/Airport/Edit/{airportId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            int mismatchedId = 999;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var exists = await dbContext.Airports.AnyAsync(a => a.Id == mismatchedId);
                Assert.False(exists);
            });

            var formData = new Dictionary<string, string>
            {
                ["Id"] = mismatchedId.ToString(), 
                ["Name"] = "Valid Name",
                ["Code"] = "VCW",
                ["City"] = "Valid City",
                ["Country"] = "Valid Country"
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            var response = await client.PostAsync($"/Admin/Airport/Edit/{mismatchedId}", content);

            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                var body = await response.Content.ReadAsStringAsync();
                Assert.Fail($"Expected NotFound but got {response.StatusCode}. Body: {body}");
            }

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        [Fact]
        public async Task Edit_Post_InvalidModel_ReturnsViewWithErrors()
        {
            int airportId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airport = new Airport { Name = "Test", Code = "T1", City = "City", Country = "Country" };
                dbContext.Airports.Add(airport);
                await dbContext.SaveChangesAsync();
                airportId = airport.Id;
            });

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/Airport/Edit/{airportId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Id"] = airportId.ToString(),
                ["Name"] = "",
                ["Code"] = "VC",
                ["City"] = "Valid City",
                ["Country"] = "Valid Country"
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            // Act
            var response = await client.PostAsync($"/Admin/Airport/Edit/{airportId}", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Edit Airport", responseContent);
        }

        #endregion

        #region Delete (DELETE)

        [Fact]
        public async Task Delete_ValidId_ReturnsJsonSuccessAndInvalidatesCache()
        {
            int airportId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airport = new Airport { Name = "ToDelete", Code = "TD", City = "City", Country = "Country" };
                dbContext.Airports.Add(airport);
                await dbContext.SaveChangesAsync();
                airportId = airport.Id;
            });

            _cacheServiceMock.Setup(c => c.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var client = CreateAdminClient();

            // Act
            var response = await client.DeleteAsync($"/Admin/Airport/Delete/{airportId}");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(bool.Parse(json["success"].ToString()));
            Assert.Equal("Delete Successful", json["message"]?.ToString());
            _cacheServiceMock.Verify(c => c.RemoveAsync("airports:all"), Times.AtLeastOnce);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"airport:id:{airportId}"), Times.AtLeastOnce);

            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airport = await dbContext.Airports.FindAsync(airportId);
                Assert.Null(airport);
            });
        }

        [Fact]
        public async Task Delete_InvalidId_ReturnsJsonNotFound()
        {
            var client = CreateAdminClient();
            var response = await client.DeleteAsync("/Admin/Airport/Delete/999");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(bool.Parse(json["success"].ToString()));
            Assert.Equal("Airport not found", json["message"]?.ToString());
        }

        [Fact]
        public async Task Delete_WithoutId_ReturnsJsonError()
        {
            var client = CreateAdminClient();
            var response = await client.DeleteAsync("/Admin/Airport/Delete/");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(bool.Parse(json["success"].ToString()));
            Assert.Equal("Invalid ID", json["message"]?.ToString());
        }

        #endregion

        #region Helper method

        private string ExtractAntiForgeryToken(string htmlContent)
        {
            var regex = new Regex(@"<input[^>]*name=""__RequestVerificationToken""[^>]*value=""([^""]*)""");
            var match = regex.Match(htmlContent);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            throw new Exception("Anti-forgery token not found in the HTML.");
        }

        #endregion
    }
}