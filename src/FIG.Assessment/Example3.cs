using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FIG.Assessment;

/// <summary>
///     Entry point for the daily report service.
///     Uses dependency injection and a background worker.
/// </summary>
public class Example3
{
    public static void Main(string[] args)
    {
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddDbContext<UserDbContextE3>(options => 
                    options.UseSqlServer("dummy-connection-string"));
                services.AddSingleton<ReportEngine>();
                services.AddHostedService<DailyReportService>();
            })
            .Build()
            .Run();
    }
}

/// <summary>
///     Background service that generates and sends daily user reports.
///     Uses a scheduled execution strategy.
/// </summary>
public class DailyReportService(ReportEngine reportEngine, ILogger<DailyReportService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DailyReportService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var scheduledTime = now.Date.AddHours(3);

            if (now > scheduledTime) 
                scheduledTime = scheduledTime.AddDays(1);

            logger.LogInformation("Next report scheduled for {ScheduledTime} UTC", scheduledTime);
            
            var timeRemaining = scheduledTime - DateTime.UtcNow;
            if (timeRemaining > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(timeRemaining, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    logger.LogInformation("DailyReportService is shutting down.");
                    return;
                }
            }


            var startingFrom = DateTime.UtcNow.AddDays(-1);
            logger.LogInformation("Generating user report for users created/deactivated since {StartTime} UTC", startingFrom);
            
            var results = await Task.WhenAll(
                reportEngine.GetNewUsersAsync(startingFrom),
                reportEngine.GetDeactivatedUsersAsync(startingFrom));

            var newUsers = results[0];
            var deactivatedUsers = results[1];

            logger.LogInformation("Report generated. New users: {NewUserCount}, Deactivated users: {DeactivatedUserCount}",
                newUsers.Count(), deactivatedUsers.Count());

            await SendUserReportAsync(newUsers, deactivatedUsers);
        }
    }

    private Task SendUserReportAsync(IEnumerable<UserE3> newUsers, IEnumerable<UserE3> deactivatedUsers)
    {
        logger.LogInformation("Sending user report: {NewUserCount} new users, {DeactivatedUserCount} deactivated users.",
            newUsers.Count(), deactivatedUsers.Count());

        return Task.CompletedTask;
    }
}

/// <summary>
///     Report engine responsible for fetching user data from the database.
///     Optimized to avoid loading unnecessary records into memory.
/// </summary>
public class ReportEngine(UserDbContextE3 db, ILogger<ReportEngine> logger)
{
    public async Task<List<UserE3>> GetNewUsersAsync(DateTime startingFrom)
    {
        logger.LogInformation("Fetching new users created since {StartTime} UTC", startingFrom);
        return await db.Users
            .Where(u => u.CreatedAt > startingFrom)
            .ToListAsync();
    }

    public async Task<List<UserE3>> GetDeactivatedUsersAsync(DateTime startingFrom)
    {
        logger.LogInformation("Fetching deactivated users since {StartTime} UTC", startingFrom);
        return await db.Users
            .Where(u => u.DeactivatedAt > startingFrom)
            .ToListAsync();
    }
}

#region Database Entities

// Ignored per instructions. Simply renamed to prevent conflicts with other examples.

public class UserE3
{
    [Key] public required int UserId { get; set; }
    
    [MaxLength(50)]
    public string UserName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? DeactivatedAt { get; set; }
}

public class UserDbContextE3(DbContextOptions<UserDbContextE3> options) : DbContext(options)
{
    public DbSet<UserE3> Users { get; set; }
}

#endregion