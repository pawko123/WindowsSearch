# Przewodnik po Ustawieniach (Settings Guide)

Ustawienia dostawcy są **silnie typowane i walidowane atrybutami** (`System.ComponentModel.DataAnnotations`), a nie surowym słownikiem `Dictionary<string,string>`. Formularz w oknie ustawień Hub jest generowany w pełni przez refleksję - nie trzeba pisać XAML per dostawca.

## 1. Definiowanie Ustawień w Dostawcy

Każdy dostawca ma własny, mały projekt biblioteki `<Nazwa>.Settings` (osobny od samego dostawcy), zawierający klasę dziedziczącą po `ProviderSettingsBase` (`WindowsSearch.Common.Models`):

```csharp
using System.ComponentModel.DataAnnotations;
using WindowsSearch.Common.Models;

namespace MyNewProvider.Settings;

public sealed class MyNewProviderSettings : ProviderSettingsBase
{
    public MyNewProviderSettings()
    {
        Endpoints.NamedPipe = @"\\.\pipe\my_new_provider";
    }

    [Required(ErrorMessage = "Action path is required.")]
    [Display(Name = "Action path", Description = "Executable launched when a result is activated.")]
    public string ActionPath { get; set; } = "notepad.exe";
}
```

- `ProviderSettingsBase` dostarcza pola wspólne dla każdego dostawcy: `IsEnabled`, `LogLevel` oraz zagnieżdżoną grupę `Endpoints` (`NamedPipe`, `Http`, `Grpc`) z odpowiednimi atrybutami walidacji (`[HttpEndpoint]`/`[NamedPipeEndpoint]`) i `[Display]`. Flaga `IsEnabled` (w YAML `is_enabled`) pozwala na całkowite wyłączenie dostawcy - Hub nie załaduje go ani nie uruchomi jego procesu. Domyślne wartości dla endpointów nadpisuje się w konstruktorze. Używamy konwencji `UnderscoredNamingConvention` (snake_case) w całej aplikacji.

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

Ustawienia samego Hub (`Hub/Hub/Models/Settings/AppSettings.cs` - m.in. zagnieżdżone grupy `Search` i `Provider`, domyślny transport, limit wyszukiwania, poziom logowania, tryb wyszukiwania dostawców) używają tego samego wzorca: atrybuty `[Display]`/`[Range]` + `SettingsValidationHelper.Validate` przy zapisie, i są renderowane tym samym generycznym formularzem co ustawienia dostawców (`Hub/SettingsWindow.xaml.cs`, metoda `ExtractWritableSettings`). Walidacja i UI poprawnie obsługują dowolne zagęszczenie (zagnieżdżone klasy).

Szczególnie istotnym ustawieniem Hub'a zdefiniowanym w `ProviderSettings` jest `SearchMode` (`ProviderSearchMode`), które kontroluje sposób odpytywania dostawców:
- **Sequential** (domyślnie): Odpytuje dostawców jeden po drugim (synchronicznie z punktu widzenia pętli), wyświetlając wyniki dopiero po zakończeniu wszystkich. Używane w pracy magisterskiej dla zagwarantowania spójności pomiarów blokujących.
- **ConcurrentBlocking**: Uruchamia zapytania do wszystkich dostawców równolegle (asynchronicznie), ale czeka na zakończenie najwolniejszego z nich, zanim zaktualizuje interfejs użytkownika (wyniki pojawiają się jednocześnie).
- **ConcurrentStream**: Najbardziej responsywny tryb (strumieniowy). Odpytuje wszystkich równolegle i aktualizuje interfejs Hub'a na bieżąco, w miarę spływania odpowiedzi od poszczególnych dostawców (nie blokuje GUI na czas oczekiwania na wolniejsze żądania sieciowe np. w `FirefoxProvider`).


## 4. Testowanie Reguł Walidacyjnych

Wszystkie reguły walidacyjne (`[Required]`, `[Range]`, itp.) zdefiniowane w plikach ustawień powinny zostać pokryte testami jednostkowymi. Testy gwarantują, że mechanizm `SettingsValidationHelper` prawidłowo identyfikuje braki oraz zwraca konkretne komunikaty błędów zdefiniowane w atrybutach (np. `ErrorMessage`).

- Testy znajdują się zawsze w projekcie `<Nazwa>.Tests` wewnątrz wtyczki (np. `Providers/MyNewProvider/MyNewProvider.Tests/`).
- Pliki testowe przechowuje się w podkatalogu `Settings/` projektów testowych (np. `Settings/MyNewProviderSettingsTests.cs`), aby odzwierciedlały one płaską strukturę testowanej biblioteki.
- Do testów instancjonowane są oryginalne obiekty ustawień.

Przykład testu w xUnit:
```csharp
[Fact]
public void Validate_MissingActionPath_ReturnsRequiredErrorMessage()
{
    var settings = new MyNewProviderSettings { ActionPath = "" };
    var errors = SettingsValidationHelper.Validate(settings);
    
    Assert.Single(errors);
    Assert.Equal("Action path is required.", errors[0]);
}
```

## 5. Lokalizacja plików
- Model bazowy: `Shared/WindowsSearch.Common/Models/ProviderSettingsBase.cs`
- Fallback (wolny edytor): `Shared/WindowsSearch.Common/Models/GenericProviderSettings.cs`
- Wspólne YAML I/O: `Shared/WindowsSearch.Common/Serialization/ProviderSettingsYaml.cs`
- Wspólna walidacja: `Shared/WindowsSearch.Common/Validation/SettingsValidationHelper.cs`
- Odkrywanie/ładowanie po stronie Hub: `Hub/Hub/Services/Settings/ProviderSettingsService.cs`
- Generyczny formularz ustawień: `Hub/SettingsWindow.xaml.cs`
- Przykłady typowanych ustawień: `Providers/JetBrainsProvider/JetBrainsProvider.Settings/`, `Providers/DemoProvider/DemoProvider.Settings/`
