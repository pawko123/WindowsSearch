# Zadanie: Stworzenie aplikacji hosta dla wyszukiwarki w stylu Spotlight

## Framework i styl UI

- **Framework:** WPF
- **Styl UI:** Minimalistyczny, wyśrodkowany pasek wyszukiwania (wzorowany na Spotlight/GNOME Activities). Przezroczyste tło Acrylic.

---

## Model cyklu życia — Tray App (nie Windows Service)

Aplikacja działa **stale w tle** jako proces użytkownika. Nie jest usługą Windows — usługi działają w Session 0, która nie ma dostępu do UI, globalnych hotkeys ani danych użytkownika (Start menu, profile przeglądarek itp.).

- **Autostart:** rejestracja w kluczu rejestru `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- Okno wyszukiwania istnieje cały czas w pamięci — jest **ukrywane**, nigdy nie zamykane
- Przy utracie focusa lub naciśnięciu `Escape` — okno wraca do stanu ukrytego
- Ikona w zasobniku systemowym (tray) z menu kontekstowym: Ustawienia, Wyjdź
- Restart przy nieoczekiwanym zamknięciu konfigurowalny przez Task Scheduler (flaga "uruchom ponownie przy błędzie")

---

## Globalny hotkey

- Rejestrowany przez Win32 API `RegisterHotKey` / `UnregisterHotKey` przy starcie aplikacji
- Host nasłuchuje `WM_HOTKEY` w pętli komunikatów — działa niezależnie od aktywnego okna
- Skrót klawiszowy konfigurowalny w `app_config.yaml`
- Po naciśnięciu: okno wyszukiwania pokazuje się i otrzymuje focus; pole wyszukiwania jest czyszczone
- **Domyślny skrót:** `Ctrl+Alt+Space` — mapuje się na flagi `MOD_CONTROL | MOD_ALT` + `VK_SPACE`; brak konfliktów z systemowymi skrótami Windows ani typowymi skrótami IDE

---

## Plik konfiguracyjny hosta — `app_config.yaml`

Główny plik konfiguracyjny aplikacji (nie mylić z plikami providerów):

```yaml
hotkey: "Ctrl+Alt+Space"
refresh_interval_seconds: 60
providers_directory: "providers"
provider_timeout_seconds: 5
```

| Pole | Opis |
|---|---|
| `hotkey` | Skrót klawiszowy otwierający okno wyszukiwania |
| `refresh_interval_seconds` | Interwał cyklicznego odświeżania listy plików `_provider.yaml` i indeksu aplikacji |
| `providers_directory` | Ścieżka do katalogu zawierającego pliki `{nazwa}_provider.yaml` |
| `provider_timeout_seconds` | Maksymalny czas oczekiwania na odpowiedź jednego providera; po przekroczeniu — pomiń i zaloguj |

---

## Pliki konfiguracyjne providerów — `{nazwa}_provider.yaml`

### Konwencja nazewnictwa

Plik musi być nazwany `{nazwa}_provider.yaml`. Identyfikator `{nazwa}` jest wyciągany wyłącznie z nazwy pliku — **nigdy nie jest wpisywany wewnątrz YAML** (eliminuje rozbieżności między nazwą pliku a jego zawartością).

### Schemat pliku

```yaml
executable_path: "C:\\Providers\\FirefoxProvider\\FirefoxProvider.exe"
priority: 10
settings:
  browser_path: "C:\\Program Files\\Mozilla Firefox\\firefox.exe"
  profile_path: "C:\\Users\\user\\AppData\\Roaming\\Mozilla\\Firefox\\Profiles\\default"
