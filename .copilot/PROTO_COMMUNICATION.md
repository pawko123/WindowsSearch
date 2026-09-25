# Komunikacja z Dostawcami (Communication Guide)

## 1. Wstęp
Dawniej komunikacja w projekcie opierała się w 100% na protobufie z centralnym `GenericMessage`. Po ogromnym refaktoringu projekt używa architektury wielotransportowej (multi-transport). Wiele protokołów jest wspieranych jednocześnie, w zależności od tego, jak zostanie zainicjowany `BaseProviderHost`.

Obecnie wspierane transporty to:
- **Named Pipes**: Używa serializacji w formacie `System.Text.Json`. Zapewnia najszybszą komunikację z lokalnymi procesami.
- **gRPC**: Tradycyjne połączenie oparte o protokoły Protobuf. Użyteczne do komunikacji międzyserwerowej lub procesów wymagających ścisłych kontraktów.
- **HTTP**: Domyślna komunikacja w formacie REST / JSON.

Wszystkie te transporty są obsługiwane i obudowane przez pakiet `BaseProvider`. Dostawca implementuje generyczny interfejs `IResultFinder<TSettings>`, a za transport i kodowanie odpowiadają fabryki oraz klasy takie jak `TransportMessageCodec.cs` i `ProviderTransportFactory.cs`.

## 2. Modele Danych
Wspólne kontrakty komunikacyjne (`ProviderSearchRequest`, `ProviderSearchResponse`, `ProviderResultCategory`, `ProviderResultItem`, `ProviderSettingsBase`) są zdefiniowane w `Shared/WindowsSearch.Common/Models/`, nie w `Providers/BaseProvider/Models/`.

Najważniejsze kontrakty:
- `ProviderSearchRequest`: zawiera dane o zapytaniu (`Query`), limicie (`Limit`) oraz `SettingsYaml` - bieżący snapshot typowanych ustawień dostawcy (zserializowany do YAML), wysyłany przy każdym zapytaniu tak, aby edycja ustawień w Hub działała bez restartu procesu dostawcy. Zobacz `SETTINGS_GUIDE.md`.
- `ProviderSearchResponse`: zwracany do huba, zawiera listę `ProviderResultCategory`.
- `ProviderResultCategory`: grupuje wyniki, zawiera kolekcję `ProviderResultItem`.
- `ProviderResultItem`: pojedynczy obiekt reprezentujący wynik z tytułem, podtytułem, ikoną i akcjami.
- `ProviderSettingsBase`: bazowy, typowany i walidowany model ustawień (transport/endpoint/serializacja/timeout/log level). Każdy dostawca ma własną podklasę we własnym projekcie `<Nazwa>.Settings` - szczegóły w `SETTINGS_GUIDE.md`.

## 3. Abstrakcja Transportu
Zamiast ręcznego pisania nasłuchiwania w pętli na `NamedPipeServerStream`, `BaseProvider` używa modelu opartego na interfejsach:
- `IProviderTransport` - interfejs implementowany przez poszczególne warstwy transportu (`NamedPipeProviderTransport`, `GrpcProviderTransport`, `HttpProviderTransport`).
- `IProviderTransportFactory` - tworzy odpowiedni transport na podstawie przekazanych flag i preferencji podczas startu programu.

Większość nowo tworzonych dostawców nie musi bezpośrednio modyfikować protokołu komunikacji. Wszystko jest spakowane w `BaseProviderHost.RunAsync(...)`.
