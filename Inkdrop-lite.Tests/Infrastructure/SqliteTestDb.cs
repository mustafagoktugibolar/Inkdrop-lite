using Inkdrop_lite.Authorization;
using Inkdrop_lite.Data;
using InkdropLite.Api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inkdrop_lite.Tests.Infrastructure;

internal sealed class SqliteTestDb : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private SqliteTestDb(SqliteConnection connection, AppDbContext context)
    {
        _connection = connection;
        Context = context;
    }

    public AppDbContext Context { get; }

    public async Task<Notebook> AddNotebookAsync(string name = "Inbox")
    {
        var notebook = new Notebook { Name = name };
        Context.Notebooks.Add(notebook);
        await Context.SaveChangesAsync();
        return notebook;
    }

    public static async Task<SqliteTestDb> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options, new FixedCurrentUser("test-user"));
        await context.Database.EnsureCreatedAsync();

        return new SqliteTestDb(connection, context);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private sealed class FixedCurrentUser(string userId) : ICurrentUser
    {
        public string? UserId { get; } = userId;
    }
}
