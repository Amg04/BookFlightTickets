using BLLProject.Interfaces;
using BLLProject.Specifications;
using DAL.Data;
using DAL.models;
using Microsoft.EntityFrameworkCore;
namespace BLLProject.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : BaseClass
    {
        public readonly BookFilghtsDbContext _dbContext;
        public GenericRepository(BookFilghtsDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task AddAsync(T entity)
        {
           await  _dbContext.Set<T>().AddAsync(entity);
        }

        public void Delete(T entity)
        {
            _dbContext.Set<T>().Remove(entity);
        }

        public void Update(T entity)
        {
            _dbContext.Set<T>().Update(entity);

        }

        public async Task<T?> GetByIdAsync(int id)
        {
            return await _dbContext.Set<T>().FindAsync(id);
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbContext.Set<T>().AsNoTracking().ToListAsync();
        }

        public async Task<T?> GetEntityWithSpecAsync(ISpecification<T> spec)
        {
            return await SpecificationEvalutor<T>
                        .GetQuery(_dbContext.Set<T>(), spec)
                        .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<T>> GetAllWithSpecAsync(ISpecification<T> spec)
        {
            return await SpecificationEvalutor<T>
                        .GetQuery(_dbContext.Set<T>(), spec)
                        .AsNoTracking()
                        .ToListAsync();
        }

        public void RemoveRange(IEnumerable<T> entities)
        {
            _dbContext.Set<T>().RemoveRange(entities);
        }

        public async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _dbContext.Set<T>().AddRangeAsync(entities);
        }


        public async Task<int> CountAsync(ISpecification<T>? spec = null)
        {
            if (spec == null)
            {
                return await _dbContext.Set<T>().CountAsync();
            }
            else
            {
                return await SpecificationEvalutor<T>
                            .GetQuery(_dbContext.Set<T>(), spec)
                            .CountAsync();
            }
        }

    }
}
