# Przewodnik po Ustawieniach (Settings Guide)

W nowej architekturze obsługiwanej przez `BaseProvider`, mechanizm ustawień został uproszczony i jest silnie typowany. Zamiast manualnie budować odpowiedzi z użyciem starego schematu Protobuf, dostawcy zwracają instancję klasy `ProviderSettings`.

## 1. Definiowanie Ustawień w Dostawcy

Kiedy implementujesz interfejs `IResultFinder` w swoim dostawcy, musisz zaimplementować metodę `GetSettingsAsync`:

```csharp
using BaseProvider.Abstractions;
using BaseProvider.Models;

namespace MyNewProvider;

public class MyResultFinder : IResultFinder
{
    // ... implementacja SearchAsync ...

    public Task<ProviderSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = new ProviderSettings
        {
            // Możesz tu zdefiniować swoje właściwości / ustawienia.
            // Model ProviderSettings z BaseProvider/Models dostarcza właściwą strukturę.
        };

        return Task.FromResult(settings);
    }
}
```

## 2. Dostęp do Ustawień
Ustawienia są ładowane dynamicznie podczas uruchamiania dostawcy. Podczas pierwszego połączenia (przez Named Pipe, gRPC, lub HTTP), aplikacja Host może odpytać Twojego dostawcę wywołując jego endpoint odpowiedzialny za podanie konfiguracji.

Dzięki wspólnej klasie `ProviderSettings`, aplikacja Hub odbiera zunifikowaną strukturę w formacie JSON lub Protobuf i może dynamicznie zbudować formularze w interfejsie użytkownika.

## 3. Lokalizacja plików
Podstawowa definicja modelu znajduje się w pliku: `Providers/BaseProvider/Models/ProviderSettings.cs`.
Jeżeli potrzebujesz zdefiniować nowe typy ustawień (np. enumy lub własne modele formularzy), skonsultuj się z definicjami w tym pliku.
