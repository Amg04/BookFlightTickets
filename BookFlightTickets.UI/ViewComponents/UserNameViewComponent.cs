using BookFlightTickets.Core.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BookFlightTickets.UI.ViewComponent
{
    public class UserNameViewComponent : Microsoft.AspNetCore.Mvc.ViewComponent
    {
        private readonly UserManager<AppUser> _userManager;
        public UserNameViewComponent(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var displayName = string.Empty;
            if (user != null)
            {
                if (!string.IsNullOrWhiteSpace(user.FirstName) || !string.IsNullOrWhiteSpace(user.LastName))
                {
                    displayName = string.Concat(user.FirstName, " ", user.LastName).Trim();
                }
                else if (!string.IsNullOrWhiteSpace(user.UserName))
                {
                    displayName = user.UserName;
                }
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = HttpContext.User.Identity?.Name;
            }

            return View("Default", displayName);
        }
    }
}
