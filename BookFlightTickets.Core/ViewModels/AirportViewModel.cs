using System.ComponentModel.DataAnnotations;
using BookFlightTickets.Core.Domain.Entities;
namespace BookFlightTickets.Core.ViewModels
{
    public class AirportViewModel
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Airport name is required")]
        [StringLength(100, ErrorMessage = "Airport name cannot exceed 100 characters")]
        public string Name { get; set; } = default!;
        [Required(ErrorMessage = "Airport code is required")]
        [RegularExpression(@"^[A-Z]{3,4}$", ErrorMessage = "Airport code must be 3 or 4 uppercase letters")]
        public string Code { get; set; } = default!;
        [StringLength(50, ErrorMessage = "City name cannot exceed 50 characters")]
        public string? City { get; set; }
        [StringLength(50, ErrorMessage = "Country name cannot exceed 50 characters")]
        public string? Country { get; set; }

        #region Mapping

        public static explicit operator AirportViewModel(Airport model)
        {
            return new AirportViewModel
            {
                Id = model.Id,
                Name = model.Name,
                Code = model.Code,
                City = model.City,
                Country = model.Country,
            };
        }

        public static explicit operator Airport(AirportViewModel viewModel)
        {
            return new Airport
            {
                Id = viewModel.Id,
                Name = viewModel.Name,
                Code = viewModel.Code,
                City = viewModel.City,
                Country = viewModel.Country,
            };
        }

        #endregion
    }
}