```

| Pole | Typ | Opis |
|---|---|---|
| `executable_path` | string | Ścieżka do pliku wykonywalnego procesu providera |
| `priority` | int | Określa kolejność bloków wyników providerów na liście — niższa wartość = wyżej; nie miesza kategorii różnych providerów ze sobą |
| `settings` | mapa `string → string` | Dowolne pary klucz-wartość wstrzykiwane do każdego `SearchRequest.settings` |

### Konwencja NamedPipe

Na podstawie nazwy pliku host wyznacza nazwę pipe'a:

```
firefox_provider.yaml  →  nazwa: "firefox"  →  pipe: "\\.\pipe\firefox_provider"
```

Host przekazuje nazwę pipe'a jako argument CLI przy uruchamianiu procesu providera. Provider musi nasłuchiwać dokładnie na tej nazwie.

---

## `ProviderDescriptor` — model in-memory

Struktura budowana z YAML przy starcie i przy każdym odświeżeniu przez `RefreshWorker`. Przechowywana w pamięci — **nie zawiera referencji do procesu ani pipe'a** (providery są uruchamiane na żądanie):

| Pole | Typ | Opis |
|---|---|---|
| `Name` | `string` | Wyciągnięty z nazwy pliku YAML (bez `_provider.yaml`) |
| `ExecutablePath` | `string` | Ścieżka do EXE providera |
| `PipeName` | `string` | Wyliczona nazwa pipe'a: `{nazwa}_provider` |
| `Priority` | `int` | Kolejność wyświetlania bloków wyników |
| `Settings` | `Dictionary<string, string>` | Wstrzykiwane do każdego `SearchRequest.settings` |

---

## `RefreshWorker` — background worker cykliczny

Oparty o `PeriodicTimer` (.NET 6+), uruchamiany przy starcie aplikacji. Wykonuje dwa niezależne zadania z interwałem z `app_config.yaml`:

### 1. Odświeżanie providerów

Skanuje katalog `providers_directory` w poszukiwaniu plików `*_provider.yaml`:

| Zmiana | Akcja |
|---|---|
| Nowy plik | Parsuje YAML, tworzy `ProviderDescriptor`, dodaje do listy |
| Usunięty plik | Usuwa descriptor z listy |
| Zmieniony plik (timestamp) | Przeładowuje descriptor |
| Bez zmian | Nie podejmuje żadnej akcji |

### 2. Odświeżanie indeksu aplikacji

Skanuje dwa źródła i odbudowuje indeks in-memory zainstalowanych programów:

- **Start menu:** `%APPDATA%\Microsoft\Windows\Start Menu\Programs` oraz `%PROGRAMDATA%\Microsoft\Windows\Start Menu\Programs` — pliki `.lnk` skanowane rekurencyjnie przez `Directory.EnumerateFiles("*.lnk", SearchOption.AllDirectories)`
- **Rejestr `App Paths`:** `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths` — każda podgałąź zawiera nazwę i pełną ścieżkę EXE

Deduplikacja po ścieżce EXE. Operacja tania (kilkaset plików), bezpieczna do wykonania co minutę w tle.

---

## Wbudowany launcher aplikacji (strefa górna)

Oddzielny komponent hosta — nie jest providerem, nie używa pipe'ów ani protobuf.

- Pre-indeksowanie przy starcie aplikacji w wątku tła
- Wyszukiwanie pełnotekstowe w indeksie in-memory — nigdy nie blokuje podczas debounce
- Wyniki zawsze wyświetlane **ponad** wynikami wszystkich providerów (jak na wzorcowym zrzucie ekranu)
- Własny model `AppLaunchResult`: `Name`, `ExecutablePath`, `IconPath`
- `action_path` = ścieżka do EXE, `action_args` = puste

---

## Logika wyszukiwania — `ProviderOrchestrator`

Provider **nie jest uruchamiany przy starcie hosta**. Proces providera istnieje tylko przez czas trwania jednego wyszukiwania:

```
Użytkownik wpisuje znak (debounce 200 ms)
    │
    ▼
ProviderOrchestrator.QueryAllAsync(query, cancellationToken)
    │
    ├► foreach provider w równoległości (Task.WhenAll):
    │       1. Process.Start(ExecutablePath, PipeName)   ← uruchom proces
    │       2. NamedPipeClientStream.ConnectAsync()      ← połącz z pipe
    │       3. PipeProtocol.SendMessageAsync(SearchRequest)
    │       4. PipeProtocol.ReceiveMessageAsync<SearchResponse>()
    │       5. process.Kill() + process.Dispose()        ← zabij proces
    │       6. Zwróć SearchResponse
    │
    └► Agreguj wyniki → odśwież UI
