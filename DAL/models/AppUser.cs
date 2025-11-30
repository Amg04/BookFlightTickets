using DAL.models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BAL.model
{
    public class AppUser : IdentityUser
    {
        public string FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PassportNumber { get; set; }

        [ValidateNever]
        public ICollection<Booking> Bookings { get; set; } = new HashSet<Booking>();
    }
}
