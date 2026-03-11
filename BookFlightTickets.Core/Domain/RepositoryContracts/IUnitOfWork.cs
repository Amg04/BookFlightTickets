using BookFlightTickets.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage;

namespace BookFlightTickets.Core.Domain.RepositoryContracts
{
    public interface IUnitOfWork : IAsyncDisposable
    {
        IGenericRepository<T> Repository<T>() where T : BaseClass;
        Task<IDbContextTransaction> BeginTransactionAsync();
        IFlightRepository FlightRepository { get; }
        Task<int> CompleteAsync();
    }
}
