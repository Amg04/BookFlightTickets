using BLL.Interfaces;
using BLLProject.Interfaces;
using DAL.Data;
using DAL.models;
using Microsoft.EntityFrameworkCore.Storage;
namespace BLLProject.Repositories
{ 
    public class UnitOfWork : IUnitOfWork
    {
        private readonly BookFilghtsDbContext _dbContext;
        public IFlight FlightRepository { get; private set; }
        private readonly Dictionary<string, object> _repositories = new();

        public UnitOfWork(BookFilghtsDbContext dbContext)
        {
            _dbContext = dbContext;
            FlightRepository = new FlightRepository(dbContext);
        }


        public IGenericRepository<T> Repository<T>() where T : BaseClass
        {
            var typeName = typeof(T).Name;
            if (!_repositories.ContainsKey(typeName))
            {
                _repositories[typeName] = new GenericRepository<T>(_dbContext);
            }
            return (IGenericRepository<T>)_repositories[typeName];
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            return await _dbContext.Database.BeginTransactionAsync();
        }


        public async ValueTask DisposeAsync()
        {
            await _dbContext.DisposeAsync();
        }

        public async Task<int> CompleteAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }
    }
}
