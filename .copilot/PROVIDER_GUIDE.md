# Tworzenie Dostawców (Providers) - Przewodnik

## 1. Architektura Dostawcy (Provider)

Dostawcy (Providers) korzystają ze wspólnego projektu bazowego **`BaseProvider`**. Projekt ten udostępnia:
- Generyczny interfejs **`IResultFinder<TSettings>`** (gdzie `TSettings` dziedziczy po `ProviderSettingsBase`), który musisz zaimplementować.
- Klasę **`BaseProviderHost`** z generyczną metodą `RunAsync<TSettings>(args, resultFinder)`, która zarządza cyklem życia procesu, wczytywaniem i walidacją ustawień oraz komunikacją (NamedPipe z użyciem `System.Text.Json`, gRPC, HTTP).
- Modele danych (`ProviderSearchRequest`, `ProviderSearchResponse`, `ProviderResultCategory`, `ProviderResultItem`) oraz bazowy model ustawień `ProviderSettingsBase` (w `WindowsSearch.Common`).

Zobacz też `SETTINGS_GUIDE.md` po szczegóły dotyczące typowanych, walidowanych ustawień per dostawca.

## 2. Struktura Katalogów

Każdy dostawca składa się z **dwóch** projektów: samego dostawcy (exe) oraz osobnej, lekkiej biblioteki `<Nazwa>.Settings` z typowanym modelem ustawień. Hub odkrywa ustawienia dostawcy wyłącznie poprzez refleksję nad tą biblioteką w runtime - **Hub nigdy nie referencuje projektu dostawcy**, więc dodanie nowego dostawcy nie wymaga żadnych zmian w kodzie Hub.

```text
Providers/
├── BaseProvider/                    <-- Współdzielona logika i modele
├── DemoProvider/
│   ├── DemoProvider.Settings/       <-- Typowany model ustawień (biblioteka)
│   └── DemoProvider/                <-- Sam dostawca (exe, referencuje BaseProvider + *.Settings)
└── MyNewProvider/
    ├── MyNewProvider.Settings/
    │   ├── MyNewProvider.Settings.csproj
    │   └── MyNewProviderSettings.cs
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
using YamlDotNet.Serialization;

namespace MyNewProvider.Settings;

public sealed class MyNewProviderSettings : ProviderSettingsBase
{
    [YamlMember(Alias = "cache_ttl_minutes")]
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

Jeśli w konfiguracji Release używasz `PublishSingleFile` (patrz `JetBrainsProvider.csproj`/`DemoProvider.csproj`), dodaj target kopiujący `<Nazwa>.Settings.dll` z powrotem do `$(PublishDir)` **po** tym, jak `TrimPublishedProviderArtifacts` usunie wszystko poza plikiem `.exe` i `settings.yaml` - inaczej Hub nie znajdzie typowanych ustawień. Skopiuj istniejący wzorzec `CopyProviderSettingsAssembly` z `JetBrainsProvider.csproj`/`DemoProvider.csproj`.

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
Plik `settings.yaml` obok exe zawiera płaskie, typowane pola (bez zagnieżdżonego słownika `settings:`):
```yaml
transport: NamedPipe
endpoint_named_pipe: \\.\pipe\my_new_provider
endpoint_http: http://localhost:5010
endpoint_grpc: http://localhost:5011
serialization: json
timeout_seconds: 5
log_level: Info
cache_ttl_minutes: 5
```
Jeśli plik jest nieprawidłowy (np. nie przechodzi walidacji atrybutów), proces dostawcy zgłasza błąd i zamyka się przy starcie (fail-fast) - nie działa dalej z domyślnymi wartościami po tichu.

### Krok 6: Publikacja i CI/CD
Bez zmian względem wcześniejszej wersji tego przewodnika: skrypt w `build-release.yml` wykrywa foldery wewnątrz `Providers/` (z pominięciem `BaseProvider` i `DemoProvider`) i publikuje `Providers/MyNewProvider/MyNewProvider/MyNewProvider.csproj` używając .NET 10.0. Projekt `.Settings` jest kompilowany automatycznie jako zależność projektu dostawcy - nie trzeba dodawać go osobno do CI.
