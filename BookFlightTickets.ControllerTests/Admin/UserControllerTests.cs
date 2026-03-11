using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.ViewModels;
using BookFlightTickets.UI.Areas.Admin.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Linq.Expressions;
using System.Text.Json;

namespace BookFlightTickets.ControllerTests.Admin
{
    // Helper classes to support async IQueryable mocking
    public class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
        public TestAsyncEnumerable(Expression expression) : base(expression) { }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
        }

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
    }

    public class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;

        public TestAsyncEnumerator(IEnumerator<T> inner)
        {
            _inner = inner;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return ValueTask.FromResult(_inner.MoveNext());
        }

        public T Current => _inner.Current;

        public ValueTask DisposeAsync()
        {
            _inner.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    public class TestAsyncQueryProvider<T> : IQueryProvider
    {
        private readonly IQueryProvider _inner;

        public TestAsyncQueryProvider(IQueryProvider inner)
        {
            _inner = inner;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            return new TestAsyncEnumerable<T>(expression);
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new TestAsyncEnumerable<TElement>(expression);
        }

        public object Execute(Expression expression)
        {
            return _inner.Execute(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return _inner.Execute<TResult>(expression);
        }
    }

    public static class AsyncQueryableExtensions
    {
        public static IQueryable<T> AsAsyncQueryable<T>(this IEnumerable<T> source)
        {
            return new TestAsyncEnumerable<T>(source);
        }
    }

    public class UserControllerTests
    {
        private readonly Mock<UserManager<AppUser>> _userManagerMock;
        private readonly Mock<RoleManager<IdentityRole>> _roleManagerMock;
        private readonly UserController _controller;
        private readonly ITempDataDictionary _tempData;

        public UserControllerTests()
        {
            // Mock UserManager dependencies
            var userStoreMock = new Mock<IUserStore<AppUser>>();
            var optionsMock = new Mock<IOptions<IdentityOptions>>();
            var passwordHasherMock = new Mock<IPasswordHasher<AppUser>>();
            var userValidators = new List<IUserValidator<AppUser>>();
            var passwordValidators = new List<IPasswordValidator<AppUser>>();
            var lookupNormalizerMock = new Mock<ILookupNormalizer>();
            var identityErrorDescriberMock = new Mock<IdentityErrorDescriber>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            var loggerUserManagerMock = new Mock<ILogger<UserManager<AppUser>>>();

            _userManagerMock = new Mock<UserManager<AppUser>>(
                userStoreMock.Object,
                optionsMock.Object,
                passwordHasherMock.Object,
                userValidators,
                passwordValidators,
                lookupNormalizerMock.Object,
                identityErrorDescriberMock.Object,
                serviceProviderMock.Object,
                loggerUserManagerMock.Object);

            // Mock RoleManager dependencies
            var roleStoreMock = new Mock<IRoleStore<IdentityRole>>();
            var roleValidators = new List<IRoleValidator<IdentityRole>>();
            var loggerRoleManagerMock = new Mock<ILogger<RoleManager<IdentityRole>>>();

            _roleManagerMock = new Mock<RoleManager<IdentityRole>>(
                roleStoreMock.Object,
                roleValidators,
                lookupNormalizerMock.Object,
                identityErrorDescriberMock.Object,
                loggerRoleManagerMock.Object);

            // Create controller instance
            _controller = new UserController(
                _userManagerMock.Object,
                _roleManagerMock.Object
            );

            // Setup TempData
            _tempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
            _controller.TempData = _tempData;
        }

        #region Index

        [Fact]
        public async Task Index_ShouldReturnViewWithUsers()
        {
            // Arrange
            var users = new List<AppUser>
            {
                new AppUser { Id = "1", UserName = "user1", Email = "user1@test.com", FirstName = "Zyad" },
                new AppUser { Id = "2", UserName = "user2", Email = "user2@test.com", FirstName = "Ahmed" }
            };

            // Use the custom async queryable wrapper
            var mockUserQueryable = users.AsAsyncQueryable();
            _userManagerMock.Setup(u => u.Users).Returns(mockUserQueryable);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<UserViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count());
        }

        #endregion

        #region LockUnlockAsync

        [Fact]
        public async Task LockUnlockAsync_InvalidId_ShouldReturnJsonError()
        {
            // Arrange
            string userId = "nonexistent";
            _userManagerMock.Setup(u => u.FindByIdAsync(userId)).ReturnsAsync((AppUser)null);

            // Act
            var result = await _controller.LockUnlockAsync(userId);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.False(root.GetProperty("success").GetBoolean());
            Assert.Equal("User not found", root.GetProperty("message").GetString());
        }

        [Fact]
        public async Task LockUnlockAsync_AdminUser_ShouldReturnJsonError()
        {
            // Arrange
            string userId = "admin1";
            var user = new AppUser { Id = userId, UserName = "admin" };
            _userManagerMock.Setup(u => u.FindByIdAsync(userId)).ReturnsAsync(user);
            _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });

            // Act
            var result = await _controller.LockUnlockAsync(userId);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.False(root.GetProperty("success").GetBoolean());
            Assert.Equal("Cannot lock/unlock an admin user.", root.GetProperty("message").GetString());
        }

        [Fact]
        public async Task LockUnlockAsync_UserCurrentlyLocked_ShouldUnlock()
        {
            // Arrange
            string userId = "user1";
            var user = new AppUser
            {
                Id = userId,
                UserName = "user",
                LockoutEnd = DateTimeOffset.UtcNow.AddDays(1),
                FirstName = "Ali",
                LockoutEnabled = true 
            };
            _userManagerMock.Setup(u => u.FindByIdAsync(userId)).ReturnsAsync(user);
            _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User" });

            _userManagerMock.Setup(u => u.SetLockoutEndDateAsync(user, null))
                .ReturnsAsync(IdentityResult.Success);
            _userManagerMock.Setup(u => u.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _controller.LockUnlockAsync(userId);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.Equal("Operation Successful", root.GetProperty("message").GetString());

            _userManagerMock.Verify(u => u.SetLockoutEndDateAsync(user, null), Times.Once);
            _userManagerMock.Verify(u => u.SetLockoutEnabledAsync(user, true), Times.Never);
            _userManagerMock.Verify(u => u.UpdateAsync(user), Times.Once);
        }

        [Fact]
        public async Task LockUnlockAsync_UserNotLocked_ShouldLock()
        {
            // Arrange
            string userId = "user1";
            var user = new AppUser { Id = userId, UserName = "user", LockoutEnd = null, LockoutEnabled = true };
            _userManagerMock.Setup(u => u.FindByIdAsync(userId)).ReturnsAsync(user);
            _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User" });

            _userManagerMock.Setup(u => u.SetLockoutEndDateAsync(user, It.IsAny<DateTimeOffset>()))
                .ReturnsAsync(IdentityResult.Success);
            _userManagerMock.Setup(u => u.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _controller.LockUnlockAsync(userId);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.Equal("Operation Successful", root.GetProperty("message").GetString());

            _userManagerMock.Verify(u => u.SetLockoutEndDateAsync(user, It.Is<DateTimeOffset>(d => d > DateTimeOffset.UtcNow.AddYears(1))), Times.Once);
            _userManagerMock.Verify(u => u.SetLockoutEnabledAsync(user, true), Times.Never);
            _userManagerMock.Verify(u => u.UpdateAsync(user), Times.Once);
        }

        [Fact]
        public async Task LockUnlockAsync_WhenLockoutDisabled_ShouldEnableLockout()
        {
            // Arrange
            string userId = "user1";
            var user = new AppUser { Id = userId, UserName = "user", LockoutEnd = null, LockoutEnabled = false };
            _userManagerMock.Setup(u => u.FindByIdAsync(userId)).ReturnsAsync(user);
            _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User" });

            _userManagerMock.Setup(u => u.SetLockoutEndDateAsync(user, It.IsAny<DateTimeOffset>()))
                .ReturnsAsync(IdentityResult.Success);
            _userManagerMock.Setup(u => u.SetLockoutEnabledAsync(user, true))
                .ReturnsAsync(IdentityResult.Success);
            _userManagerMock.Setup(u => u.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _controller.LockUnlockAsync(userId);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.Equal("Operation Successful", root.GetProperty("message").GetString());

            _userManagerMock.Verify(u => u.SetLockoutEndDateAsync(user, It.IsAny<DateTimeOffset>()), Times.Once);
            _userManagerMock.Verify(u => u.SetLockoutEnabledAsync(user, true), Times.Once);
            _userManagerMock.Verify(u => u.UpdateAsync(user), Times.Once);
        }

        [Fact]
        public async Task LockUnlockAsync_UpdateFails_ShouldReturnJsonError()
        {
            // Arrange
            string userId = "user1";
            var user = new AppUser { Id = userId, UserName = "user", LockoutEnd = null };
            _userManagerMock.Setup(u => u.FindByIdAsync(userId)).ReturnsAsync(user);
            _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User" });

            _userManagerMock.Setup(u => u.SetLockoutEndDateAsync(user, It.IsAny<DateTimeOffset>()))
                .ReturnsAsync(IdentityResult.Success);
            _userManagerMock.Setup(u => u.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Update failed" }));

            // Act
            var result = await _controller.LockUnlockAsync(userId);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.False(root.GetProperty("success").GetBoolean());
            Assert.Equal("Error while locking/unlocking", root.GetProperty("message").GetString());
        }

        #endregion

        #region RoleManagment (GET)

        [Fact]
        public async Task RoleManagment_Get_InvalidUserId_ShouldReturnNotFound()
        {
            // Arrange
            string userId = "invalid";
            _userManagerMock.Setup(u => u.FindByIdAsync(userId)).ReturnsAsync((AppUser)null);

            // Act
            var result = await _controller.RoleManagmentAsync(userId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task RoleManagment_Get_ValidUserId_ShouldReturnViewWithVM()
        {
            // Arrange
            string userId = "1";
            var user = new AppUser { Id = userId, UserName = "user1" };
            var roles = new List<IdentityRole>
            {
                new IdentityRole { Name = "Admin" },
                new IdentityRole { Name = "User" }
            };
            var userRoles = new List<string> { "User" };

            _userManagerMock.Setup(u => u.FindByIdAsync(userId)).ReturnsAsync(user);
            _roleManagerMock.Setup(r => r.Roles).Returns(roles.AsQueryable());
            _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(userRoles);

            // Act
            var result = await _controller.RoleManagmentAsync(userId);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<RoleManagmentVM>(viewResult.Model);
            Assert.Equal(user, model.ApplicationUser);
            Assert.Equal(2, model.RoleList.Count());
        }

        #endregion

        #region RoleManagment (POST)

        [Fact]
        public async Task RoleManagment_Post_UserNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var vm = new RoleManagmentVM
            {
                ApplicationUser = new AppUser { Id = "1" }
            };
            _userManagerMock.Setup(u => u.FindByIdAsync(vm.ApplicationUser.Id)).ReturnsAsync((AppUser)null);

            // Act
            var result = await _controller.RoleManagmentAsync(vm);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task RoleManagment_Post_NoRoleSelected_ShouldAddModelErrorAndReturnView()
        {
            // Arrange
            string userId = "1";
            var user = new AppUser { Id = userId, UserName = "user1" };
            var vm = new RoleManagmentVM
            {
                ApplicationUser = new AppUser { Id = userId, Role = "" }
            };

            _userManagerMock.Setup(u => u.FindByIdAsync(userId)).ReturnsAsync(user);
            _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string>());
            var roles = new List<IdentityRole>
            {
                new IdentityRole { Name = "Admin" },
                new IdentityRole { Name = "User" }
            };
            _roleManagerMock.Setup(r => r.Roles).Returns(roles.AsQueryable());

            // Act
            var result = await _controller.RoleManagmentAsync(vm);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<RoleManagmentVM>(viewResult.Model);
            Assert.Equal(userId, model.ApplicationUser.Id);
            Assert.NotNull(model.RoleList);
            Assert.Equal(2, model.RoleList.Count()); // assuming roles list has 2 items
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("Please select a role.", _controller.ModelState[""].Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task RoleManagment_Post_ValidRole_ShouldUpdateRoleAndRedirect()
        {
            // Arrange
            string userId = "1";
            var user = new AppUser { Id = userId, UserName = "user1", Role = "User" };
            var vm = new RoleManagmentVM
            {
                ApplicationUser = new AppUser { Id = userId, Role = "Admin" }
            };
            var currentRoles = new List<string> { "User" };

            _userManagerMock.Setup(u => u.FindByIdAsync(userId)).ReturnsAsync(user);
            _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(currentRoles);
            _userManagerMock.Setup(u => u.RemoveFromRolesAsync(user, currentRoles)).ReturnsAsync(IdentityResult.Success);
            _userManagerMock.Setup(u => u.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
            _userManagerMock.Setup(u => u.AddToRoleAsync(user, "Admin")).ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _controller.RoleManagmentAsync(vm);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(UserController.Index), redirectResult.ActionName);
            Assert.Equal("Permission updated successfully.", _controller.TempData["success"]);

            Assert.Equal("Admin", user.Role);
            _userManagerMock.Verify(u => u.RemoveFromRolesAsync(user, currentRoles), Times.Once);
            _userManagerMock.Verify(u => u.UpdateAsync(user), Times.Once);
            _userManagerMock.Verify(u => u.AddToRoleAsync(user, "Admin"), Times.Once);
        }

        [Fact]
        public async Task RoleManagment_Post_SameRole_ShouldNotUpdateAndRedirect()
        {
            // Arrange
            string userId = "1";
            var user = new AppUser { Id = userId, UserName = "user1", Role = "User" };
            var vm = new RoleManagmentVM
            {
                ApplicationUser = new AppUser { Id = userId, Role = "User" }
            };
            var currentRoles = new List<string> { "User" };

            _userManagerMock.Setup(u => u.FindByIdAsync(userId)).ReturnsAsync(user);
            _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(currentRoles);

            // Act
            var result = await _controller.RoleManagmentAsync(vm);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(UserController.Index), redirectResult.ActionName);
            Assert.Null(_controller.TempData["success"]);

            _userManagerMock.Verify(u => u.RemoveFromRolesAsync(It.IsAny<AppUser>(), It.IsAny<IEnumerable<string>>()), Times.Never);
            _userManagerMock.Verify(u => u.UpdateAsync(It.IsAny<AppUser>()), Times.Never);
            _userManagerMock.Verify(u => u.AddToRoleAsync(It.IsAny<AppUser>(), It.IsAny<string>()), Times.Never);
        }

        #endregion
    }
}