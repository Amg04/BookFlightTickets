using DAL.models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PL.ViewModels
{
    public class BookingViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Flight ID is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Flight ID must be greater than zero")]
        public int FlightId { get; set; }

        public DateTime BookingDate { get; set; } = DateTime.UtcNow;

        [Required(ErrorMessage = "PNR is required")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "PNR must be exactly 6 characters")]
        [RegularExpression(@"^[A-Z0-9]{6}$", ErrorMessage = "PNR must contain only uppercase letters or digits (6 characters)")]
        public string PNR { get; set; }

        [Required(ErrorMessage = "Total price is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Total price must be greater than zero")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        [Required(ErrorMessage = "Booking status is required")]
        public Status Status { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [ValidateNever]
        public Payment? Payment { get; set; }

        #region Mapping

        public static explicit operator BookingViewModel(Booking model)
        {
            return new BookingViewModel
            {
                Id = model.Id,
                FlightId = model.FlightId,
                BookingDate = model.BookingDate,
                PNR = model.PNR,
                TotalPrice = model.TotalPrice,
                Status = model.Status,
                LastUpdated = model.LastUpdated,
                Payment = model.Payment,
            };
        }

        public static explicit operator Booking(BookingViewModel ViewModel)
        {
            return new Booking
            {
                Id = ViewModel.Id,
                FlightId = ViewModel.FlightId,
                BookingDate = ViewModel.BookingDate,
                PNR = ViewModel.PNR,
                TotalPrice = ViewModel.TotalPrice,
                Status = ViewModel.Status,
                LastUpdated = ViewModel.LastUpdated,
            };
        }

        #endregion
    }
}
