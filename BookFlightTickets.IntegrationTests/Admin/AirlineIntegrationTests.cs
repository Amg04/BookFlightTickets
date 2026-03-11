using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.Hubs;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.ViewModels;
using BookFlightTickets.Infrastructure.Data.DbContext;
using BookFlightTickets.UI;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BookFlightTickets.IntegrationTests.Admin
{
    public class AirlineIntegrationTests : BaseIntegrationTest
    {
        private readonly Mock<IRedisCacheService> _cacheServiceMock;
        private readonly Mock<IHubContext<DashboardHub>> _hubContextMock;

        public AirlineIntegrationTests(WebApplicationFactory<Program> factory) : base(factory)
        {
            _cacheServiceMock = new Mock<IRedisCacheService>();
            _hubContextMock = new Mock<IHubContext<DashboardHub>>();

            InitializeFactory(services =>
            {
                services.AddScoped(_ => _cacheServiceMock.Object);
                services.AddScoped(_ => _hubContextMock.Object);
            });
        }

        #region Index (GET)

        [Fact]
        public async Task Index_WhenAirlinesExist_ReturnsViewWithAirlines()
        {
            // Arrange
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                dbContext.Airlines.Add(new Airline { Name = "EgyptAir", Code = "MS" });
                dbContext.Airlines.Add(new Airline { Name = "Emirates", Code = "EK" });
                await dbContext.SaveChangesAsync();
            });

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<List<AirlineViewModel>>>>(),
                It.IsAny<TimeSpan?>()))
                .Returns((string key, Func<Task<List<AirlineViewModel>>> factory, TimeSpan? expiry) => factory());

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Airline/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("EgyptAir", content);
            Assert.Contains("Emirates", content);
        }

        [Fact]
        public async Task Index_WhenNoAirlines_ReturnsViewWithInfoMessage()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<List<AirlineViewModel>>>>(),
                It.IsAny<TimeSpan?>()))
                .Returns((string key, Func<Task<List<AirlineViewModel>>> factory, TimeSpan? expiry) => factory());

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Airline/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("No Airlines Found!", content);
        }

        [Fact]
        public async Task Index_WhenExceptionThrown_DisplaysErrorMessage()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<AirlineViewModel>>>>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new Exception("Cache error"));

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Airline/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("An error occurred while retrieving airlines", content);
        }

        [Fact]
        public async Task Index_UnauthorizedUser_ReturnsRedirectToLogin()
        {
            var client = CreateUnauthenticatedClient();
            var response = await client.GetAsync("/Admin/Airline/Index");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location;
            Assert.NotNull(location);
            Assert.Equal("/Identity/Account/Login", location.LocalPath);
        }

        [Fact]
        public async Task Index_UserWithoutAdminRole_ReturnsForbidden()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/Admin/Airline/Index");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        #endregion

        #region Create (GET)

        [Fact]
        public async Task Create_Get_ReturnsView()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/Airline/Create");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Create Airline", content);
        }

        #endregion

        #region Create (POST)

        [Fact]
        public async Task Create_Post_ValidModel_RedirectsToIndexAndInvalidatesCache()
        {
            var client = CreateAdminClient();

            var getResponse = await client.GetAsync("/Admin/Airline/Create");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Name"] = "Test Airline",
                ["Code"] = "TA",
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            _cacheServiceMock.Setup(c => c.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            var response = await client.PostAsync("/Admin/Airline/Create", content);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Admin/Airline", response.Headers.Location?.OriginalString);
            _cacheServiceMock.Verify(c => c.RemoveAsync("airlines:all"), Times.AtLeastOnce);

            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();
                var airline = await dbContext.Airlines.FirstOrDefaultAsync(a => a.Name == "Test Airline");
                Assert.NotNull(airline);
                Assert.Equal("TA", airline.Code);
            }
        }

        [Fact]
        public async Task Create_Post_InvalidModel_ReturnsViewWithErrors()
        {
            var client = CreateAdminClient();

            var getResponse = await client.GetAsync("/Admin/Airline/Create");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Name"] = "", 
                ["Code"] = "TA"
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            // Act
            var response = await client.PostAsync("/Admin/Airline/Create", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Create Airline", responseContent);
        }

        #endregion

        #region Edit (GET)

        [Fact]
        public async Task Edit_Get_ValidId_ReturnsViewWithAirline()
        {
            int airlineId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test Airline", Code = "TA" };
                dbContext.Airlines.Add(airline);
                await dbContext.SaveChangesAsync();
                airlineId = airline.Id;
            });

            _cacheServiceMock.Setup(c => c.GetAsync<AirlineViewModel>(It.IsAny<string>()))
                .ReturnsAsync((AirlineViewModel)null);

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync($"/Admin/Airline/Edit/{airlineId}");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Test Airline", content);
        }

        [Fact]
        public async Task Edit_Get_InvalidId_ReturnsNotFound()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/Airline/Edit/999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Edit_Get_WithoutId_ReturnsBadRequest()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/Airline/Edit/");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        #endregion

        #region Edit (POST)

        [Fact]
        public async Task Edit_Post_ValidModel_RedirectsToIndexAndInvalidatesCache()
        {
            int airlineId = 0;
            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();
                await dbContext.Database.EnsureCreatedAsync();

                var airline = new Airline { Name = "Old Name", Code = "ON" };
                dbContext.Airlines.Add(airline);
                await dbContext.SaveChangesAsync();
                airlineId = airline.Id;
            }

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/Airline/Edit/{airlineId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Id"] = airlineId.ToString(),
                ["Name"] = "New Name",
                ["Code"] = "NN",
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            _cacheServiceMock.Setup(c => c.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            var response = await client.PostAsync($"/Admin/Airline/Edit/{airlineId}", content);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Admin/Airline", response.Headers.Location?.OriginalString);
            _cacheServiceMock.Verify(c => c.RemoveAsync("airlines:all"), Times.AtLeastOnce);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"airline:id:{airlineId}"), Times.AtLeastOnce);

            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();
                var airline = await dbContext.Airlines.FindAsync(airlineId);
                Assert.NotNull(airline);
                Assert.Equal("New Name", airline.Name);
                Assert.Equal("NN", airline.Code);
            }
        }

        [Fact]
        public async Task Edit_Post_IdMismatch_ReturnsNotFound()
        {
            int airlineId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test", Code = "T1"};
                dbContext.Airlines.Add(airline);
                await dbContext.SaveChangesAsync();
                airlineId = airline.Id;
            });

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/Airline/Edit/{airlineId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Name"] = "Valid Name",
                ["Code"] = "VC",
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            int mismatchedId = 999;
            var response = await client.PostAsync($"/Admin/Airline/Edit/{mismatchedId}", content);

            if (response.StatusCode == HttpStatusCode.Found)
            {
                Assert.Fail($"Expected NotFound but got redirect to {response.Headers.Location}");
            }
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Edit_Post_InvalidModel_ReturnsViewWithErrors()
        {
            int airlineId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test", Code = "T1"};
                dbContext.Airlines.Add(airline);
                await dbContext.SaveChangesAsync();
                airlineId = airline.Id;
            });

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/Airline/Edit/{airlineId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Id"] = airlineId.ToString(),
                ["Name"] = "",
                ["Code"] = "VC"
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            // Act
            var response = await client.PostAsync($"/Admin/Airline/Edit/{airlineId}", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Edit Airline", responseContent);
        }

        #endregion

        #region Details (GET)

        [Fact]
        public async Task Details_ValidId_ReturnsViewWithAirline()
        {
            int airlineId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test Airline", Code = "TA" };
                dbContext.Airlines.Add(airline);
                await dbContext.SaveChangesAsync();
                airlineId = airline.Id;
            });

            _cacheServiceMock.Setup(c => c.GetAsync<AirlineViewModel>(It.IsAny<string>()))
                .ReturnsAsync((AirlineViewModel)null);

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync($"/Admin/Airline/Details/{airlineId}");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Test Airline", content);
        }

        [Fact]
        public async Task Details_InvalidId_ReturnsNotFound()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/Airline/Details/999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Details_WithoutId_ReturnsBadRequest()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/Airline/Details/");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        #endregion

        #region Delete (DELETE)

        [Fact]
        public async Task Delete_ValidId_ReturnsJsonSuccessAndInvalidatesCache()
        {
            int airlineId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "ToDelete", Code = "TD" };
                dbContext.Airlines.Add(airline);
                await dbContext.SaveChangesAsync();
                airlineId = airline.Id;
            });

            _cacheServiceMock.Setup(c => c.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var client = CreateAdminClient();

            // Act
            var response = await client.DeleteAsync($"/Admin/Airline/Delete/{airlineId}");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(bool.Parse(json["success"].ToString()));
            Assert.Equal("Delete Successful", json["message"]?.ToString());
            _cacheServiceMock.Verify(c => c.RemoveAsync("airlines:all"), Times.AtLeastOnce);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"airline:id:{airlineId}"), Times.AtLeastOnce);

            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = await dbContext.Airlines.FindAsync(airlineId);
                Assert.Null(airline);
            });
        }

        [Fact]
        public async Task Delete_InvalidId_ReturnsJsonNotFound()
        {
            var client = CreateAdminClient();
            var response = await client.DeleteAsync("/Admin/Airline/Delete/999");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(bool.Parse(json["success"].ToString()));
            Assert.Equal("Airline not found", json["message"]?.ToString());
        }

        [Fact]
        public async Task Delete_WithoutId_ReturnsJsonError()
        {
            var client = CreateAdminClient();
            var response = await client.DeleteAsync("/Admin/Airline/Delete/");
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