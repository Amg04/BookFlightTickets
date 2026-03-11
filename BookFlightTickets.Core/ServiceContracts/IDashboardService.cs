using BookFlightTickets.Core.ViewModels;

namespace BookFlightTickets.Core.ServiceContracts
{
    public interface IDashboardService
    {
        Task<DashboardViewModel> GetDashboardDataAsync();
    }
}
