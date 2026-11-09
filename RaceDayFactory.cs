using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

namespace RaceDay.Api.Tests;

/// <summary>Hosts the real API in memory, swapping SQL Server for an isolated InMemory database.</summary>
public class RaceDayFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>));
            if (descriptor != null) services.Remove(descriptor);
            services.AddDbContext<RaceDayDbContext>(o => o.UseInMemoryDatabase(_dbName));

            // Never talk to real Azure from tests.
            services.RemoveAll<IBlobStorageService>();
            services.AddScoped<IBlobStorageService, FakeBlobStorageService>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<RaceDayDbContext>().Database.EnsureCreated(); // also seeds roles
        return host;
    }
}

/// <summary>Stands in for Azure Blob Storage during tests.</summary>
public class FakeBlobStorageService : IBlobStorageService
{
    public Task<string> UploadAsync(Microsoft.AspNetCore.Http.IFormFile file, string folder, CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://fake.blob.local/raceday-images/{folder}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}");
}
