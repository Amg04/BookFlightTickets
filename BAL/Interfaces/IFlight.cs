using BLLProject.Interfaces;
using BLLProject.Specifications;
using DAL.models;

namespace BLL.Interfaces
{
    public interface IFlight : IGenericRepository<Flight>
    {
        Task<int> CountAsync(ISpecification<Flight> spec);
        Task<Dictionary<string, int>> GetFlightCountByAirlineAsync();
    }
}
