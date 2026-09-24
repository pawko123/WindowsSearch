using WindowsSearch.Common.Models;
using BaseProvider.Host; using DemoProvider.ResultFinders;  await BaseProviderHost.RunAsync(args, new DemoResultFinder());
