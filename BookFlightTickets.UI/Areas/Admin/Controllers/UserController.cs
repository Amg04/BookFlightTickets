using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BookFlightTickets.UI.Areas.Admin.Controllers
{
    [Area(SD.Admin)]
    [Authorize(Roles = SD.Admin)]
    public class UserController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserController(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        #region Index

        public async Task<IActionResult> Index()
        {
            List<AppUser> usersList =  await _userManager.Users.ToListAsync();
            return View(usersList.Select(u => (UserViewModel)u));
        }

        #endregion

        #region LockUnlockAsync

        [HttpPost]
        public async Task<IActionResult> LockUnlockAsync([FromBody] string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found" });
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Admin"))
            {
                return Json(new { success = false, message = "Cannot lock/unlock an admin user." });
            }

            if (user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.Now)
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(2));
            }
            if (!user.LockoutEnabled)
            {
                await _userManager.SetLockoutEnabledAsync(user, true);
            }
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return Json(new { success = false, message = "Error while locking/unlocking" });
            }

            return Json(new { success = true, message = "Operation Successful" });
        }

        #endregion

        #region RoleManagmentAsync

        [HttpGet]
        public async Task<IActionResult> RoleManagmentAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var vm = await BuildRoleManagmentVMAsync(userId);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RoleManagmentAsync(RoleManagmentVM roleManagementVM)
        {
            var user = await _userManager.FindByIdAsync(roleManagementVM.ApplicationUser.Id);
            if (user == null) return NotFound();

            if (string.IsNullOrEmpty(roleManagementVM.ApplicationUser.Role))
            {
                ModelState.AddModelError("", "Please select a role.");
                var vm = await BuildRoleManagmentVMAsync(user.Id);
                return View(vm);
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var newRole = roleManagementVM.ApplicationUser.Role;

            if (!currentRoles.Contains(newRole))
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                user.Role = newRole;
                await _userManager.UpdateAsync(user);
                await _userManager.AddToRoleAsync(user, newRole);
                TempData["success"] = "Permission updated successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<RoleManagmentVM> BuildRoleManagmentVMAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            var roles = _roleManager.Roles.ToList();
            var userRoles = await _userManager.GetRolesAsync(user);

            return new RoleManagmentVM
            {
                ApplicationUser = user,
                RoleList = roles.Select(r => new SelectListItem
                {
                    Text = r.Name,
                    Value = r.Name
                }),
            };
        }

        #endregion

    }
}
