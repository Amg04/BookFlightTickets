using BookFlightTickets.Core.Domain.Enums;
using System.ComponentModel.DataAnnotations;
namespace BookFlightTickets.Core.ViewModels
{
    public class FlightFilterViewModel
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        [StringLength(100, ErrorMessage = "Search text cannot exceed 100 characters")]
        public string? SearchString { get; set; } = "";
        public string SearchBy { get; set; } = "";
        public string SortBy { get; set; } = nameof(FlightViewModel.DepartureTime);
        public SortOrderOptions SortOrder { get; set; } = SortOrderOptions.ASC;
        [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0")]
        public int Page { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100")]
        public int PageSize { get; set; } = 10;
    }
}
