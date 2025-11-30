using BLLProject.Specifications;
using DAL.models;
namespace BLLProject.Interfaces
{
    public interface IGenericRepository<T> where T : BaseClass
    {

        Task AddAsync(T entity);
        void Delete(T entity);
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        void Update(T entity);
        Task<T?> GetEntityWithSpecAsync(ISpecification<T> spec);
        Task<IEnumerable<T>> GetAllWithSpecAsync(ISpecification<T> spec);
        void RemoveRange(IEnumerable<T> entities);
        Task AddRangeAsync(IEnumerable<T> entities);
        Task<int> CountAsync(ISpecification<T>? spec = null);
    }
}
