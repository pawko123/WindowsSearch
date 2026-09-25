using BaseProvider.Host;
using DemoProvider.ResultFinders;
using DemoProvider.Settings;

await BaseProviderHost.RunAsync<DemoProviderSettings>(args, new DemoResultFinder());