```

- **Search-as-you-type:** debounce 200 ms — zapytanie wysyłane 200 ms po ostatnim naciśnięciu klawisza
- `CancellationTokenSource` anuluje poprzednie zapytanie (i killuje procesy providerów) gdy użytkownik wpisze kolejny znak
- **Timeout:** `provider_timeout_seconds` z `app_config.yaml` — przekroczenie = pomiń provider w wynikach, zaloguj `Warning`
- Wstrzykuje `Settings` z `ProviderDescriptor` do każdego `SearchRequest.settings`

---

## Wykonywanie akcji po wybraniu wyniku

Host odbiera wybrany `ResultItem` i wykonuje akcję przez `ProcessStartInfo`:

```csharp
var psi = new ProcessStartInfo(item.ActionPath);
foreach (var arg in item.ActionArgs)
    psi.ArgumentList.Add(arg); // poprawne escapowanie, .NET 5+
Process.Start(psi);
```

Windows sam rozpoznaje typ zasobu — EXE uruchamia, URL otwiera domyślną przeglądarkę, ścieżkę do pliku otwiera domyślną aplikacją dla danego rozszerzenia. Provider Firefox ustawia `action_path` na ścieżkę przeglądarki z `settings.browser_path` i przekazuje URL jako argument, co gwarantuje otwarcie wyniku właśnie w tej przeglądarce.

---

## Struktura solucji

```
Hub/
├── Hub.sln
├── Hub/                                  ← projekt główny WPF
│   ├── Hub.csproj
│   ├── App.xaml / App.xaml.cs
│   ├── SearchWindow.xaml / .cs           ← główne okno wyszukiwania
│   ├── TrayIcon.cs                       ← obsługa ikony tray
│   ├── HotkeyManager.cs                  ← RegisterHotKey / WM_HOTKEY
│   ├── ProviderOrchestrator.cs           ← uruchamianie providerów na żądanie
│   ├── RefreshWorker.cs                  ← odświeżanie listy providerów i indeksu app
│   ├── ConfigLoader.cs                   ← wczytywanie app_config.yaml i _provider.yaml
│   ├── AppIndexer.cs                     ← indeks Start Menu + App Paths
│   └── app_config.yaml                   ← domyślna konfiguracja
└── Hub.Tests/                            ← projekt testów NUnit
    └── Hub.Tests.csproj
```

Kod współdzielony z providerami (`PipeProtocol`, modele Protobuf) przechowywany jest w osobnym projekcie:

```
SearchEngine.Shared/
├── SearchEngine.Shared.sln
└── SearchEngine.Shared/
    ├── SearchEngine.Shared.csproj
    ├── PipeProtocol.cs                   ← Length-Prefix framing (Send / Receive)
    └── Proto/
        └── search.proto                  ← jedyna kopia definicji Protobuf
