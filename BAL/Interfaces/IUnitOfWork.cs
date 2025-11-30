using BLL.Interfaces;
using DAL.models;
using Microsoft.EntityFrameworkCore.Storage;
namespace BLLProject.Interfaces
{
    public interface IUnitOfWork : IAsyncDisposable
    {
        IGenericRepository<T> Repository<T>() where T : BaseClass;
        Task<IDbContextTransaction> BeginTransactionAsync();
        IFlight FlightRepository { get; }
        Task<int> CompleteAsync();
    }
}
