# Tworzenie Dostawców (Providers) - Przewodnik

## 1. Architektura Dostawcy (Provider)

Dostawcy (Providers) korzystają ze wspólnego projektu bazowego **`BaseProvider`**. Projekt ten udostępnia:
- Generyczny interfejs **`IResultFinder<TSettings>`** (gdzie `TSettings` dziedziczy po `ProviderSettingsBase`), który musisz zaimplementować.
- Klasę **`BaseProviderHost`** z generyczną metodą `RunAsync<TSettings>(args, resultFinder)`, która zarządza cyklem życia procesu, wczytywaniem i walidacją ustawień oraz komunikacją (NamedPipe z użyciem `System.Text.Json`, gRPC, HTTP).
- Modele danych (`ProviderSearchRequest`, `ProviderSearchResponse`, `ProviderResultCategory`, `ProviderResultItem`) oraz bazowy model ustawień `ProviderSettingsBase` (w `WindowsSearch.Common`).

Zobacz też `SETTINGS_GUIDE.md` po szczegóły dotyczące typowanych, walidowanych ustawień per dostawca.

## 2. Struktura Katalogów

Każdy dostawca składa się z **trzech** projektów: samego dostawcy (exe), lekkiej biblioteki `<Nazwa>.Settings` z typowanym modelem ustawień, oraz projektu testowego `<Nazwa>.Tests`. Hub odkrywa ustawienia dostawcy wyłącznie poprzez refleksję nad tą biblioteką w runtime.

```text
Providers/
├── BaseProvider/                    <-- Współdzielona logika i modele
├── DemoProvider/
│   ├── DemoProvider.Settings/       <-- Typowany model ustawień (biblioteka)
│   ├── DemoProvider.Tests/          <-- Testy ustawień i walidacji
│   └── DemoProvider/                <-- Sam dostawca (exe, referencuje BaseProvider + *.Settings)
└── MyNewProvider/
    ├── MyNewProvider.Settings/
    │   ├── MyNewProvider.Settings.csproj
    │   └── MyNewProviderSettings.cs
    ├── MyNewProvider.Tests/
    │   ├── MyNewProvider.Tests.csproj
    │   └── Settings/MyNewProviderSettingsTests.cs
    └── MyNewProvider/
        ├── MyNewProvider.csproj
        ├── ResultFinders/MyResultFinder.cs
        └── Program.cs
```

## 3. Krok po Kroku: Tworzenie nowego dostawcy

### Krok 1: Projekt ustawień (`MyNewProvider.Settings`)
Zdefiniuj klasę ustawień dziedziczącą po `ProviderSettingsBase`, z atrybutami walidacji (`System.ComponentModel.DataAnnotations`) i etykietami (`[Display]`):

```csharp
using System.ComponentModel.DataAnnotations;
using WindowsSearch.Common.Models;

namespace MyNewProvider.Settings;

public sealed class MyNewProviderSettings : ProviderSettingsBase
{
    public MyNewProviderSettings()
    {
        Endpoints.NamedPipe = @"\\.\pipe\my_new_provider";
        Endpoints.Http = "http://localhost:5040";
        Endpoints.Grpc = "http://localhost:5041";
    }

    [Range(1, 1440, ErrorMessage = "Cache TTL must be between 1 and 1440 minutes.")]
    [Display(Name = "Cache TTL (minutes)", Description = "Jak długo trzymać wyniki w cache.")]
    public int CacheTtlMinutes { get; set; } = 5;
}
```

Projekt `MyNewProvider.Settings.csproj` referencuje **tylko** `WindowsSearch.Common` (żadnych zależności ASP.NET Core/gRPC), żeby był lekki i łatwy do wczytania przez Hub w runtime (`AssemblyLoadContext.Default.LoadFromAssemblyPath`).

