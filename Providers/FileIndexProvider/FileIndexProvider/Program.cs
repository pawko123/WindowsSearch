using BaseProvider.Host;
using FileIndexProvider.ResultFinders;
using FileIndexProvider.Settings;

if (args.Contains("--rebuild", StringComparer.OrdinalIgnoreCase))
{
    using var finder = new FileIndexResultFinder();
    var settings = new FileIndexProviderSettings();
    Console.WriteLine("Rebuilding file index...");
    await finder.Crawler.RebuildAsync(settings, CancellationToken.None, (count, path) =>
    {
        Console.WriteLine($"Indexed {count} files. Current: {path}");
    });
    Console.WriteLine("Rebuild complete.");
    return;
}

await BaseProviderHost.RunAsync<FileIndexProviderSettings>(args, new FileIndexResultFinder());
