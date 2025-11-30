using DAL.models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace PL.ViewModels
{
    public class AirplaneViewModel
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Airline is required")]
        public int AirlineId { get; set; }
        [Required(ErrorMessage = "Model is required")]
        [StringLength(50, ErrorMessage = "Model cannot exceed 50 characters")]
        public string Model { get; set; }
        [Required(ErrorMessage = "Seat capacity is required")]
        [Range(1, 1000, ErrorMessage = "Seat capacity must be between 1 and 1000")]
        public short SeatCapacity { get; set; }
        [ValidateNever]
        public Airline Airline { get; set; } = null!;

        #region Mapping

        public static explicit operator AirplaneViewModel(Airplane model)
        {
            return new AirplaneViewModel
            {
                Id = model.Id,
                AirlineId = model.AirlineId,
                Model = model.Model,
                SeatCapacity = model.SeatCapacity,
                Airline = model.Airline
            };
        }

        public static explicit operator Airplane(AirplaneViewModel ViewModel)
        {
            return new Airplane
            {
                Id = ViewModel.Id,
                AirlineId = ViewModel.AirlineId,
                Model = ViewModel.Model,
                SeatCapacity = ViewModel.SeatCapacity,
            };
        }

        #endregion

    }
}
