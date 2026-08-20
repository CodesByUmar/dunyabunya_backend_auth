using System.Security.Claims;
using AuthApi.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Tests.Helpers;

// SQLite'ning xotiradagi (":memory:") bazasi ishlatiladi — EF Core'ning
// InMemory provayderi ExecuteUpdateAsync/ExecuteDeleteAsync'ni (bizning
// atomik ball yechish kodimiz shularga tayanadi) qo'llab-quvvatlamaydi,
// SQLite esa haqiqiy SQL'ga tarjima qilgani uchun qo'llab-quvvatlaydi.
internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    public AppDbContext Context { get; }

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}

internal static class TestDbContextFactory
{
    public static TestDatabase Create() => new();

    public static ControllerContext ControllerContextFor(int userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            authenticationType: "TestAuth");

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }
}
