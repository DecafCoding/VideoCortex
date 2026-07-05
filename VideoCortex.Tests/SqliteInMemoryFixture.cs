using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VideoCortex.Core.Db;

namespace VideoCortex.Tests;

/// <summary>
/// Backs each test with an isolated in-memory SQLite database. The connection is kept open
/// for the fixture's lifetime — a <c>:memory:</c> database is discarded the moment its last
/// connection closes. Create one per test (via <c>using</c>) for isolation.
/// </summary>
public sealed class SqliteInMemoryFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<VideoCortexDbContext> _options;

    public SqliteInMemoryFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<VideoCortexDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    /// <summary>A fresh context over the shared in-memory database.</summary>
    public VideoCortexDbContext CreateContext() => new(_options);

    /// <summary>The shared options, for registering the context in a test service provider.</summary>
    public DbContextOptions<VideoCortexDbContext> Options => _options;

    public void Dispose() => _connection.Dispose();
}