```

Zarówno `Hub.csproj` jak i każdy `{Nazwa}Provider.csproj` dodają `<ProjectReference>` do `SearchEngine.Shared.csproj` — **nigdy nie kopiuj `PipeProtocol.cs` ani `search.proto`**.

---

## UI — okno wyszukiwania

### Zachowanie

- Okno wyśrodkowane na ekranie (lub na aktywnym monitorze przy multi-monitor)
- Styl: Acrylic backdrop, zaokrąglone rogi, cień
- Przy stracie focusa (`Window.Activated` → `false`) i przy `Escape` — okno chowane (`AppWindow.Hide()`)
- Pole tekstowe otrzymuje focus automatycznie przy pokazaniu okna; zawartość czyszczona

### Wyświetlanie wyników

- Wyniki grupowane: **najpierw per provider** (wg `priority`), wewnątrz providera **per kategoria** (`ResultCategory.name`)
- Każda kategoria = osobny blok z nagłówkiem (`ResultCategory.name`) i ikoną (`ResultCategory.icon_path`)
- Każdy `ResultItem` wyświetlany z **ikoną** (`icon_path`, PNG/ICO), tytułem i podtytułem — ikony są wymogiem, nie opcją
- Lista wyników **scrollowalna** (`ScrollViewer`) — brak limitu liczby wyników
- Nawigacja: strzałki `↑` `↓`, `Tab`, `Enter` — uruchamia zaznaczony wynik

### ViewModel — dwie strefy

| Kolekcja | Typ elementu | Opis |
|---|---|---|
| `AppResults` | `ObservableCollection<AppLaunchResult>` | Strefa górna — launcher aplikacji, zawsze wyświetlany pierwszy |
| `ProviderCategories` | `ObservableCollection<CategoryViewModel>` | Strefa dolna — wyniki providerów posortowane wg `Priority` |

Lista providerów używa dwóch typów elementów (heterogeniczna, kompatybilna z UI Virtualization):
- `CategoryHeaderItem` — nagłówek kategorii z ikoną z `ResultCategory.icon_path`
- `ResultRowItem` — pojedynczy wynik z ikoną z `ResultItem.icon_path`

---

## Logowanie

- Biblioteka: `Microsoft.Extensions.Logging` + `Serilog` (sink: plik tekstowy)
- Plik logu: `%LOCALAPPDATA%\SearchEngine\logs\hub-.log` (rolling daily)
- Poziomy:
  - `Information` — start/stop aplikacji, wykrycie providerów, pokazanie/schowanie okna
  - `Warning` — provider timeout, provider nie odpowiedział
  - `Error` — błąd rejestracji hotkey, błąd parsowania YAML, nieoczekiwany wyjątek

---

## Wymagania techniczne

| Element | Wartość |
|---|---|
| .NET | 8.0 |
| Windows App SDK | najnowsza stabilna |
| Target Framework | `net8.0-windows10.0.19041.0` |
| Architektura | `win-x64` |
| Publish | `--self-contained true` |

### Pakiety NuGet (`Hub.csproj`)

| Pakiet | Cel |
|---|---|
| `Microsoft.WindowsAppSDK` | WPF host |
| `Google.Protobuf` | serializacja wiadomości |
| `Grpc.Tools` | generowanie kodu z `.proto` |
| `YamlDotNet` | parsowanie `app_config.yaml` i `_provider.yaml` |
| `Serilog.Extensions.Logging` | logowanie |
| `Serilog.Sinks.File` | sink do pliku |

### Pakiety NuGet (`Hub.Tests.csproj`)

| Pakiet | Cel |
|---|---|
| `NUnit` | framework testowy |
| `NUnit3TestAdapter` | adapter dla `dotnet test` |
| `Microsoft.NET.Test.Sdk` | runner testów |
| `NSubstitute` | mockowanie zależności |

---

## Co należy przetestować (Hub.Tests)

| Klasa | Scenariusze testowe |
|---|---|
| `ConfigLoader` | poprawny YAML, brakujące pola (wartości domyślne), nieprawidłowy YAML (wyjątek), wyciąganie `Name` z nazwy pliku |
| `ProviderOrchestrator` | agregacja wyników z wielu providerów, sortowanie wg `priority`, timeout → pominięcie providera, anulowanie poprzedniego zapytania |
| `HotkeyManager` | rejestracja/wyrejestrowanie (mock Win32), obsługa duplikatu rejestracji |

---

## Deliverables

- `SearchEngine.Shared` — projekt współdzielony: `PipeProtocol.cs` + `search.proto`
- XAML głównego widoku z dwustrefowym układem (launcher + providerzy), scrollowalny, z ikonami
- `SearchViewModel` z dwiema kolekcjami i debouncingiem 200 ms
- `ProviderOrchestrator` — uruchamianie providerów na żądanie, timeout, równoległość, killowanie procesu
- `RefreshWorker` z `PeriodicTimer` — odświeżanie listy `ProviderDescriptorów` i indeksu aplikacji
- `AppIndexer` — Start Menu + rejestr App Paths
- `HotkeyManager` — `RegisterHotKey` / `WM_HOTKEY`
- `TrayIcon` — ikona tray z menu kontekstowym
- `ConfigLoader` — parsowanie YAML
- `Hub.Tests` — testy NUnit dla `ConfigLoader`, `ProviderOrchestrator`, `HotkeyManager`
