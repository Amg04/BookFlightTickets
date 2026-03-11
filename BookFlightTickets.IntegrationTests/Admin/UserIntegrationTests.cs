using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.UI;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace BookFlightTickets.IntegrationTests.Admin
{
    public class UserIntegrationTests : BaseIntegrationTest
    {
        public UserIntegrationTests(WebApplicationFactory<Program> factory) : base(factory)
        {
            InitializeFactory(services => { });
        }

        #region Index (GET)

        [Fact]
        public async Task Index_WhenUsersExist_ReturnsViewWithUsers()
        {
            // Arrange
            using (var scope = Factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                var users = new[]
                {
                    new AppUser { UserName = "user1@test.com", Email = "user1@test.com", FirstName = "User", LastName = "One" },
                    new AppUser { UserName = "user2@test.com", Email = "user2@test.com", FirstName = "User", LastName = "Two" }
                };
                foreach (var user in users)
                {
                    await userManager.CreateAsync(user, "Password123!");
                }
            }

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/User/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("user1@test.com", content);
            Assert.Contains("user2@test.com", content);
        }

        [Fact]
        public async Task Index_WhenNoUsers_ReturnsViewWithEmptyList()
        {
            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/User/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("User List", content); 
        }

        [Fact]
        public async Task Index_UnauthorizedUser_ReturnsRedirectToLogin()
        {
            var client = CreateUnauthenticatedClient();
            var response = await client.GetAsync("/Admin/User/Index");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location;
            Assert.NotNull(location);
            Assert.Equal("/Identity/Account/Login", location.LocalPath);
        }

        [Fact]
        public async Task Index_UserWithoutAdminRole_ReturnsForbidden()
        {
            var client = CreateAuthenticatedClient(); 
            var response = await client.GetAsync("/Admin/User/Index");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        #endregion

        #region LockUnlockAsync (POST)

        [Fact]
        public async Task LockUnlock_LockRegularUser_ReturnsSuccess()
        {
            // Arrange
            string userId = null;
            using (var scope = Factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                var user = new AppUser { UserName = "lockuser@test.com", Email = "lockuser@test.com", FirstName = "Test", LastName = "User" };
                await userManager.CreateAsync(user, "Password123!");
                userId = user.Id;
            }

            var client = CreateAdminClient();
            var requestContent = new StringContent($"\"{userId}\"", Encoding.UTF8, "application/json");

            // Act
            var response = await client.PostAsync("/Admin/User/LockUnlock", requestContent);
            var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(bool.Parse(json["success"].ToString()));
            Assert.Equal("Operation Successful", json["message"].ToString());

            using (var scope = Factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                var user = await userManager.FindByIdAsync(userId);
                Assert.NotNull(user.LockoutEnd);
                Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow);
            }
        }

        [Fact]
        public async Task LockUnlock_UnlockRegularUser_ReturnsSuccess()
        {
            // Arrange
            string userId = null;
            using (var scope = Factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                var user = new AppUser { UserName = "unlockuser@test.com", Email = "unlockuser@test.com", FirstName = "Test", LastName = "User" };
                await userManager.CreateAsync(user, "Password123!");
                await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(2));
                userId = user.Id;
            }

            var client = CreateAdminClient();
            var requestContent = new StringContent($"\"{userId}\"", Encoding.UTF8, "application/json");

            // Act
            var response = await client.PostAsync("/Admin/User/LockUnlock", requestContent);
            var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(bool.Parse(json["success"].ToString()));
            Assert.Equal("Operation Successful", json["message"].ToString());

            using (var scope = Factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                var user = await userManager.FindByIdAsync(userId);
                Assert.Null(user.LockoutEnd);
            }
        }

        [Fact]
        public async Task LockUnlock_AdminUser_ReturnsFailure()
        {
            string adminId = null;
            using (var scope = Factory.Services.CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

                if (!await roleManager.RoleExistsAsync(SD.Admin))
                {
                    await roleManager.CreateAsync(new IdentityRole(SD.Admin));
                }

                var admin = new AppUser { UserName = "admin@test.com", Email = "admin@test.com", FirstName = "Admin", LastName = "User" };
                await userManager.CreateAsync(admin, "Password123!");
                await userManager.AddToRoleAsync(admin, SD.Admin);
                adminId = admin.Id;
            }

            var client = CreateAdminClient();
            var requestContent = new StringContent($"\"{adminId}\"", Encoding.UTF8, "application/json");

            // Act
            var response = await client.PostAsync("/Admin/User/LockUnlock", requestContent);
            var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(bool.Parse(json["success"].ToString()));
            Assert.Equal("Cannot lock/unlock an admin user.", json["message"].ToString());

            using (var scope = Factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                var admin = await userManager.FindByIdAsync(adminId);
                Assert.Null(admin.LockoutEnd);
            }
        }

        [Fact]
        public async Task LockUnlock_UserNotFound_ReturnsFailure()
        {
            var client = CreateAdminClient();
            var requestContent = new StringContent($"\"non-existent-id\"", Encoding.UTF8, "application/json");

            // Act
            var response = await client.PostAsync("/Admin/User/LockUnlock", requestContent);
            var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(bool.Parse(json["success"].ToString()));
            Assert.Equal("User not found", json["message"].ToString());
        }

        [Fact]
        public async Task LockUnlock_UnauthorizedUser_ReturnsRedirectToLogin()
        {
            var client = CreateUnauthenticatedClient();
            var requestContent = new StringContent($"\"some-id\"", Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/Admin/User/LockUnlock", requestContent);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location;
            Assert.NotNull(location);
            Assert.Equal("/Identity/Account/Login", location.LocalPath);
        }

        [Fact]
        public async Task LockUnlock_UserWithoutAdminRole_ReturnsForbidden()
        {
            var client = CreateAuthenticatedClient(); 
            var requestContent = new StringContent($"\"some-id\"", Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/Admin/User/LockUnlock", requestContent);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        #endregion

        #region RoleManagmentAsync (GET)

        [Fact]
        public async Task RoleManagment_Get_ValidUser_ReturnsViewWithRoles()
        {
            // Arrange
            string userId = null;
            using (var scope = Factory.Services.CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

                if (!await roleManager.RoleExistsAsync(SD.Admin))
                    await roleManager.CreateAsync(new IdentityRole(SD.Admin));
                if (!await roleManager.RoleExistsAsync(SD.Customer))
                    await roleManager.CreateAsync(new IdentityRole(SD.Customer));

                var user = new AppUser { UserName = "roleuser@test.com", Email = "roleuser@test.com", FirstName = "Role", LastName = "User" };
                await userManager.CreateAsync(user, "Password123!");
                await userManager.AddToRoleAsync(user, SD.Customer);
                userId = user.Id;
            }

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync($"/Admin/User/RoleManagment?userId={userId}");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Manage User Role", content);
            Assert.Contains("Customer", content); 
        }

        [Fact]
        public async Task RoleManagment_Get_InvalidUser_ReturnsNotFound()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/User/RoleManagment?userId=invalid-id");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task RoleManagment_Get_WithoutUserId_ReturnsNotFound()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/User/RoleManagment");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); 
        }

        #endregion

        #region RoleManagmentAsync (POST)

        [Fact]
        public async Task RoleManagment_Post_ValidRoleChange_RedirectsToIndexWithSuccess()
        {
            // Arrange
            string userId = null;
            using (var scope = Factory.Services.CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

                if (!await roleManager.RoleExistsAsync(SD.Admin))
                    await roleManager.CreateAsync(new IdentityRole(SD.Admin));
                if (!await roleManager.RoleExistsAsync(SD.Customer))
                    await roleManager.CreateAsync(new IdentityRole(SD.Customer));

                var user = new AppUser { UserName = "rolepost@test.com", Email = "rolepost@test.com", FirstName = "Role", LastName = "Post" };
                await userManager.CreateAsync(user, "Password123!");
                await userManager.AddToRoleAsync(user, SD.Customer);
                userId = user.Id;
            }

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/User/RoleManagment?userId={userId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["ApplicationUser.Id"] = userId,
                ["ApplicationUser.Role"] = SD.Admin
            };
            var postContent = new FormUrlEncodedContent(formData);
            postContent.Headers.Add("RequestVerificationToken", token);

            // Act
            var response = await client.PostAsync("/Admin/User/RoleManagment", postContent);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Admin/User", response.Headers.Location?.OriginalString);

            using (var scope = Factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                var user = await userManager.FindByIdAsync(userId);
                var roles = await userManager.GetRolesAsync(user);
                Assert.Contains(SD.Admin, roles);
                Assert.DoesNotContain(SD.Customer, roles);
            }
        }

        [Fact]
        public async Task RoleManagment_Post_EmptyRole_ReturnsViewWithError()
        {
            // Arrange
            string userId = null;
            using (var scope = Factory.Services.CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

                if (!await roleManager.RoleExistsAsync(SD.Customer))
                    await roleManager.CreateAsync(new IdentityRole(SD.Customer));

                var user = new AppUser { UserName = "emptyrole@test.com", Email = "emptyrole@test.com", FirstName = "Empty", LastName = "Role" };
                await userManager.CreateAsync(user, "Password123!");
                userId = user.Id;
            }

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/User/RoleManagment?userId={userId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["ApplicationUser.Id"] = userId,
                ["ApplicationUser.Role"] = "" 
            };
            var postContent = new FormUrlEncodedContent(formData);
            postContent.Headers.Add("RequestVerificationToken", token);

            // Act
            var response = await client.PostAsync("/Admin/User/RoleManagment", postContent);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Please select a role.", responseContent);
        }

        [Fact]
        public async Task RoleManagment_Post_InvalidUser_ReturnsNotFound()
        {
            string validUserId = null;
            using (var scope = Factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                var user = new AppUser { UserName = "temp@test.com", Email = "temp@test.com", FirstName = "Temp", LastName = "User" };
                await userManager.CreateAsync(user, "Password123!");
                validUserId = user.Id;
            }

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/User/RoleManagment?userId={validUserId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["ApplicationUser.Id"] = "non-existent-id",
                ["ApplicationUser.Role"] = SD.Admin
            };
            var postContent = new FormUrlEncodedContent(formData);
            postContent.Headers.Add("RequestVerificationToken", token);

            var response = await client.PostAsync("/Admin/User/RoleManagment", postContent);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task RoleManagment_Post_UnauthorizedUser_ReturnsRedirectToLogin()
        {
            var client = CreateUnauthenticatedClient();
            var formData = new Dictionary<string, string>
            {
                ["ApplicationUser.Id"] = "some-id",
                ["ApplicationUser.Role"] = SD.Admin
            };
            var postContent = new FormUrlEncodedContent(formData);
            var response = await client.PostAsync("/Admin/User/RoleManagment", postContent);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location;
            Assert.NotNull(location);
            Assert.Equal("/Identity/Account/Login", location.LocalPath);
        }

        [Fact]
        public async Task RoleManagment_Post_UserWithoutAdminRole_ReturnsForbidden()
        {
            var client = CreateAuthenticatedClient(); 
            var formData = new Dictionary<string, string>
            {
                ["ApplicationUser.Id"] = "some-id",
                ["ApplicationUser.Role"] = SD.Admin
            };
            var postContent = new FormUrlEncodedContent(formData);
            var response = await client.PostAsync("/Admin/User/RoleManagment", postContent);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        #endregion

        #region Helper

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