namespace mysystem_bff.Services.Services.ApiUsage;

public class EndpointUsageWriterService : BackgroundService
{
    private readonly EndpointUsageTracker _tracker;
    private readonly string _logDirectory;

    public EndpointUsageWriterService(
        EndpointUsageTracker tracker)
    {
        _tracker = tracker;

        _logDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "logs",
            "usage");
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await _tracker.WriteCsvAsync(
                _logDirectory,
                stoppingToken);

            DeleteExpiredFiles();
        }
    }

    public override async Task StopAsync(
        CancellationToken cancellationToken)
    {
        await _tracker.WriteCsvAsync(
            _logDirectory,
            cancellationToken);

        await base.StopAsync(cancellationToken);
    }

    private void DeleteExpiredFiles()
    {
        if (!Directory.Exists(_logDirectory))
            return;

        var oldestAllowedDate =
            DateTime.Now.Date.AddDays(-365);

        foreach (var file in Directory.GetFiles(
            _logDirectory,
            "endpoint-usage-*.csv"))
        {
            var createdDate = File.GetCreationTime(file).Date;

            if (createdDate < oldestAllowedDate)
            {
                File.Delete(file);
            }
        }
    }
}