using BaseProvider.Host;
using FirefoxProvider.ResultFinders;
using FirefoxProvider.Settings;

await BaseProviderHost.RunAsync<FirefoxProviderSettings>(args, new FirefoxResultFinder());