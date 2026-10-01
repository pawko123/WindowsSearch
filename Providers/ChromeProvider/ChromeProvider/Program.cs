using BaseProvider.Host;
using ChromeProvider.ResultFinders;
using ChromeProvider.Settings;

await BaseProviderHost.RunAsync<ChromeProviderSettings>(args, new ChromeResultFinder());