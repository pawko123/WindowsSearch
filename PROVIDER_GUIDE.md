# Przewodnik tworzenia providera

Provider to **oddzielny proces** (dowolny język/runtime) nasłuchujący na Named Pipe. Host uruchamia go, wysyła zapytania i odbiera wyniki wyszukiwania w formacie Protobuf. Ten dokument opisuje:

1. [Kontrakt komunikacyjny](#1-kontrakt-komunikacyjny--niezależny-od-języka) — niezależny od języka
2. [Plik konfiguracyjny YAML](#2-plik-konfiguracyjny-yaml) — jak host odkrywa provider
3. [Cykl życia procesu providera](#3-cykl-życia-procesu-providera)
4. [Implementacja w C#](#4-implementacja-w-c--szczegółowa) — szczegółowa implementacja referencyjna

---

## 1. Kontrakt komunikacyjny — niezależny od języka

### 1.1 Named Pipe

Host tworzy połączenie do providera przez Windows Named Pipe:

```
\\.\pipe\{nazwa}_provider
```

Gdzie `{nazwa}` pochodzi z nazwy pliku YAML (`firefox_provider.yaml` → `firefox`).

| Rola | Typ strumienia |
|---|---|
| Host | `NamedPipeClientStream` — **klient** |
| Provider | `NamedPipeServerStream` — **serwer** |

Provider musi nasłuchiwać na tej nazwie **zanim** host spróbuje się połączyć. Host przekazuje pełną nazwę pipe'a jako pierwszy argument CLI przy uruchamianiu providera:

```
FirefoxProvider.exe \\.\pipe\firefox_provider
```

Provider odczytuje `args[0]` i nasłuchuje dokładnie na tej nazwie.

---

### 1.2 Schemat wiadomości Protobuf (`search.proto`)

```protobuf
syntax = "proto3";

package search;

// Zapytanie wysyłane przez hosta do providera
message SearchRequest {
  string query                = 1;  // tekst wpisany przez użytkownika
  int32  limit                = 2;  // maks. liczba wyników łącznie
  map<string, string> settings = 3; // konfiguracja z _provider.yaml, read-only
}

// Pojedynczy wynik wyszukiwania
message ResultItem {
  string          title       = 1;  // główna etykieta w UI
  string          subtitle    = 2;  // opis dodatkowy (URL, ścieżka)
  float           score       = 3;  // trafność — opcjonalne, host może sortować
  string          action_path = 4;  // EXE / URL / plik do otwarcia
  repeated string action_args = 5;  // argumenty do action_path
  string          icon_path   = 6;  // ścieżka do PNG/ICO/SVG
}

// Kategoria grupująca wyniki (np. "Historia", "Zakładki")
message ResultCategory {
  string          name        = 1;  // wyświetlana nazwa kategorii
  string          icon_path   = 2;  // ikona nagłówka kategorii
  repeated ResultItem items   = 3;  // wyniki w tej kategorii
}

// Odpowiedź providera
message SearchResponse {
  repeated ResultCategory categories = 1;
}
```

> Plik `search.proto` przechowywany jest w katalogu `/proto/search.proto`. Provider i host korzystają z **tej samej definicji** — nigdy nie kopiuj `.proto` do każdego projektu osobno; wskazuj na plik relatywną ścieżką.

---

### 1.3 Protokół Length-Prefix (framing)

Named Pipe nie ma wbudowanego podziału na wiadomości — każda wiadomość poprzedzona jest **4-bajtowym nagłówkiem** big-endian zawierającym długość serializowanej wiadomości Protobuf:

```
[ 4 bajty: długość N (big-endian int32) ][ N bajtów: dane Protobuf ]
```

**Odczyt:**
1. Czytaj dokładnie 4 bajty → zdekoduj `N` (big-endian `int32`)
2. Czytaj dokładnie `N` bajtów → deserializuj wiadomość Protobuf

**Zapis:**
1. Serializuj wiadomość Protobuf do bajtów → uzyskaj `N` bajtów
2. Zapisz 4 bajty nagłówka (big-endian `N`)
3. Zapisz `N` bajtów danych

Provider MUSI stosować ten schemat — host zawsze wysyła `SearchRequest` i oczekuje `SearchResponse` w tym formacie.

---

### 1.4 Przepływ jednego zapytania

```
Host                                  Provider
  │                                       │
  │── connect() ──────────────────────── │ (host łączy się, provider już nasłuchuje)
  │                                       │
  │── [4B length][SearchRequest bytes] ──►│
  │                                       │ (provider przetwarza zapytanie)
  │◄── [4B length][SearchResponse bytes] ─│
  │                                       │
  │  (połączenie pozostaje otwarte)       │
  │── kolejny SearchRequest ─────────────►│
  │◄── kolejny SearchResponse ────────────│
```

Połączenie pipe pozostaje **trwale otwarte** — host nie zamyka go po każdym zapytaniu. Provider musi obsługiwać kolejne `SearchRequest` w pętli.

---

## 2. Plik konfiguracyjny YAML

Każdy provider wymaga pliku `{nazwa}_provider.yaml` w katalogu `providers/` (ścieżka konfigurowana w `app_config.yaml` hosta):

```yaml
executable_path: "C:\\Providers\\FirefoxProvider\\FirefoxProvider.exe"
priority: 10
settings:
  browser_path: "C:\\Program Files\\Mozilla Firefox\\firefox.exe"
  profile_path: "C:\\Users\\user\\AppData\\Roaming\\Mozilla\\Firefox\\Profiles\\default"
```

| Pole | Typ | Wymagane | Opis |
|---|---|---|---|
| `executable_path` | string | tak | Pełna ścieżka do EXE providera |
| `priority` | int | tak | Kolejność bloków wyników w UI (niższy = wyżej); nie wpływa na kolejność w obrębie jednego providera |
| `settings` | mapa string→string | nie | Dowolne klucze-wartości przekazywane do każdego `SearchRequest.settings` |

**Konwencja nazewnictwa:**

```
firefox_provider.yaml  →  nazwa: "firefox"  →  pipe: "\\.\pipe\firefox_provider"
```

Identyfikator nazwy wyciągany jest wyłącznie z nazwy pliku YAML — **nie jest wpisywany wewnątrz pliku YAML**.

---

## 3. Cykl życia procesu providera

```
[Uruchomienie przez hosta]
       │
       ▼
Odczytaj args[0] → nazwa pipe'a
       │
       ▼
Utwórz NamedPipeServerStream(pipeName)
       │
       ▼
WaitForConnection() ← host łączy się w tym momencie
       │
       ▼
  ┌────────────────────────────────────┐
  │            Pętla główna            │
  │                                    │
  │  ReceiveMessage() → SearchRequest  │
  │  Przetwórz zapytanie               │
  │  SendMessage() → SearchResponse    │
  │                                    │
  └────────────────────────────────────┘
       │ (host zamknął połączenie lub proces zakończony)
       ▼
    Zakończ
```

**Zasady:**
- Provider **nie kończy się** po obsłużeniu jednego zapytania — czeka na kolejne.
- Provider **nie inicjuje** połączenia — czeka aż host się podłączy.
- Provider **nie zapisuje** stanu sesji — każdy `SearchRequest` jest niezależny.
- Pola `settings` z `SearchRequest` są read-only — provider nie odsyła ich z powrotem.
- Provider powinien odpowiedzieć **poniżej 200ms** — host ma timeout i pomija wyniki po jego przekroczeniu.

---

## 4. Implementacja w C# — szczegółowa

### 4.1 Struktura projektu

```
exampleProviders/
└── FirefoxProvider/                        ← korzeń solucji (otwierany w Rider / VS)
    ├── FirefoxProvider.sln                 ← plik solucji
    ├── FirefoxProvider/                    ← projekt główny (EXE)
    │   ├── FirefoxProvider.csproj
    │   ├── Program.cs                      ← główna logika: pipe + pętla
    │   ├── PipeProtocol.cs                 ← Length-Prefix framing
    │   └── SearchLogic.cs                  ← właściwa logika wyszukiwania (opcjonalne)
    └── FirefoxProvider.Tests/              ← projekt testów jednostkowych NUnit (wymagany przez CI)
        └── FirefoxProvider.Tests.csproj
```

Każdy provider to **osobna solucja** (`.sln`) otwierana bezpośrednio w JetBrains Rider lub Visual Studio. Solucja zawiera projekt `Exe` oraz projekt testowy NUnit — oba muszą być dodane do `.sln`, bo CI wykonuje `dotnet test {Nazwa}.sln` przed każdym publish.

---

### 4.2 Tworzenie solucji — scaffold

Uruchom poniższe polecenia w katalogu `exampleProviders/` aby wygenerować strukturę od zera:

```bash
# Przejdź do katalogu providerów
cd exampleProviders

# Utwórz katalog solucji i wejdź do niego
mkdir FirefoxProvider && cd FirefoxProvider

# Utwórz plik solucji
dotnet new sln -n FirefoxProvider

# Utwórz projekt główny (EXE) w podkatalogu o tej samej nazwie
dotnet new console -n FirefoxProvider -f net8.0-windows -o FirefoxProvider

# Utwórz projekt testowy NUnit (wymagany — CI uruchamia dotnet test przed publish)
dotnet new nunit -n FirefoxProvider.Tests -f net8.0 -o FirefoxProvider.Tests

# Dodaj projekty do solucji
dotnet sln FirefoxProvider.sln add FirefoxProvider/FirefoxProvider.csproj
dotnet sln FirefoxProvider.sln add FirefoxProvider.Tests/FirefoxProvider.Tests.csproj

# Rider / VS: otwórz FirefoxProvider.sln
```

---

### 4.3 `FirefoxProvider.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <RuntimeIdentifiers>win-x64</RuntimeIdentifiers>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>FirefoxProvider</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Google.Protobuf" Version="3.*" />
    <PackageReference Include="Grpc.Tools" Version="2.*" PrivateAssets="All" />
  </ItemGroup>

  <!-- Wskaż wspólny .proto — ścieżka relatywna do katalogu projektu -->
  <!-- Projekt leży w: exampleProviders/FirefoxProvider/FirefoxProvider/ -->
  <!-- Proto leży w:   proto/search.proto (korzeń repo)                 -->
  <ItemGroup>
    <Protobuf Include="..\..\..\proto\search.proto" GrpcServices="None" />
  </ItemGroup>
</Project>
```

> `GrpcServices="None"` — generujemy wyłącznie klasy wiadomości (nie stub gRPC). Komunikacja odbywa się przez Named Pipe.

> **Ścieżka do `.proto`:** projekt `FirefoxProvider.csproj` leży trzy poziomy niżej od korzenia repozytorium (`exampleProviders/FirefoxProvider/FirefoxProvider/`), stąd trzy `..\` przed `proto\search.proto`.

Po zbudowaniu projektu `Grpc.Tools` automatycznie wygeneruje klasy `SearchRequest`, `SearchResponse`, `ResultItem`, `ResultCategory` w przestrzeni nazw `Search` (z `package search;` w `.proto`).

---

### 4.4 `PipeProtocol.cs` — Length-Prefix framing

```csharp
using Google.Protobuf;
using System.Buffers.Binary;

namespace FirefoxProvider;

public static class PipeProtocol
{
    /// <summary>Serializuje wiadomość Protobuf i wysyła ją poprzedzoną 4-bajtowym nagłówkiem (big-endian).</summary>
    public static async Task SendMessageAsync<T>(Stream stream, T message)
        where T : IMessage<T>
    {
        byte[] data = message.ToByteArray();
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, data.Length);

        await stream.WriteAsync(header);
        await stream.WriteAsync(data);
        await stream.FlushAsync();
    }

    /// <summary>Czyta 4-bajtowy nagłówek, następnie N bajtów danych i deserializuje wiadomość Protobuf.</summary>
    public static async Task<T> ReceiveMessageAsync<T>(Stream stream)
        where T : IMessage<T>, new()
    {
        byte[] header = await ReadExactAsync(stream, 4);
        int length = BinaryPrimitives.ReadInt32BigEndian(header);

        byte[] data = await ReadExactAsync(stream, length);

        var parser = new MessageParser<T>(() => new T());
        return parser.ParseFrom(data);
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int count)
    {
        byte[] buffer = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset));
            if (read == 0)
                throw new EndOfStreamException("Pipe closed before full message was received.");
            offset += read;
        }
        return buffer;
    }
}
```

---

### 4.5 `Program.cs` — pętla główna

```csharp
using System.IO.Pipes;
using FirefoxProvider;
using Search; // namespace wygenerowany z search.proto

