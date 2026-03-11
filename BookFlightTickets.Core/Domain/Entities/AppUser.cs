using Microsoft.AspNetCore.Identity;

namespace BookFlightTickets.Core.Domain.Entities
{
    public class AppUser : IdentityUser
    {
        public string FirstName { get; set; } = default!;
        public string? LastName { get; set; }
        public string? PassportNumber { get; set; }
        public string Role { get; set; } = string.Empty;
        public ICollection<Booking> Bookings { get; set; } = new HashSet<Booking>();
    }
}
