using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VideoCortex.Core.Db;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> can construct the context without the web host.
/// The connection string here is only used for migration scaffolding, never at runtime.
/// </summary>
public class VideoCortexDbContextFactory : IDesignTimeDbContextFactory<VideoCortexDbContext>
{
    public VideoCortexDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<VideoCortexDbContext>()
            .UseSqlite("Data Source=videocortex-design.db")
            .Options;
        return new VideoCortexDbContext(options);
    }
}
