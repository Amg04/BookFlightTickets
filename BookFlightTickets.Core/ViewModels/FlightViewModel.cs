using BookFlightTickets.Core.CustomValidationAttributes;
using BookFlightTickets.Core.Domain.Entities;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
namespace BookFlightTickets.Core.ViewModels
{
    public class FlightViewModel : IValidatableObject
    {
        public int Id { get; set; }
        [Display(Name = "Airline")]
        [Required]
        public int AirlineId { get; set; }
        [ValidateNever]
        public AirlineViewModel Airline { get; set; } = null!;
        [Required]
        [Display(Name = "Airplane")]
        public int AirplaneId { get; set; }
        [ValidateNever]
        public AirplaneViewModel Airplane { get; set; } = null!;
        [Display(Name = "Departure Airport")]
        [Required]
        public int DepartureAirportID { get; set; }
        [ValidateNever]
        public AirportViewModel DepartureAirport { get; set; } = null!;
        [Display(Name = "Arrival Airport")]
        [Required]
        public int ArrivalAirportID { get; set; }
        [ValidateNever]
        public AirportViewModel ArrivalAirport { get; set; } = null!;
        [Required]
        [DataType(DataType.DateTime)]
        public DateTime DepartureTime { get; set; }
        [Required]
        [DataType(DataType.DateTime)]
        [GreaterThan(nameof(DepartureTime), ErrorMessage = "Arrival time must be after departure time.")]
        public DateTime ArrivalTime { get; set; }
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Base Price must be greater than zero.")]
        [Display(Name = "Base Price")]
        public decimal BasePrice { get; set; }
        [Required]
        public FlightStatus Status { get; set; } = FlightStatus.Scheduled;
        [ValidateNever]
        public ICollection<FlightSeat> FlightSeats { get; set; } = new HashSet<FlightSeat>();
        public int AvailableSeatsCount  => FlightSeats?.Count(s => s.IsAvailable) ?? 0;


        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ArrivalAirportID == DepartureAirportID)
            {
                yield return new ValidationResult(
                    "Arrival Airport must be different from Departure Airport.",

                   // asp - validation -for= "ArrivalAirportID" displays the error.
                   //asp - validation -for= "DepartureAirportID" also displays the same error.
                   //new[] { nameof(ArrivalAirportID), nameof(DepartureAirportID) }

                   new[] { nameof(ArrivalAirportID) }
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
                Airline = (AirlineViewModel)model.Airline,
                AirplaneId = model.AirplaneId,
                Airplane = (AirplaneViewModel)model.Airplane,
                DepartureAirportID = model.DepartureAirportID,
                DepartureAirport = (AirportViewModel)model.DepartureAirport,
                ArrivalAirportID = model.ArrivalAirportID,
                ArrivalAirport = (AirportViewModel)model.ArrivalAirport,
                DepartureTime = model.DepartureTime,
                ArrivalTime = model.ArrivalTime,
                BasePrice = model.BasePrice,
                Status = model.Status,
                FlightSeats = model.FlightSeats,
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
