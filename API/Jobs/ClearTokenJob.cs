using CradleSoft.DMS.Data;

public class ClearTokensJob
{
    private readonly AppDbContext _dbContext;

    public ClearTokensJob(AppDbContext dbContext)
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