using BookFlightTickets.Core.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace BookFlightTickets.Core.ViewModels
{
    public class AirlineViewModel
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Name is required")]
        [StringLength(50, ErrorMessage = "Name cannot exceed 50 characters")]
        public string Name { get; set; } = default!;
        [Required(ErrorMessage = "Code is required")]
        [StringLength(10, ErrorMessage = "Code cannot exceed 10 characters")]
        public string Code { get; set; } = default!;

        #region Mapping

        public static explicit operator AirlineViewModel(Airline model)
        {
            return new AirlineViewModel
            {
                Id = model.Id,
                Name = model.Name,
                Code = model.Code,
            };
        }

        public static explicit operator Airline(AirlineViewModel ViewModel)
        {
            return new Airline
            {
                Id = ViewModel.Id,
                Name = ViewModel.Name,
                Code = ViewModel.Code,
            };
        }

        #endregion

    }
}
