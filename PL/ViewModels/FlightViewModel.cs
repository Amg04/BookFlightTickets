using DAL.models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PL.CustomValidationAttributes;
using System.ComponentModel.DataAnnotations;

namespace PL.ViewModels
{
    public class FlightViewModel : IValidatableObject
    {
        public int Id { get; set; }
        [Required]
        public int AirlineId { get; set; }
        [ValidateNever]
        public Airline Airline { get; set; } = null!;
        [Required]
        public int AirplaneId { get; set; }
        [ValidateNever]
        public Airplane Airplane { get; set; } = null!;
        [Required]
        public int DepartureAirportID { get; set; }
        [ValidateNever]
        public Airport DepartureAirport { get; set; } = null!;
        [Required]
        public int ArrivalAirportID { get; set; }
        [ValidateNever]
        public Airport ArrivalAirport { get; set; } = null!;
        [Required]
        [DataType(DataType.DateTime)]
        public DateTime DepartureTime { get; set; }
        [Required]
        [DataType(DataType.DateTime)]
        [GreaterThan(nameof(DepartureTime), ErrorMessage = "Arrival time must be after departure time.")]
        public DateTime ArrivalTime { get; set; }
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Base price must be greater than zero.")]
        [Display(Name = "Base Price")]
        public decimal BasePrice { get; set; }
        [Required]
        public FlightStatus Status { get; set; } = FlightStatus.Scheduled;
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ArrivalAirportID == DepartureAirportID)
            {
                yield return new ValidationResult(
                    "Arrival Airport must be different from Departure Airport.",
                    new[] { nameof(ArrivalAirportID), nameof(DepartureAirportID) }
                );
            }
        }

        #region Mapping

        public static explicit operator FlightViewModel(Flight model)
        {
            return new FlightViewModel
            {
                Id = model.Id,
                AirlineId = model.AirlineId,
                Airline = model.Airline,
                AirplaneId = model.AirplaneId,
                Airplane = model.Airplane,
                DepartureAirportID = model.DepartureAirportID,
                DepartureAirport = model.DepartureAirport,
                ArrivalAirportID = model.ArrivalAirportID,
                ArrivalAirport = model.ArrivalAirport,
                DepartureTime = model.DepartureTime,
                ArrivalTime = model.ArrivalTime,
                BasePrice = model.BasePrice,
                Status = model.Status,
            };
        }

        public static explicit operator Flight(FlightViewModel ViewModel)
        {
            return new Flight
            {
                Id = ViewModel.Id,
                AirlineId = ViewModel.AirlineId,
                AirplaneId = ViewModel.AirplaneId,
                DepartureAirportID = ViewModel.DepartureAirportID,
                ArrivalAirportID = ViewModel.ArrivalAirportID,
                DepartureTime = ViewModel.DepartureTime,
                ArrivalTime = ViewModel.ArrivalTime,
                BasePrice = ViewModel.BasePrice,
                Status = ViewModel.Status,
            };
        }

        #endregion

    }
}
