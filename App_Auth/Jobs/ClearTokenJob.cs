using CradleSoft.DMS.Data;

namespace App_Auth.Jobs;

public class ClearTokenJob
{
    private readonly AppDbContext _dbContext;

    public ClearTokenJob(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Execute()
    {
        var expiredTokens = _dbContext.RefreshTokens.Where(t => t.Expires < DateTime.UtcNow);
        _dbContext.RefreshTokens.RemoveRange(expiredTokens);
        _dbContext.SaveChanges();
    }
}
