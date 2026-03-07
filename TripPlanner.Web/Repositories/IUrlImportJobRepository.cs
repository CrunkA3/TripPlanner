using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public interface IUrlImportJobRepository
{
    Task<UrlImportJob> AddAsync(UrlImportJob job);
    Task<List<UrlImportJob>> GetByWishlistIdAsync(string wishlistId);
    Task<List<UrlImportJob>> GetPendingJobsAsync(int maxCount = 5);
    Task<UrlImportJob?> GetByIdAsync(string id);
    Task<UrlImportJob> UpdateAsync(UrlImportJob job);
    Task DeleteAsync(string id);
}