### Krok 2: Projekt dostawcy (exe)
`MyNewProvider.csproj` referencuje `BaseProvider` **oraz** `MyNewProvider.Settings`:
```xml
<ItemGroup>
  <ProjectReference Include="..\..\BaseProvider\BaseProvider.csproj" />
  <ProjectReference Include="..\MyNewProvider.Settings\MyNewProvider.Settings.csproj" />
</ItemGroup>
```
Jeśli w konfiguracji Release używasz `PublishSingleFile` (patrz `JetBrainsProvider.csproj`/`DemoProvider.csproj`), dodaj target kopiujący `<Nazwa>.Settings.dll` do centralnego folderu `bin` w Hubie (`$(PublishDir)..\..\bin\`) **po** tym, jak `TrimPublishedProviderArtifacts` usunie wszystko poza plikiem `.exe` i `settings.yaml` - inaczej Hub nie znajdzie typowanych ustawień. Skopiuj istniejący wzorzec `CopyProviderSettingsAssembly` z innych dostawców. **Ważne:** Zdefiniuj `PublishDir` używając normalizacji absolutnej `$([System.IO.Path]::GetFullPath('...'))\`, aby reguła `<Exclude>` w targecie czyszczącym (zapobiegająca usunięciu `settings.yaml`) zadziałała prawidłowo!

### Krok 2.5: Ikona dostawcy (UI)
Każdy dostawca powinien posiadać swój własny plik ikony (np. logo aplikacji docelowej) umieszczony w podkatalogu `Icons/` obok projektu pod ścisłą nazwą `icon.png`. Plik ten musi być oznaczony w `.csproj` za pomocą `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`, aby został przeniesiony do folderu publikacji. Jeśli plik nie zostanie znaleziony, Hub użyje domyślnej, generycznej ikony wbudowanej w aplikację bazową (`provider-fallback-icon.png`). Ikona ta jest używana przez Hub m.in. w komponencie wizualizującym oczekujących na odpowiedź dostawców (`PendingProvidersControl`).

### Krok 3: Implementacja `IResultFinder<TSettings>`
Stwórz klasę `MyResultFinder.cs`:
```csharp
using BaseProvider.Abstractions;
using MyNewProvider.Settings;
using WindowsSearch.Common.Models;

namespace MyNewProvider.ResultFinders;

public sealed class MyResultFinder : IResultFinder<MyNewProviderSettings>
{
    public Task<ProviderSearchResponse> FindAsync(ProviderSearchRequest request, MyNewProviderSettings settings, CancellationToken cancellationToken)
    {
        var response = new ProviderSearchResponse();
        var category = new ProviderResultCategory { Name = "Wyniki z MyNewProvider" };

        category.Items.Add(new ProviderResultItem
        {
            Title = $"Wynik dla: {request.Query}",
            Subtitle = "Przykładowy podtytuł"
        });

        response.Categories.Add(category);
        return Task.FromResult(response);
    }
}
```
Ustawienia (`settings`) są przekazywane bezpośrednio, jako typowany i już zwalidowany obiekt - nie czyta się ich z surowego słownika (ten mechanizm nie istnieje już w `ProviderSearchRequest`).

### Krok 4: Konfiguracja Host'a w `Program.cs`
Dzięki `BaseProviderHost` plik startowy ogranicza się do jednej linijki:
```csharp
using BaseProvider.Host;
using MyNewProvider.ResultFinders;
using MyNewProvider.Settings;

await BaseProviderHost.RunAsync<MyNewProviderSettings>(args, new MyResultFinder());
```

### Krok 5: `settings.yaml`
Plik `settings.yaml` obok exe zawiera typowane pola formatowane jako `snake_case`:
```yaml
is_enabled: true
log_level: info
weight: 0
endpoints:
  named_pipe: \\.\pipe\my_new_provider
  http: http://localhost:5040
  grpc: http://localhost:5041
cache_ttl_minutes: 5
### Krok 6: Projekt Testów (`MyNewProvider.Tests`)
Utwórz projekt testowy `MyNewProvider.Tests` oparty na xUnit. Skonfiguruj referencję do biblioteki `.Settings` dostawcy oraz projektu `WindowsSearch.Common`:
```xml
<ItemGroup>
  <ProjectReference Include="..\MyNewProvider.Settings\MyNewProvider.Settings.csproj" />
  <ProjectReference Include="..\..\..\Shared\WindowsSearch.Common\WindowsSearch.Common.csproj" />
</ItemGroup>
```
Utwórz folder `Settings/` i dodaj klasę testującą walidację modelu (wykorzystując `SettingsValidationHelper`). Pamiętaj o dodaniu testów do pliku rozwiązania `Providers.slnx`. Pakiety testowe zostaną automatycznie dołączone dzięki globalnemu plikowi `Directory.Build.props`.

### Krok 7: Publikacja i CI/CD
Skrypt w `build-release.yml` analizuje strukturę. Akcja uruchomi testy automatycznie, a po ich przejściu, opublikuje projekt dostawcy ignorując wtyczki pomocnicze (np. `BaseProvider`, `WebBaseProvider`, `DemoProvider`) oraz wszystkie foldery testowe. Projekt `.Settings` wbudowany jest zawsze w katalog `bin` dostawcy.
