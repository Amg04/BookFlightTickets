using DAL.models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace PL.ViewModels
{
    public class BookingCreateViewModel
    {
        public int FlightId { get; set; }
        public Flight? Flight { get; set; }
        public List<SelectListItem> AvailableSeats { get; set; } = new();
        public List<AddOn> AddOns { get; set; } = new();
        public List<TicketBookingVM>? Tickets { get; set; } 

    }

    public class TicketBookingVM
    {
        [Required(ErrorMessage = "Please select a seat.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid seat.")]
        public int SelectedSeatId { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 50 characters.")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters.")]
        [Display(Name = "Last Name")]
        public string? LastName { get; set; }
        [Display(Name = "Ticket Total Price")]
        public decimal TicketPrice { get; set; } = 0;
        [Required(ErrorMessage = "Passport number is required.")]
        [RegularExpression(@"^[A-PR-WY][1-9]\d\s?\d{4}[1-9]$",ErrorMessage = "Invalid Passport Number format.")]
        [StringLength(12, MinimumLength = 8, ErrorMessage = "Passport number must be 8-12 characters.")]
        [Display(Name = "Passport Number")]
        public string PassportNumber { get; set; } = string.Empty;

        [MinLength(0)] // Optional: Allow empty list
        public List<int> SelectedAddOnIds { get; set; } = new();
    }
}