// args[0] = pełna nazwa pipe'a przekazana przez hosta, np. "\\.\pipe\firefox_provider"
// NamedPipeServerStream przyjmuje tylko część po ostatnim '\' jako pipeName
if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: FirefoxProvider.exe <pipe_name>");
    return 1;
}

string fullPipeName = args[0];
// Wytnij prefix "\\.\pipe\" — NamedPipeServerStream chce tylko "firefox_provider"
string pipeName = fullPipeName.Replace(@"\\.\pipe\", "").TrimStart('\\');

Console.WriteLine($"[FirefoxProvider] Starting, listening on pipe: {pipeName}");

await using var pipeServer = new NamedPipeServerStream(
    pipeName,
    PipeDirection.InOut,
    maxNumberOfServerInstances: 1,
    PipeTransmissionMode.Byte,
    PipeOptions.Asynchronous);

Console.WriteLine("[FirefoxProvider] Waiting for host connection...");
await pipeServer.WaitForConnectionAsync();
Console.WriteLine("[FirefoxProvider] Host connected.");

// Pętla obsługi zapytań — działa dopóki host nie zamknie połączenia
while (pipeServer.IsConnected)
{
    try
    {
        var request = await PipeProtocol.ReceiveMessageAsync<SearchRequest>(pipeServer);
        Console.WriteLine($"[FirefoxProvider] Query: '{request.Query}', limit: {request.Limit}");

        // Odczytaj ustawienia wstrzyknięte przez hosta z _provider.yaml
        string browserPath = request.Settings.GetValueOrDefault("browser_path", "firefox.exe");
        string profilePath = request.Settings.GetValueOrDefault("profile_path", "");

        // ── Właściwa logika wyszukiwania ────────────────────────────────────
        SearchResponse response = await BuildSearchResponse(request, browserPath, profilePath);
        // ────────────────────────────────────────────────────────────────────

        await PipeProtocol.SendMessageAsync(pipeServer, response);
    }
    catch (EndOfStreamException)
    {
        Console.WriteLine("[FirefoxProvider] Host disconnected.");
        break;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[FirefoxProvider] Error: {ex.Message}");
        break;
    }
}

return 0;

// ── Pomocnicze: buduje SearchResponse na podstawie zapytania ─────────────────
static Task<SearchResponse> BuildSearchResponse(SearchRequest request, string browserPath, string profilePath)
{
    // TODO: właściwa implementacja (SQLite na bazie profilu Firefox itp.)
    // Poniżej minimalna implementacja zwracająca jeden wynik przykładowy

    var item = new ResultItem
    {
        Title      = $"Szukaj: {request.Query}",
        Subtitle   = $"https://www.google.com/search?q={Uri.EscapeDataString(request.Query)}",
        Score      = 1.0f,
        ActionPath = browserPath,
        IconPath   = ""   // ścieżka do ikony Firefoxa jeśli masz
    };
    item.ActionArgs.Add($"https://www.google.com/search?q={Uri.EscapeDataString(request.Query)}");

    var category = new ResultCategory
    {
        Name     = "Firefox",
        IconPath = ""
    };
    category.Items.Add(item);

    var response = new SearchResponse();
    response.Categories.Add(category);

    return Task.FromResult(response);
}
```

---

### 4.6 Budowanie i testowanie lokalnie

```bash
# Otwórz solucję w Rider / VS → kliknij dwukrotnie FirefoxProvider.sln

# Lub z terminala — wejdź do katalogu solucji:
cd exampleProviders/FirefoxProvider

# Restore + build całej solucji (razem z projektem testowym)
dotnet build FirefoxProvider.sln

# Uruchom testy (jeśli projekt testowy istnieje)
dotnet test FirefoxProvider.sln

# Publish self-contained win-x64 (identycznie jak CI)
dotnet publish FirefoxProvider/FirefoxProvider.csproj \
  -c Release -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o ./publish

# Uruchom manualnie (symuluj wywołanie przez hosta)
./publish/FirefoxProvider.exe \\.\pipe\firefox_provider
```

Testowanie komunikacji: napisz prosty skrypt testowy w C# lub użyj `NamedPipeClientStream` z poziomu REPL, który wyśle `SearchRequest` i wypisze otrzymany `SearchResponse`.

---

### 4.7 Testy jednostkowe — `FirefoxProvider.Tests`

Celem testów jest weryfikacja logiki wyszukiwania i serializacji **bez uruchamiania pipe'a**. Testuj jednostkowo metody z `SearchLogic.cs` i `PipeProtocol.cs`, nie cały proces.

**`FirefoxProvider.Tests.csproj`:**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="NUnit" Version="4.*" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.*" PrivateAssets="All" />
    <PackageReference Include="Google.Protobuf" Version="3.*" />
  </ItemGroup>

  <!-- Referencja do projektu głównego — dostęp do klas SearchLogic, PipeProtocol -->
  <ItemGroup>
    <ProjectReference Include="..\FirefoxProvider\FirefoxProvider.csproj" />
  </ItemGroup>
</Project>
```

**Przykładowy test `SearchLogicTests.cs`:**

```csharp
using NUnit.Framework;
using Search;

namespace FirefoxProvider.Tests;

[TestFixture]
public class SearchLogicTests
{
    [Test]
    public async Task BuildSearchResponse_ReturnsAtLeastOneCategory()
    {
        var request = new SearchRequest { Query = "test", Limit = 5 };

        SearchResponse response = await SearchLogic.BuildAsync(request, browserPath: "firefox.exe", profilePath: "");

        Assert.That(response.Categories, Is.Not.Empty);
    }

    [Test]
    public async Task BuildSearchResponse_TitleContainsQuery()
    {
        var request = new SearchRequest { Query = "mozilla", Limit = 5 };

        SearchResponse response = await SearchLogic.BuildAsync(request, browserPath: "firefox.exe", profilePath: "");

        Assert.That(
            response.Categories.SelectMany(c => c.Items),
            Has.Some.Matches<ResultItem>(item =>
                item.Title.Contains("mozilla", StringComparison.OrdinalIgnoreCase)
                || item.Subtitle.Contains("mozilla", StringComparison.OrdinalIgnoreCase)));
    }

    [Test]
    public void PipeProtocol_RoundTrip_SearchRequest()
    {
        // Weryfikuje Length-Prefix framing: serialize → stream → deserialize
        var original = new SearchRequest { Query = "roundtrip", Limit = 10 };
        using var stream = new System.IO.MemoryStream();

        PipeProtocol.SendMessageAsync(stream, original).GetAwaiter().GetResult();
        stream.Position = 0;
        var decoded = PipeProtocol.ReceiveMessageAsync<SearchRequest>(stream).GetAwaiter().GetResult();

        Assert.That(decoded.Query, Is.EqualTo(original.Query));
        Assert.That(decoded.Limit, Is.EqualTo(original.Limit));
    }
}
```

> **Co testować:** logikę filtrowania/scoringu wyników, parsowanie danych źródłowych (SQLite, API itp.), round-trip serializacji Protobuf przez `PipeProtocol`, obsługę pustego zapytania i zapytania z `limit = 0`.

---

### 4.8 Plik konfiguracyjny do skopiowania (szablon)

Umieść poniższy plik jako `firefox_provider.yaml` w katalogu `providers/` obok aplikacji hostowanej:

```yaml
executable_path: "C:\\Providers\\FirefoxProvider\\FirefoxProvider.exe"
priority: 10
settings:
  browser_path: "C:\\Program Files\\Mozilla Firefox\\firefox.exe"
  profile_path: "C:\\Users\\TWOJ_USER\\AppData\\Roaming\\Mozilla\\Firefox\\Profiles\\default"
```

---

## 5. Checklist — gotowość providera

Przed dodaniem providera do repozytorium sprawdź:

- [ ] Katalog providera umieszczony w `/exampleProviders/{NazwaProvidra}/`
- [ ] Plik solucji `{NazwaProvidra}.sln` istnieje w katalogu `exampleProviders/{NazwaProvidra}/`
- [ ] Projekt EXE leży w podkatalogu `{NazwaProvidra}/{NazwaProvidra}/` (Rider widzi poprawną strukturę)
- [ ] `dotnet build {NazwaProvidra}.sln` kończy się bez błędów
- [ ] `dotnet test {NazwaProvidra}.sln` — wszystkie testy przechodzą (CI blokuje publish przy nieprzechodzących testach)
- [ ] Projekt `{NazwaProvidra}.Tests` dodany do solucji (`dotnet sln add ...`)
- [ ] `dotnet publish {NazwaProvidra}/{NazwaProvidra}.csproj -r win-x64 --self-contained true` generuje działający EXE
- [ ] Provider odczytuje nazwę pipe'a z `args[0]`
- [ ] Provider nasłuchuje na `NamedPipeServerStream` z podaną nazwą
- [ ] Provider implementuje protokół Length-Prefix (4B big-endian + Protobuf)
- [ ] Provider odpowiada w < 200ms dla typowych zapytań
- [ ] Plik `{nazwa}_provider.yaml` istnieje i ma poprawną ścieżkę do EXE
- [ ] Plik `.csproj` zawiera `<Protobuf Include="..\..\..\proto\search.proto" GrpcServices="None" />` (3 poziomy wyżej do korzenia repo)
- [ ] Plik `.csproj` ma `<OutputType>Exe</OutputType>` i `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>`
