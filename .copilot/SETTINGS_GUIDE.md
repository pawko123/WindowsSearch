# Przewodnik po Ustawieniach (Settings Guide)

Ustawienia dostawcy są **silnie typowane i walidowane atrybutami** (`System.ComponentModel.DataAnnotations`), a nie surowym słownikiem `Dictionary<string,string>`. Formularz w oknie ustawień Hub jest generowany w pełni przez refleksję - nie trzeba pisać XAML per dostawca.

## 1. Definiowanie Ustawień w Dostawcy

Każdy dostawca ma własny, mały projekt biblioteki `<Nazwa>.Settings` (osobny od samego dostawcy), zawierający klasę dziedziczącą po `ProviderSettingsBase` (`WindowsSearch.Common.Models`):

```csharp
using System.ComponentModel.DataAnnotations;
using WindowsSearch.Common.Models;
using YamlDotNet.Serialization;

namespace MyNewProvider.Settings;

public sealed class MyNewProviderSettings : ProviderSettingsBase
{
    [YamlMember(Alias = "action_path")]
    [Required(ErrorMessage = "Action path is required.")]
    [Display(Name = "Action path", Description = "Executable launched when a result is activated.")]
    public string ActionPath { get; set; } = "notepad.exe";
}
```

- `ProviderSettingsBase` dostarcza pola wspólne dla każdego dostawcy: `IsEnabled`, `Transport`, `EndpointNamedPipe`/`EndpointHttp`/`EndpointGrpc`, `Serialization`, `TimeoutSeconds`, `LogLevel` - wszystkie z odpowiednimi atrybutami walidacji (`[Range]`, własne `[HttpEndpoint]`/`[NamedPipeEndpoint]`) i `[Display]`. Flaga `IsEnabled` (w YAML `is_enabled`) pozwala na całkowite wyłączenie dostawcy - Hub nie załaduje go ani nie uruchomi jego procesu.
- Atrybuty walidacji (`[Required]`, `[Range]`, `[RegularExpression]`, itd.) na Twoich własnych właściwościach są sprawdzane automatycznie - zarówno przy starcie procesu dostawcy (fail-fast w `BaseProviderHost.RunAsync`), jak i w Hub przy zapisie ustawień w oknie Settings.
- Atrybut `[Display(Name=..., Description=...)]` steruje etykietą i podpowiedzią wyświetlaną w formularzu Hub.
- Jeśli potrzebujesz pola swobodnego (klucz/wartość), użyj właściwości typu `Dictionary<string, string>` - formularz automatycznie wyrenderuje ją jako edytowalną tabelę (tak jak `GenericProviderSettings.Extra`).

## 2. Jak Hub odkrywa ustawienia (bez zmian w Hub!)

Hub **nigdy nie referencuje** projektu dostawcy ani jego `.Settings`. Zamiast tego, w runtime (`Hub/Hub/Services/Settings/ProviderSettingsService.cs`):
1. `LoadAll()` skanuje katalog `Providers/<Nazwa>/` szukając plików `settings.yaml`.
2. Następnie szuka pliku `<Nazwa>.Settings.dll` w scentralizowanym katalogu `bin/` Hub'a (skopiowanego tam podczas publikacji - patrz `PROVIDER_GUIDE.md`). Hub wczytuje go przez `AssemblyLoadContext.Default.LoadFromAssemblyPath(...)` i znajduje refleksją typ dziedziczący po `ProviderSettingsBase`.
3. Jeśli takiego pliku nie znajdzie (np. dostawca jeszcze nie zaimplementował własnych typowanych ustawień, albo to dostawca firm trzecich), Hub używa `GenericProviderSettings` - ten sam ekran ustawień, ale z wolnym edytorem klucz/wartość.

Dzięki temu **dodanie nowego dostawcy nigdy nie wymaga zmiany kodu Hub** - wystarczy, że projekt dostawcy skopiuje własny `<Nazwa>.Settings.dll` do centralnego folderu `bin/` Hub'a.

## 3. Ustawienia "na żywo" bez restartu dostawcy

Hub wczytuje `settings.yaml` na nowo przy każdym wyszukiwaniu i wysyła bieżący snapshot ustawień (zserializowany do YAML) w polu `ProviderSearchRequest.SettingsYaml`. `BaseProviderHost` waliduje ten snapshot przy każdym żądaniu (`SettingsValidationHelper.Validate`) i używa go, jeśli jest prawidłowy - w przeciwnym razie (błąd walidacji/parsowania) wraca do ustawień wczytanych przy starcie procesu i loguje ostrzeżenie. Dzięki temu edycja ustawień w oknie Hub działa bez restartu procesu dostawcy.

## 4. Ustawienia Hub (nie dotyczące konkretnego dostawcy)

Ustawienia samego Hub (`Hub/Hub/Models/Settings/AppSettings.cs` - m.in. domyślny transport, limit wyszukiwania, poziom logowania) używają tego samego wzorca: atrybuty `[Display]`/`[Range]` + `SettingsValidationHelper.Validate` przy zapisie, i są renderowane tym samym generycznym formularzem co ustawienia dostawców (`Hub/SettingsWindow.xaml.cs`, metoda `BuildSettingsForm`).

## 5. Lokalizacja plików
- Model bazowy: `Shared/WindowsSearch.Common/Models/ProviderSettingsBase.cs`
- Fallback (wolny edytor): `Shared/WindowsSearch.Common/Models/GenericProviderSettings.cs`
- Wspólne YAML I/O: `Shared/WindowsSearch.Common/Serialization/ProviderSettingsYaml.cs`
- Wspólna walidacja: `Shared/WindowsSearch.Common/Validation/SettingsValidationHelper.cs`
- Odkrywanie/ładowanie po stronie Hub: `Hub/Hub/Services/Settings/ProviderSettingsService.cs`
- Generyczny formularz ustawień: `Hub/SettingsWindow.xaml.cs`
- Przykłady typowanych ustawień: `Providers/JetBrainsProvider/JetBrainsProvider.Settings/`, `Providers/DemoProvider/DemoProvider.Settings/`
