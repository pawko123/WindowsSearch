using BaseProvider.Host;
using VsCodeProvider.ResultFinders;
using VsCodeProvider.Settings;

await BaseProviderHost.RunAsync<VsCodeProviderSettings>(args, new VsCodeResultFinder());