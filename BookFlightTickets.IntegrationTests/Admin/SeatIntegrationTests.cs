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
    public class SeatIntegrationTests : BaseIntegrationTest
    {
        private readonly Mock<IRedisCacheService> _cacheServiceMock;
        private readonly Mock<IHubContext<DashboardHub>> _hubContextMock;

        public SeatIntegrationTests(WebApplicationFactory<Program> factory) : base(factory)
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
        public async Task Index_WhenSeatsExist_ReturnsViewWithSeats()
        {
            // Arrange
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airplane = new Airplane { Model = "Boeing 737", SeatCapacity = 180 };
                dbContext.Airplanes.Add(airplane);
                await dbContext.SaveChangesAsync();

                dbContext.Seats.Add(new Seat { AirplaneId = airplane.Id, Row = "A", Number = 1, Class = SeatClass.Economy });
                dbContext.Seats.Add(new Seat { AirplaneId = airplane.Id, Row = "A", Number = 2, Class = SeatClass.Economy });
                await dbContext.SaveChangesAsync();
            });

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<List<SeatViewModel>>>>(),
                It.IsAny<TimeSpan?>()))
                .Returns((string key, Func<Task<List<SeatViewModel>>> factory, TimeSpan? expiry) => factory());

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Seat/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("A", content);
            Assert.Contains("1", content);
            Assert.Contains("2", content);
        }

        [Fact]
        public async Task Index_WhenNoSeats_ReturnsViewWithInfoMessage()
        {
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<List<SeatViewModel>>>>(),
                It.IsAny<TimeSpan?>()))
                .Returns((string key, Func<Task<List<SeatViewModel>>> factory, TimeSpan? expiry) => factory());

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Seat/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("toastr.info('No seats available.');", content);
        }

        [Fact]
        public async Task Index_WhenExceptionThrown_DisplaysErrorMessage()
        {
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<SeatViewModel>>>>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new Exception("Cache error"));

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Seat/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("An error occurred while retrieving seats", content);
        }

        [Fact]
        public async Task Index_UnauthorizedUser_ReturnsRedirectToLogin()
        {
            var client = CreateUnauthenticatedClient();
            var response = await client.GetAsync("/Admin/Seat/Index");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location;
            Assert.NotNull(location);
            Assert.Equal("/Identity/Account/Login", location.LocalPath);
        }

        [Fact]
        public async Task Index_UserWithoutAdminRole_ReturnsForbidden()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/Admin/Seat/Index");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        #endregion

        #region Create (GET)

        [Fact]
        public async Task Create_Get_ReturnsViewWithDropdowns()
        {
            // Arrange
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                dbContext.Airplanes.Add(new Airplane { Model = "Boeing 737", SeatCapacity = 180 });
                dbContext.Airplanes.Add(new Airplane { Model = "Airbus A320", SeatCapacity = 200 });
                await dbContext.SaveChangesAsync();
            });

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Seat/Create");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Create Seat", content);
            Assert.Contains("Boeing 737", content);
            Assert.Contains("Economy", content);
        }

        #endregion

        #region Create (POST)

        [Fact]
        public async Task Create_Post_ValidModel_RedirectsToIndexAndInvalidatesCache()
        {
            int airplaneId = 0;
            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();
                await dbContext.Database.EnsureCreatedAsync();

                var airplane = new Airplane { Model = "Boeing 737", SeatCapacity = 180 };
                dbContext.Airplanes.Add(airplane);
                await dbContext.SaveChangesAsync();
                airplaneId = airplane.Id;
            }

            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();
                var airplane = await dbContext.Airplanes.FindAsync(airplaneId);
                Assert.NotNull(airplane);
            }

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync("/Admin/Seat/Create");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["AirplaneId"] = airplaneId.ToString(),
                ["Row"] = "A",
                ["Number"] = "10",
                ["Class"] = "1" 
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            _cacheServiceMock.Setup(c => c.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            var response = await client.PostAsync("/Admin/Seat/Create", content);

            if (response.StatusCode != HttpStatusCode.Redirect)
            {
                var body = await response.Content.ReadAsStringAsync();
                Assert.Fail($"Expected redirect but got {response.StatusCode}. Body: {body}");
            }
            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Admin/Seat", response.Headers.Location?.OriginalString);
            _cacheServiceMock.Verify(c => c.RemoveAsync("seats:all"), Times.AtLeastOnce);

            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();
                var seat = await dbContext.Seats.FirstOrDefaultAsync(s => s.AirplaneId == airplaneId && s.Row == "A" && s.Number == 10);
                Assert.NotNull(seat);
                Assert.Equal(SeatClass.Economy, seat.Class);
            }
        }

        [Fact]
        public async Task Create_Post_InvalidModel_ReturnsViewWithErrors()
        {
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                dbContext.Airplanes.Add(new Airplane { Model = "Boeing 737", SeatCapacity = 180 });
                await dbContext.SaveChangesAsync();
            });

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync("/Admin/Seat/Create");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["AirplaneId"] = "",
                ["Row"] = "",
                ["Number"] = "abc",
                ["Class"] = ""
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            // Act
            var response = await client.PostAsync("/Admin/Seat/Create", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Create Seat", responseContent);
        }

        #endregion

        #region Edit (GET)

        [Fact]
        public async Task Edit_Get_ValidId_ReturnsViewWithSeat()
        {
            int seatId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airplane = new Airplane { Model = "Boeing 737", SeatCapacity = 180 };
                dbContext.Airplanes.Add(airplane);
                await dbContext.SaveChangesAsync();

                var seat = new Seat { AirplaneId = airplane.Id, Row = "A", Number = 1, Class = SeatClass.Economy };
                dbContext.Seats.Add(seat);
                await dbContext.SaveChangesAsync();
                seatId = seat.Id;
            });

            _cacheServiceMock.Setup(c => c.GetAsync<SeatViewModel>(It.IsAny<string>()))
                .ReturnsAsync((SeatViewModel)null);

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync($"/Admin/Seat/Edit/{seatId}");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Edit Seat", content);
        }

        [Fact]
        public async Task Edit_Get_InvalidId_ReturnsNotFound()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/Seat/Edit/999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Edit_Get_WithoutId_ReturnsBadRequest()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/Seat/Edit/");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        #endregion

        #region Edit (POST)

        [Fact]
        public async Task Edit_Post_ValidModel_RedirectsToIndexAndInvalidatesCache()
        {
            int seatId = 0;
            int airplaneId = 0;
            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();
                await dbContext.Database.EnsureCreatedAsync();

                var airplane = new Airplane { Model = "Boeing 737", SeatCapacity = 180 };
                dbContext.Airplanes.Add(airplane);
                await dbContext.SaveChangesAsync();

                var seat = new Seat { AirplaneId = airplane.Id, Row = "A", Number = 1, Class = SeatClass.Economy };
                dbContext.Seats.Add(seat);
                await dbContext.SaveChangesAsync();
                seatId = seat.Id;
                airplaneId = airplane.Id;
            }

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/Seat/Edit/{seatId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Id"] = seatId.ToString(),
                ["AirplaneId"] = airplaneId.ToString(),
                ["Row"] = "B",
                ["Number"] = "2",
                ["Class"] = "2" 
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            _cacheServiceMock.Setup(c => c.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            var response = await client.PostAsync($"/Admin/Seat/Edit/{seatId}", content);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Admin/Seat", response.Headers.Location?.OriginalString);
            _cacheServiceMock.Verify(c => c.RemoveAsync("seats:all"), Times.AtLeastOnce);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"seat:id:{seatId}"), Times.AtLeastOnce);

            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();
                var seat = await dbContext.Seats.FindAsync(seatId);
                Assert.NotNull(seat);
                Assert.Equal("B", seat.Row);
                Assert.Equal(2, seat.Number);
                Assert.Equal(SeatClass.Business, seat.Class);
            }
        }

        [Fact]
        public async Task Edit_Post_IdMismatch_ReturnsNotFound()
        {
            int seatId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airplane = new Airplane { Model = "Boeing 737", SeatCapacity = 180 };
                dbContext.Airplanes.Add(airplane);
                await dbContext.SaveChangesAsync();

                var seat = new Seat { AirplaneId = airplane.Id, Row = "A", Number = 1, Class = SeatClass.Economy };
                dbContext.Seats.Add(seat);
                await dbContext.SaveChangesAsync();
                seatId = seat.Id;
            });

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/Seat/Edit/{seatId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Id"] = (seatId + 1).ToString(),
                ["AirplaneId"] = "1",
                ["Row"] = "B",
                ["Number"] = "2",
                ["Class"] = "1"
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            var response = await client.PostAsync($"/Admin/Seat/Edit/{seatId}", content);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Edit_Post_InvalidModel_ReturnsViewWithErrors()
        {
            int seatId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airplane = new Airplane { Model = "Boeing 737", SeatCapacity = 180 };
                dbContext.Airplanes.Add(airplane);
                await dbContext.SaveChangesAsync();

                var seat = new Seat { AirplaneId = airplane.Id, Row = "A", Number = 1, Class = SeatClass.Economy };
                dbContext.Seats.Add(seat);
                await dbContext.SaveChangesAsync();
                seatId = seat.Id;
            });

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/Seat/Edit/{seatId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Id"] = seatId.ToString(),
                ["AirplaneId"] = "",
                ["Row"] = "",
                ["Number"] = "abc",
                ["Class"] = ""
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            // Act
            var response = await client.PostAsync($"/Admin/Seat/Edit/{seatId}", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Edit Seat", responseContent);
        }

        #endregion

        #region Details (GET)

        [Fact]
        public async Task Details_ValidId_ReturnsViewWithSeat()
        {
            int seatId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airplane = new Airplane { Model = "Boeing 737", SeatCapacity = 180 };
                dbContext.Airplanes.Add(airplane);
                await dbContext.SaveChangesAsync();

                var seat = new Seat { AirplaneId = airplane.Id, Row = "A", Number = 1, Class = SeatClass.Economy };
                dbContext.Seats.Add(seat);
                await dbContext.SaveChangesAsync();
                seatId = seat.Id;
            });

            _cacheServiceMock.Setup(c => c.GetAsync<SeatViewModel>(It.IsAny<string>()))
                .ReturnsAsync((SeatViewModel)null);

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync($"/Admin/Seat/Details/{seatId}");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Seats Details", content);
            Assert.Contains("Economy", content);
        }

        [Fact]
        public async Task Details_InvalidId_ReturnsNotFound()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/Seat/Details/999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Details_WithoutId_ReturnsBadRequest()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/Seat/Details/");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        #endregion

        #region Delete (DELETE)

        [Fact]
        public async Task Delete_ValidId_ReturnsJsonSuccessAndInvalidatesCache()
        {
            int seatId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airplane = new Airplane { Model = "Boeing 737", SeatCapacity = 180 };
                dbContext.Airplanes.Add(airplane);
                await dbContext.SaveChangesAsync();

                var seat = new Seat { AirplaneId = airplane.Id, Row = "A", Number = 1, Class = SeatClass.Economy };
                dbContext.Seats.Add(seat);
                await dbContext.SaveChangesAsync();
                seatId = seat.Id;
            });

            _cacheServiceMock.Setup(c => c.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var client = CreateAdminClient();

            // Act
            var response = await client.DeleteAsync($"/Admin/Seat/Delete/{seatId}");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(bool.Parse(json["success"].ToString()));
            Assert.Equal("Delete Successful", json["message"]?.ToString());
            _cacheServiceMock.Verify(c => c.RemoveAsync("seats:all"), Times.AtLeastOnce);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"seat:id:{seatId}"), Times.AtLeastOnce);

            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var seat = await dbContext.Seats.FindAsync(seatId);
                Assert.Null(seat);
            });
        }

        [Fact]
        public async Task Delete_InvalidId_ReturnsJsonNotFound()
        {
            var client = CreateAdminClient();
            var response = await client.DeleteAsync("/Admin/Seat/Delete/999");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(bool.Parse(json["success"].ToString()));
            Assert.Equal("Seat not found", json["message"]?.ToString());
        }

        [Fact]
        public async Task Delete_WithoutId_ReturnsJsonError()
        {
            var client = CreateAdminClient();
            var response = await client.DeleteAsync("/Admin/Seat/Delete/");
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