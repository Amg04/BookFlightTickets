using BookFlightTickets.Core.Domain.Entities;

namespace BookFlightTickets.Core.ViewModels
{
    public class UserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? LastName { get; set; } 
        public string Email { get; set; } = string.Empty ;
        public string? Phone { get; set; }
        public string PassportNumber { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTimeOffset? LockoutEnd { get; set; }

        #region Mapping

        public static explicit operator UserViewModel(AppUser model)
        {
            return new UserViewModel
            {
                Id = model.Id,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Email ?? "N/A",
                Phone = model.PhoneNumber,
                PassportNumber = model.PassportNumber ?? "N/A",
                LockoutEnd = model.LockoutEnd,
                Role = model.Role
            };
        }

        #endregion

    }
}
