using BaseProvider.Host;
using JetBrainsProvider.ResultFinders;
using JetBrainsProvider.Settings;

await BaseProviderHost.RunAsync<JetBrainsProviderSettings>(args, new JetBrainsResultFinder());
