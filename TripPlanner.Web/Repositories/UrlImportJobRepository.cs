using Microsoft.EntityFrameworkCore;
using TripPlanner.Web.Data;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public class UrlImportJobRepository(ApplicationDbContext context) : IUrlImportJobRepository
{
    public async Task<UrlImportJob> AddAsync(UrlImportJob job)
    {
        context.UrlImportJobs.Add(job);
        await context.SaveChangesAsync();
        return job;
    }

    public async Task<List<UrlImportJob>> GetByWishlistIdAsync(string wishlistId)
    {
        return await context.UrlImportJobs
            .Where(j => j.WishlistId == wishlistId)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<UrlImportJob>> GetPendingJobsAsync(int maxCount = 5)
    {
        return await context.UrlImportJobs
            .Where(j => j.Status == UrlImportJobStatus.Pending)
            .OrderBy(j => j.CreatedAt)
            .Take(maxCount)
            .ToListAsync();
    }

    public async Task<UrlImportJob?> GetByIdAsync(string id)
    {
        return await context.UrlImportJobs.FindAsync(id);
    }

    public async Task<UrlImportJob> UpdateAsync(UrlImportJob job)
    {
        context.UrlImportJobs.Update(job);
        await context.SaveChangesAsync();
        return job;
    }

    public async Task DeleteAsync(string id)
    {
        await context.UrlImportJobs
            .Where(j => j.Id == id)
            .ExecuteDeleteAsync();
    }
}
