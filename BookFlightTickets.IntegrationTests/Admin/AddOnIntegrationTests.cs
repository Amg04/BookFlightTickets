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
    public class AddOnIntegrationTests : BaseIntegrationTest
    {
        private readonly Mock<IRedisCacheService> _cacheServiceMock;

        public AddOnIntegrationTests(WebApplicationFactory<Program> factory) : base(factory)
        {
            _cacheServiceMock = new Mock<IRedisCacheService>();

            InitializeFactory(services =>
            {
                services.AddScoped(_ => _cacheServiceMock.Object);
            });
        }

        #region Index (GET)

        [Fact]
        public async Task Index_WhenAddOnsExist_ReturnsViewWithAddOns()
        {
            // Arrange
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                dbContext.AddOns.Add(new AddOn { Name = "Extra Baggage", Price = 50 });
                dbContext.AddOns.Add(new AddOn { Name = "Meal", Price = 20 });
                await dbContext.SaveChangesAsync();
            });

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<List<AddOnViewModel>>>>(),
                It.IsAny<TimeSpan?>()))
                .Returns((string key, Func<Task<List<AddOnViewModel>>> factory, TimeSpan? expiry) => factory());


            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/AddOn/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Extra Baggage", content);
            Assert.Contains("Meal", content);
        }

        [Fact]
        public async Task Index_WhenNoAddOns_ReturnsViewWithInfoMessage()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
               It.IsAny<string>(),
               It.IsAny<Func<Task<List<AddOnViewModel>>>>(),
               It.IsAny<TimeSpan?>()))
               .Returns((string key, Func<Task<List<AddOnViewModel>>> factory, TimeSpan? expiry) => factory());

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/AddOn/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("No Add-Ons Found!", content);
        }

        [Fact]
        public async Task Index_WhenExceptionThrown_DisplaysErrorMessage()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<AddOnViewModel>>>>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new Exception("Cache error"));

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/AddOn/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("An error occurred while retrieving addons", content);
        }

        [Fact]
        public async Task Index_UnauthorizedUser_ReturnsRedirectToLogin()
        {
            var client = CreateUnauthenticatedClient();
            var response = await client.GetAsync("/Admin/AddOn/Index");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location;
            Assert.NotNull(location);
            Assert.Equal("/Identity/Account/Login", location.LocalPath);
        }

        [Fact]
        public async Task Index_UserWithoutAdminRole_ReturnsForbidden()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/Admin/AddOn/Index");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        #endregion

        #region Create (GET)

        [Fact]
        public async Task Create_Get_ReturnsView()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/AddOn/Create");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Create Add-On", content);
        }

        #endregion

        #region Create (POST)

        [Fact]
        public async Task Create_Post_ValidModel_RedirectsToIndexAndInvalidatesCache()
        {
            var client = CreateAdminClient();

            var getResponse = await client.GetAsync("/Admin/AddOn/Create");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            // Arrange
            var formData = new MultipartFormDataContent
            {
                { new StringContent("Extra Baggage"), "Name" },
                { new StringContent("150"), "Price" },
                { new StringContent("Extra baggage description"), "Description" }
            };
            formData.Headers.Add("RequestVerificationToken", token);

            _cacheServiceMock.Setup(c => c.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            var response = await client.PostAsync("/Admin/AddOn/Create", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Admin/AddOn", response.Headers.Location?.OriginalString);
            _cacheServiceMock.Verify(c => c.RemoveAsync("addons:all"), Times.AtLeastOnce);

            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var addon = await dbContext.AddOns.FirstOrDefaultAsync(a => a.Name == "Extra Baggage");
                Assert.NotNull(addon);
                Assert.Equal(150, addon.Price);
            });
        }

        [Fact]
        public async Task Create_Post_InvalidModel_ReturnsViewWithErrors()
        {
            var client = CreateAdminClient();

            var getResponse = await client.GetAsync("/Admin/AddOn/Create");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            var token = ExtractAntiForgeryToken(getContent);

            // Arrange
            var formData = new MultipartFormDataContent
            {
                { new StringContent(""), "Name" }, 
                { new StringContent("50"), "Price" }
            };

            formData.Headers.Add("RequestVerificationToken", token);

            // Act
            var response = await client.PostAsync("/Admin/AddOn/Create", formData);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Create Add-On", content);
        }

        #endregion

        #region Edit (GET)

        [Fact]
        public async Task Edit_Get_ValidId_ReturnsViewWithAddOn()
        {
            // Arrange
            int addonId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var addon = new AddOn { Name = "Test AddOn", Price = 100 };
                dbContext.AddOns.Add(addon);
                await dbContext.SaveChangesAsync();
                addonId = addon.Id;
            });

            _cacheServiceMock.Setup(c => c.GetAsync<AddOnViewModel>(It.IsAny<string>()))
                .ReturnsAsync((AddOnViewModel)null);

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync($"/Admin/AddOn/Edit/{addonId}");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Test AddOn", content);
        }

        [Fact]
        public async Task Edit_Get_InvalidId_ReturnsNotFound()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/AddOn/Edit/999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Edit_Get_WithoutId_ReturnsBadRequest()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/AddOn/Edit/");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        #endregion

        #region Edit (POST)

        [Fact]
        public async Task Edit_Post_ValidModel_RedirectsToIndexAndInvalidatesCache()
        {
            // Arrange
            int addonId = 0;
            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();
                await dbContext.Database.EnsureCreatedAsync();

                var addon = new AddOn { Name = "Old Name", Price = 50 };
                dbContext.AddOns.Add(addon);
                await dbContext.SaveChangesAsync();
                addonId = addon.Id;
            }

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/AddOn/Edit/{addonId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Id"] = addonId.ToString(),
                ["Name"] = "New Name",
                ["Price"] = "75",
                ["Description"] = "New description"
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            _cacheServiceMock.Setup(c => c.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();


            // Act
            var response = await client.PostAsync($"/Admin/AddOn/Edit/{addonId}", content);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Admin/AddOn", response.Headers.Location?.OriginalString);
            _cacheServiceMock.Verify(c => c.RemoveAsync("addons:all"), Times.AtLeastOnce);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"addon:id:{addonId}"), Times.AtLeastOnce);

            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();
                var addon = await dbContext.AddOns.FindAsync(addonId);
                Assert.NotNull(addon);
                Assert.Equal("New Name", addon.Name);
                Assert.Equal(75, addon.Price);
            }
        }

        [Fact]
        public async Task Edit_Post_IdMismatch_ReturnsNotFound()
        {
            int addonId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var addon = new AddOn { Name = "Test", Price = 50 };
                dbContext.AddOns.Add(addon);
                await dbContext.SaveChangesAsync();
                addonId = addon.Id;
            });

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/AddOn/Edit/{addonId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Name"] = "Valid Name",
                ["Price"] = "50",
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            int mismatchedId = 999;
            var response = await client.PostAsync($"/Admin/AddOn/Edit/{mismatchedId}", content);

            if (response.StatusCode == HttpStatusCode.Found)
            {
                Assert.Fail($"Expected NotFound but got redirect to {response.Headers.Location}");
            }
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Edit_Post_InvalidModel_ReturnsViewWithErrors()
        {
            // Arrange
            int addonId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var addon = new AddOn { Name = "Test", Price = 50 };
                dbContext.AddOns.Add(addon);
                await dbContext.SaveChangesAsync();
                addonId = addon.Id;
            });

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/AddOn/Edit/{addonId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Id"] = addonId.ToString(),
                ["Name"] = "",
                ["Price"] = "50"
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            // Act
            var response = await client.PostAsync($"/Admin/AddOn/Edit/{addonId}", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Edit Add-On", responseContent);
        }

        #endregion

        #region Delete

        [Fact]
        public async Task Delete_ValidId_ReturnsJsonSuccessAndInvalidatesCache()
        {
            // Arrange
            int addonId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var addon = new AddOn { Name = "ToDelete", Price = 30 };
                dbContext.AddOns.Add(addon);
                await dbContext.SaveChangesAsync();
                addonId = addon.Id;
            });

            _cacheServiceMock.Setup(c => c.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var client = CreateAdminClient();

            // Act
            var response = await client.DeleteAsync($"/Admin/AddOn/Delete/{addonId}");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(bool.Parse(json["success"].ToString()));
            Assert.Equal("Delete Successful", json["message"]?.ToString());
            _cacheServiceMock.Verify(c => c.RemoveAsync("addons:all"), Times.AtLeastOnce);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"addon:id:{addonId}"), Times.AtLeastOnce);

            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var addon = await dbContext.AddOns.FindAsync(addonId);
                Assert.Null(addon);
            });
        }

        [Fact]
        public async Task Delete_InvalidId_ReturnsJsonNotFound()
        {
            var client = CreateAdminClient();
            var response = await client.DeleteAsync("/Admin/AddOn/Delete/999");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(bool.Parse(json["success"].ToString()));
            Assert.Equal("Add-On not found", json["message"]?.ToString());
        }

        [Fact]
        public async Task Delete_WithoutId_ReturnsJsonError()
        {
            var client = CreateAdminClient();
            var response = await client.DeleteAsync("/Admin/AddOn/Delete/");
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