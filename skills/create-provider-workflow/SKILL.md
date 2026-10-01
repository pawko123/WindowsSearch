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
3. **Create Executable Project**:
   - Create `<Name>Provider` console app.
   - Reference `BaseProvider` and the new `.Settings` project.
   - Ensure the `.csproj` includes a post-build target to copy the `.Settings.dll` to the Hub's `bin` folder for dynamic discovery.
4. **Create Tests Project**:
   - Create `<Name>Provider.Tests` xUnit project.
   - Add tests covering `SettingsValidationHelper` rules for the new settings model.
5. **Update Solution**: Add all three projects to `Providers/Providers.slnx`.

## 2. Implementation details
1. **Result Finder**: Implement `IResultFinder<TSettings>` in `<Name>ResultFinder.cs`.
2. **Host Setup**: In `Program.cs`, initiate the provider with exactly one line: `await BaseProviderHost.RunAsync<MySettings>(args, new MyResultFinder());`
3. **Assets**: Place a 32x32 `icon.png` in the `Icons/` directory and set it to `CopyToOutputDirectory="PreserveNewest"`.
4. **Configuration**: Create `settings.yaml` using `snake_case` keys (e.g., `is_enabled: true`, `log_level: info`, `weight: 0`) and the custom endpoints.

## 3. Coding Guidelines
- **Separation of Concerns**: Keep business logic out of `Program.cs`. Rely on the `IResultFinder`.
- **Class Structure**: 
  - Place all fields, properties, and variables at the top of the class.
  - Place all methods at the bottom of the class.
- **Method Ordering**: Group public methods at the top, followed by private/helper methods.
- **Robustness**: Log external calls and explicitly catch network/IO exceptions to prevent crashing the host process.
- **Documentation**: Verify details against `.copilot/PROVIDER_GUIDE.md` when unsure about publishing steps or schema details.