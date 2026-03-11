using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.Hubs;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.ViewModels;
using BookFlightTickets.Infrastructure.Data.DbContext;
using BookFlightTickets.UI;
using Microsoft.AspNetCore.Mvc.Rendering;
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
    public class AirplaneIntegrationTests : BaseIntegrationTest
    {
        private readonly Mock<IRedisCacheService> _cacheServiceMock;
        private readonly Mock<IHubContext<DashboardHub>> _hubContextMock;

        public AirplaneIntegrationTests(WebApplicationFactory<Program> factory) : base(factory)
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
        public async Task Index_WhenAirplanesExist_ReturnsViewWithAirplanes()
        {
            // Arrange
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test Airline", Code = "TA" };
                dbContext.Airlines.Add(airline);
                await dbContext.SaveChangesAsync();

                dbContext.Airplanes.Add(new Airplane { Model = "Boeing 737", SeatCapacity = 180, AirlineId = airline.Id });
                dbContext.Airplanes.Add(new Airplane { Model = "Airbus A320", SeatCapacity = 200, AirlineId = airline.Id });
                await dbContext.SaveChangesAsync();
            });

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<List<AirplaneViewModel>>>>(),
                It.IsAny<TimeSpan?>()))
                .Returns((string key, Func<Task<List<AirplaneViewModel>>> factory, TimeSpan? expiry) => factory());

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Airplane/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Boeing 737", content);
            Assert.Contains("Airbus A320", content);
        }

        [Fact]
        public async Task Index_WhenNoAirplanes_ReturnsViewWithInfoMessage()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<List<AirplaneViewModel>>>>(),
                It.IsAny<TimeSpan?>()))
                .Returns((string key, Func<Task<List<AirplaneViewModel>>> factory, TimeSpan? expiry) => factory());

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Airplane/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("No Airplanes Found!", content);
        }

        [Fact]
        public async Task Index_WhenExceptionThrown_DisplaysErrorMessage()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<AirplaneViewModel>>>>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new Exception("Cache error"));

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Airplane/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("An error occurred while retrieving airplanes", content);
        }

        [Fact]
        public async Task Index_UnauthorizedUser_ReturnsRedirectToLogin()
        {
            var client = CreateUnauthenticatedClient();
            var response = await client.GetAsync("/Admin/Airplane/Index");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location;
            Assert.NotNull(location);
            Assert.Equal("/Identity/Account/Login", location.LocalPath);
        }

        [Fact]
        public async Task Index_UserWithoutAdminRole_ReturnsForbidden()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/Admin/Airplane/Index");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        #endregion

        #region Create (GET)

        [Fact]
        public async Task Create_Get_ReturnsViewWithAirlinesDropdown()
        {
            // Arrange
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                dbContext.Airlines.Add(new Airline { Name = "Airline1", Code = "A1" });
                dbContext.Airlines.Add(new Airline { Name = "Airline2", Code = "A2" });
                await dbContext.SaveChangesAsync();
            });

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<List<SelectListItem>>>>(),
                It.IsAny<TimeSpan?>()))
                .Returns((string key, Func<Task<List<SelectListItem>>> factory, TimeSpan? expiry) => factory());

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Airplane/Create");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Create Airplane", content);
            Assert.Contains("Airline1", content);
            Assert.Contains("Airline2", content);
        }

        #endregion

        #region Create (POST)

        [Fact]
        public async Task Create_Post_ValidModel_RedirectsToIndexAndInvalidatesCache()
        {
            // Arrange
            int airlineId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test Airline", Code = "TA" };
                dbContext.Airlines.Add(airline);
                await dbContext.SaveChangesAsync();
                airlineId = airline.Id;
            });

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync("/Admin/Airplane/Create");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Model"] = "Boeing 787",
                ["SeatCapacity"] = "250",
                ["AirlineId"] = airlineId.ToString()
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            _cacheServiceMock.Setup(c => c.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            var response = await client.PostAsync("/Admin/Airplane/Create", content);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Admin/Airplane", response.Headers.Location?.OriginalString);
            _cacheServiceMock.Verify(c => c.RemoveAsync("airplanes:all"), Times.AtLeastOnce);

            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();
                var airplane = await dbContext.Airplanes.FirstOrDefaultAsync(a => a.Model == "Boeing 787");
                Assert.NotNull(airplane);
                Assert.Equal(250, airplane.SeatCapacity);
                Assert.Equal(airlineId, airplane.AirlineId);
            }
        }

        [Fact]
        public async Task Create_Post_InvalidModel_ReturnsViewWithErrorsAndDropdown()
        {
            // Arrange
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                dbContext.Airlines.Add(new Airline { Name = "Airline1", Code = "A1" });
                await dbContext.SaveChangesAsync();
            });

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync("/Admin/Airplane/Create");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Model"] = "", 
                ["SeatCapacity"] = "250",
                ["AirlineId"] = "1"
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            // Act
            var response = await client.PostAsync("/Admin/Airplane/Create", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Create Airplane", responseContent);
            Assert.Contains("Select Airline", responseContent); 
        }

        #endregion

        #region Edit (GET)

        [Fact]
        public async Task Edit_Get_ValidId_ReturnsViewWithAirplaneAndDropdown()
        {
            // Arrange
            int airplaneId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test Airline", Code = "TA" };
                dbContext.Airlines.Add(airline);
                await dbContext.SaveChangesAsync();

                var airplane = new Airplane { Model = "Boeing 737", SeatCapacity = 180, AirlineId = airline.Id };
                dbContext.Airplanes.Add(airplane);
                await dbContext.SaveChangesAsync();
                airplaneId = airplane.Id;
            });

            _cacheServiceMock.Setup(c => c.GetAsync<AirplaneViewModel>(It.IsAny<string>()))
                .ReturnsAsync((AirplaneViewModel)null);

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<List<SelectListItem>>>>(),
                It.IsAny<TimeSpan?>()))
                .Returns((string key, Func<Task<List<SelectListItem>>> factory, TimeSpan? expiry) => factory());

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync($"/Admin/Airplane/Edit/{airplaneId}");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Boeing 737", content);
            Assert.Contains("Test Airline", content);
        }

        [Fact]
        public async Task Edit_Get_InvalidId_ReturnsNotFound()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/Airplane/Edit/999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Edit_Get_WithoutId_ReturnsBadRequest()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/Airplane/Edit/");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        #endregion

        #region Edit (POST)

        [Fact]
        public async Task Edit_Post_ValidModel_RedirectsToIndexAndInvalidatesCache()
        {
            int airplaneId = 0;
            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();
                await dbContext.Database.EnsureCreatedAsync();

                var airline = new Airline { Name = "Test Airline", Code = "TA" };
                dbContext.Airlines.Add(airline);
                await dbContext.SaveChangesAsync();

                var airplane = new Airplane { Model = "Old Model", SeatCapacity = 150, AirlineId = airline.Id };
                dbContext.Airplanes.Add(airplane);
                await dbContext.SaveChangesAsync();
                airplaneId = airplane.Id;
            }

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/Airplane/Edit/{airplaneId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Id"] = airplaneId.ToString(),
                ["Model"] = "New Model",
                ["SeatCapacity"] = "200",
                ["AirlineId"] = "1"
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            _cacheServiceMock.Setup(c => c.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            var response = await client.PostAsync($"/Admin/Airplane/Edit/{airplaneId}", content);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Admin/Airplane", response.Headers.Location?.OriginalString);
            _cacheServiceMock.Verify(c => c.RemoveAsync("airplanes:all"), Times.AtLeastOnce);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"airplane:id:{airplaneId}"), Times.AtLeastOnce);

            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();
                var airplane = await dbContext.Airplanes.FindAsync(airplaneId);
                Assert.NotNull(airplane);
                Assert.Equal("New Model", airplane.Model);
                Assert.Equal(200, airplane.SeatCapacity);
            }
        }

        [Fact]
        public async Task Edit_Post_IdMismatch_ReturnsNotFound()
        {
            int airplaneId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test", Code = "T1" };
                dbContext.Airlines.Add(airline);
                await dbContext.SaveChangesAsync();

                var airplane = new Airplane { Model = "Test", SeatCapacity = 150, AirlineId = airline.Id };
                dbContext.Airplanes.Add(airplane);
                await dbContext.SaveChangesAsync();
                airplaneId = airplane.Id;
            });

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/Airplane/Edit/{airplaneId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Model"] = "Valid Model",
                ["SeatCapacity"] = "200",
                ["AirlineId"] = "1"
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            int mismatchedId = 999;
            var response = await client.PostAsync($"/Admin/Airplane/Edit/{mismatchedId}", content);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Edit_Post_InvalidModel_ReturnsViewWithErrors()
        {
            int airplaneId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test", Code = "T1" };
                dbContext.Airlines.Add(airline);
                await dbContext.SaveChangesAsync();

                var airplane = new Airplane { Model = "Test", SeatCapacity = 150, AirlineId = airline.Id };
                dbContext.Airplanes.Add(airplane);
                await dbContext.SaveChangesAsync();
                airplaneId = airplane.Id;
            });

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/Airplane/Edit/{airplaneId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Id"] = airplaneId.ToString(),
                ["Model"] = "",
                ["SeatCapacity"] = "200",
                ["AirlineId"] = "1"
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            // Act
            var response = await client.PostAsync($"/Admin/Airplane/Edit/{airplaneId}", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Edit Airplane", responseContent);
        }

        #endregion

        #region Delete (DELETE)

        [Fact]
        public async Task Delete_ValidId_ReturnsJsonSuccessAndInvalidatesCache()
        {
            int airplaneId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test", Code = "T1" };
                dbContext.Airlines.Add(airline);
                await dbContext.SaveChangesAsync();

                var airplane = new Airplane { Model = "ToDelete", SeatCapacity = 150, AirlineId = airline.Id };
                dbContext.Airplanes.Add(airplane);
                await dbContext.SaveChangesAsync();
                airplaneId = airplane.Id;
            });

            _cacheServiceMock.Setup(c => c.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var client = CreateAdminClient();

            // Act
            var response = await client.DeleteAsync($"/Admin/Airplane/Delete/{airplaneId}");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(bool.Parse(json["success"].ToString()));
            Assert.Equal("Delete Successful", json["message"]?.ToString());
            _cacheServiceMock.Verify(c => c.RemoveAsync("airplanes:all"), Times.AtLeastOnce);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"airplane:id:{airplaneId}"), Times.AtLeastOnce);

            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airplane = await dbContext.Airplanes.FindAsync(airplaneId);
                Assert.Null(airplane);
            });
        }

        [Fact]
        public async Task Delete_InvalidId_ReturnsJsonNotFound()
        {
            var client = CreateAdminClient();
            var response = await client.DeleteAsync("/Admin/Airplane/Delete/999");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(bool.Parse(json["success"].ToString()));
            Assert.Equal("Airplane not found", json["message"]?.ToString());
        }

        [Fact]
        public async Task Delete_WithoutId_ReturnsJsonError()
        {
            var client = CreateAdminClient();
            var response = await client.DeleteAsync("/Admin/Airplane/Delete/");
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