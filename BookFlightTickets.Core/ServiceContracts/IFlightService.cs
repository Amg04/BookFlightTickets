using BookFlightTickets.Core.Domain.Enums;
using BookFlightTickets.Core.Domain.ResultPattern;
using BookFlightTickets.Core.ViewModels;

namespace BookFlightTickets.Core.ServiceContracts
{
    public interface IFlightService
    {
        Task<Result<List<FlightViewModel>>> GetFilteredFlights(
            string searchBy, 
            string? searchString,
            DateTime? fromDate,
            DateTime? toDate);
        Task<Result<List<FlightViewModel>>> GetSortedFlightsAsync(
            List<FlightViewModel> allFlights, 
            string sortBy, 
            SortOrderOptions sortOrder);
    }
}
