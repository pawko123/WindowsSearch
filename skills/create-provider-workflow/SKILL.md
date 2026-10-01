---
name: create-provider-workflow
description: Step-by-step instructions and coding guidelines for creating a new Hub Provider
---

# Create Provider Workflow

When asked to create a new provider for the Hub, follow this strict sequence to ensure it correctly integrates with the architecture:

## 1. Plan & Scaffold
1. **Discuss the Provider**: Confirm the provider's goal, data source, and what settings it will need.
2. **Create Settings Project**: 
   - Create `<Name>Provider.Settings` class library.
   - Define `<Name>ProviderSettings` inheriting from `ProviderSettingsBase`.
   - Use `System.ComponentModel.DataAnnotations` (`[Display]`, `[Range]`, etc.) for all properties.
   - **Crucial `.csproj` Structure**: Keep it minimal. Add `<UseWPF>true</UseWPF>` and `<ProjectReference Include="..\..\..\Shared\WindowsSearch.Common\WindowsSearch.Common.csproj" />`.
3. **Create Executable Project**:
   - Create `<Name>Provider` console app.
   - Reference `BaseProvider` and the new `.Settings` project.
   - **Crucial `.csproj` Structure**: You MUST configure it for `Publish` dynamic discovery, mimicking existing providers. Include `<PublishDir>`, `<RuntimeIdentifier>win-x64</RuntimeIdentifier>`, `<PublishSingleFile>true</PublishSingleFile>`.
   - Use MSBuild targets (`AfterTargets="Publish"`) to copy `.Settings.dll` into the Hub's `bin/` directory and manage artifact cleanup, rather than standard `PostBuildEvent` actions.
4. **Create Tests Project**:
   - Create `<Name>Provider.Tests` xUnit project.
   - **Crucial `.csproj` Structure**: Do NOT add `xunit`, `Microsoft.NET.Test.Sdk`, or `coverlet` packages. They are automatically injected by the root `Directory.Build.props`. Keep the `<ItemGroup>` minimal (just reference `.Settings` and `WindowsSearch.Common`).
   - Add tests covering `SettingsValidationHelper` rules for the new settings model (using `WindowsSearch.Common.Validation`).
5. **Update Solution (`.slnx`)**: When adding projects, you MUST preserve the solution folder structure using `--solution-folder`:
   - `dotnet sln Providers/Providers.slnx add Providers/<Name>Provider/<Name>Provider.Settings/<Name>Provider.Settings.csproj --solution-folder "/<Name>Provider/"`
   - `dotnet sln Providers/Providers.slnx add Providers/<Name>Provider/<Name>Provider/<Name>Provider.csproj --solution-folder "/<Name>Provider/"`
   - `dotnet sln Providers/Providers.slnx add Providers/<Name>Provider/<Name>Provider.Tests/<Name>Provider.Tests.csproj --solution-folder "/<Name>Provider/<Name>Provider.Tests/"`

## 2. Implementation details
1. **Folder Layout**: All provider assets MUST sit in the root provider folder (e.g. `Providers/<Name>Provider/`), alongside the `.Settings` and core projects:
   - `Providers/<Name>Provider/settings.yaml`
   - `Providers/<Name>Provider/Icons/icon.png`
2. **Result Finder**: Implement `IResultFinder<TSettings>` in `<Name>ResultFinder.cs`.
3. **Host Setup**: In `Program.cs`, initiate the provider with exactly one line: `await BaseProviderHost.RunAsync<MySettings>(args, new MyResultFinder());`
4. **Assets**: Place a 32x32 `icon.png` in the root `Icons/` directory. Ensure your `.csproj` copies it to output via `<None Include="..\Icons\**\*.*">` and maps `<Link>Icons\%(RecursiveDir)%(Filename)%(Extension)</Link>`.
5. **Configuration**: Create `settings.yaml` in the root directory using `snake_case` keys (e.g., `is_enabled: true`, `log_level: info`, `weight: 0`) and the custom endpoints. Map it in `.csproj` using `<None Include="..\settings.yaml">`.

## 3. Coding Guidelines
- **Separation of Concerns**: Keep business logic out of `Program.cs`. Rely on the `IResultFinder`.
- **Class Structure**: 
  - **Single Class Per File**: NEVER put multiple classes in the same file. Always separate them.
  - **Models Directory**: Place all data structure classes/POCOs into a `Models/` directory.
  - Place all fields, properties, and variables at the top of the class.
  - Place all methods at the bottom of the class.
- **Method Ordering**: Group public methods at the top, followed by private/helper methods.
- **Robustness**: Log external calls and explicitly catch network/IO exceptions to prevent crashing the host process.
- **Documentation**: Verify details against `.copilot/PROVIDER_GUIDE.md` when unsure about publishing steps or schema details.