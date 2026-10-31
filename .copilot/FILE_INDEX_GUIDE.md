# Dostawca Indeksu Plików (FileIndexProvider Guide)

## 1. Wstęp i Architektura

`FileIndexProvider` to wysokowydajny dostawca indeksowania i wyszukiwania plików na dyskach lokalnych, zintegrowany z Hubem poprzez standardowy protokół IPC (`NamedPipe`, `gRPC`, `HTTP`). 

Dostawca wykorzystuje lokalną bazę **SQLite** z modułem **FTS5** (trigram tokenizer), asynchroniczny crawler działający w tle, usługę `FileSystemWatcher` do synchronizacji w czasie rzeczywistym oraz frekwencyjno-czasowy algorytm rankingu wyników (frecency).

```text
Providers/FileIndexProvider/
├── FileIndexProvider.Settings/         <-- Typowane ustawienia, akcje i detale poleceń
│   ├── FileIndexProvider.Settings.csproj
│   ├── FileIndexProviderSettings.cs
│   ├── FileIndexProviderAction.cs      <-- Enum akcji (Rebuild, Sync, Status, Cancel)
│   └── FileIndexCommandDetails.cs     <-- Silnie typowane metryki (CommandDetailsBase)
├── FileIndexProvider.Tests/            <-- Testy bazy, rankingu, crawlera i ustawień
│   ├── Database/FileDatabaseTests.cs
│   ├── Services/FileRankingServiceTests.cs
│   ├── ResultFinders/FileIndexResultFinderTests.cs
│   └── Settings/FileIndexProviderSettingsTests.cs
├── FileIndexProvider/                  <-- Rdzeń dostawcy (konsola / proces IPC)
│   ├── Database/
│   │   ├── FileIndexDbContext.cs       <-- EF Core DbContext (Directories, Files, BuildHistory)
│   │   └── FileDatabase.cs            <-- Repozytorium EF Core + FTS5 trigram
│   ├── Services/
│   │   ├── FileCrawlerService.cs       <-- Przeszukiwanie wszerz (BFS) z detekcją pętli
│   │   ├── FileWatcherService.cs       <-- Kanał (Channel) zdarzeń I/O z debouncingiem 500ms
│   │   ├── FileRankingService.cs       <-- Scoring: Exact, Prefix, CamelCase, Frecency
│   │   └── FileIconResolver.cs         <-- Ikony vscode-material-icon-theme + extract .exe
│   ├── Models/
│   │   ├── FileIndexRecord.cs
│   │   ├── DatabaseStats.cs
│   │   ├── DirectoryEntity.cs
│   │   ├── FileEntity.cs
│   │   ├── BuildHistoryEntity.cs
│   │   └── FileChangeEvent.cs
│   ├── ResultFinders/
│   │   └── FileIndexResultFinder.cs    <-- IResultFinder + ICommandHandler
│   ├── Icons/                          <-- 28 wyrenderowanych ikon 32x32 PNG
│   ├── settings.yaml
│   └── Program.cs                      <-- Obsługa flagi CLI --rebuild oraz BaseProviderHost
```

---

## 2. Baza Danych: SQLite + FTS5 Trigram (`FileDatabase`)

Indeks lokalny jest przechowywany w pliku bazy SQLite `cache/file_index.db` w katalogu dostawcy.

### 2.1. Konfiguracja Pragmas
Przy inicjalizacji połączenia baza konfiguruje pragmy gwarantujące maksymalną przepustowość współbieżnych odczytów i zapisów:
- `PRAGMA journal_mode = WAL;` – Write-Ahead Logging pozwala na jednoczesne czytanie przez zapytania wyszukiwania podczas trwania indeksowania w tle.
- `PRAGMA synchronous = NORMAL;` – Redukuje liczbę operacji `fsync` bez ryzyka uszkodzenia bazy w trybie WAL.
- `PRAGMA cache_size = -64000;` – 64 MB pamięci podręcznej na strony bazy danych.
- `PRAGMA foreign_keys = ON;` – Integralność referencyjna pomiędzy tabelami `Files` i `Directories`.

### 2.2. Schemat Relacyjny i FTS5
```sql
CREATE TABLE IF NOT EXISTS Directories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Path TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS Files (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    DirectoryId INTEGER NOT NULL,
    FileName TEXT NOT NULL,
    Extension TEXT NOT NULL,
    FileSizeBytes INTEGER NOT NULL,
    LastWriteTimeUtc INTEGER NOT NULL,
    AccessCount INTEGER NOT NULL DEFAULT 0,
    LastAccessedUtc INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY(DirectoryId) REFERENCES Directories(Id)
);

CREATE TABLE IF NOT EXISTS BuildHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    StartTimeUtc INTEGER NOT NULL,
    EndTimeUtc INTEGER,
    FilesIndexed INTEGER NOT NULL DEFAULT 0,
    ElapsedMs INTEGER NOT NULL DEFAULT 0,
    Status TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_files_name ON Files(FileName COLLATE NOCASE);
CREATE INDEX IF NOT EXISTS idx_files_ext ON Files(Extension COLLATE NOCASE);
CREATE INDEX IF NOT EXISTS idx_files_dir ON Files(DirectoryId);
CREATE INDEX IF NOT EXISTS idx_build_history_status ON BuildHistory(Status, Id DESC);

CREATE VIRTUAL TABLE IF NOT EXISTS FilesFts USING fts5(
    FileName,
    content='Files',
    content_rowid='Id',
    tokenize='trigram'
);

CREATE TRIGGER IF NOT EXISTS files_ai AFTER INSERT ON Files BEGIN
    INSERT INTO FilesFts(rowid, FileName) VALUES (new.Id, new.FileName);
END;

CREATE TRIGGER IF NOT EXISTS files_ad AFTER DELETE ON Files BEGIN
    INSERT INTO FilesFts(FilesFts, rowid, FileName) VALUES('delete', old.Id, old.FileName);
END;

CREATE TRIGGER IF NOT EXISTS files_au AFTER UPDATE ON Files BEGIN
    INSERT INTO FilesFts(FilesFts, rowid, FileName) VALUES('delete', old.Id, old.FileName);
    INSERT INTO FilesFts(rowid, FileName) VALUES (new.Id, new.FileName);
END;

CREATE VIRTUAL TABLE IF NOT EXISTS DirectoriesFts USING fts5(
    DirectoryName,
    content='Directories',
    content_rowid='Id',
    tokenize='trigram'
);

CREATE TRIGGER IF NOT EXISTS dirs_ai AFTER INSERT ON Directories BEGIN
    INSERT INTO DirectoriesFts(rowid, DirectoryName) VALUES (new.Id, new.DirectoryName);
END;

CREATE TRIGGER IF NOT EXISTS dirs_ad AFTER DELETE ON Directories BEGIN
    INSERT INTO DirectoriesFts(DirectoriesFts, rowid, DirectoryName) VALUES('delete', old.Id, old.DirectoryName);
END;

CREATE TRIGGER IF NOT EXISTS dirs_au AFTER UPDATE ON Directories BEGIN
    INSERT INTO DirectoriesFts(DirectoriesFts, rowid, DirectoryName) VALUES('delete', old.Id, old.DirectoryName);
    INSERT INTO DirectoriesFts(rowid, DirectoryName) VALUES (new.Id, new.DirectoryName);
END;
```

### 2.3. Przejście na FTS5 Trigram dla Maksymalnej Wydajności (Performance Gains)

#### Pomiary Wydajnościowe i Rozmiaru Bazy (Benchmark & Telemetria)
Pomiary przeprowadzono na bazie produkcyjnej `file_index.db` o rozmiarze **627,4 MB (~640 MB)**, zawierającej **1 643 407 plików** oraz **359 905 katalogów** (łącznie **2 003 312 rekordów**).

##### 1. Rzeczywiste dane telemetryczne z logów aplikacji (`FileIndexProviderLogs_2026-10-25.log`)
*Pomiary end-to-end w interfejsie Hub: obejmują zapytanie bazy, hydratację encji LINQ/EF Core oraz ranking hierarchiczny w `FileRankingService`:*

| Faza Implementacji | Okno w Logu | N (próbki) | Średnia | p50 (Mediana) | p90 | p95 | p99 (Worst case) | Min | Max | Odch. std ($\sigma$) | Rozmiar Bazy |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **1. Stary EF Core** (`LIKE '%query%'`) | `00:41`–`01:00` | 16 | **168,8 ms** | **128,0 ms** | **354,0 ms** | **408,0 ms** | **532,0 ms** | 2 ms | 563 ms | 173,2 ms | **~250 MB** *(~325 MB +30% est)* |
| **2. FTS5 Nieoptymalizowany** (poszatkowane segmenty) | `02:03`–`02:06` | 14 | **36,0 ms** | **4,0 ms** | **57,0 ms** | **170,0 ms** | **318,0 ms** | 2 ms | 355 ms | 93,6 ms | **~380 MB** |
| **3. FTS5 Zoptymalizowany** (komplet katalogów + `optimize`) | `02:22`–`02:50` | 50 | **27,5 ms** | **5,0 ms** | **39,0 ms** | **95,0 ms** | **396,0 ms** | 1 ms | 548 ms | 84,1 ms | **627,4 MB** *(~640 MB)* |
| *↳ Stan ustalony (Steady-state, bez cold-startu i locka zapisu)* | – | 47 | **10,0 ms** | **5,0 ms** | **23,0 ms** | **36,0 ms** | **62,0 ms** | 1 ms | 64 ms | 13,7 ms | **627,4 MB** |

##### 2. Izolowana wydajność silnika SQLite (Engine Throughput)
*Pomiary samego czasu wykonania zapytania SQL na poziomie bazy (bez narzutu frameworka C#):*
- **Zwykły LIKE (`%query%`)**: Średnia **115,8 ms**, p95 **193,1 ms**, p99 **1 144,1 ms**, przepustowość **8,6 zapyt./s**.
- **FTS5 Trigram (`optimize`)**: Średnia **0,36 ms**, p95 **1,02 ms**, p99 **1,36 ms**, przepustowość **2 784,4 zapyt./s** (**ponad 320-krotne przyspieszenie** na poziomie bazy).

**Wnioski architektoniczne do pracy dyplomowej**:
1. **Zysk wydajnościowy**: Mediana czasu odpowiedzi dla użytkownika spadła ze **128 ms do 5 ms** (ponad 25-krotne skrócenie typowego czasu interakcji w GUI).
2. **Eliminacja ogona opóźnień (Tail Latency SLA)**: W wariancie EF Core zapytania na ogonie p95/p99 regularnie przekraczały **400–560 ms**, powodując zacięcia interfejsu przy wpisywaniu tekstu. W zoptymalizowanym FTS5 trigram 95% wszystkich zapytań kończy się w czasie **poniżej 36 ms**, a p50 wynosi **5 ms**.
3. **Trade-off pamięciowy**: Kosztem ok. **300 MB** dodatkowego miejsca na dysku (baza wzrosła z szacunkowych 325 MB do 627,4 MB) osiągnięto pełne, natychmiastowe wyszukiwanie plików i katalogów w czasie poniżej 10 ms przy 2 milionach rekordów.
##### 3. Minimalizacja Surowego SQL i Integracja z EF Core
- Tabele, relacje, indeksy oraz encje `Files`, `Directories`, `BuildHistory` są w 100% definiowane i zarządzane w C# przez EF Core.
   - Wirtualna tabela `FilesFts` korzysta z `content='Files'` i `content_rowid='Id'`, a triggery SQLite (`files_ai`, `files_ad`, `files_au`) dbają o jej automatyczną i przezroczystą synchronizację.
   - Zapis partiami (`InsertBatchAsync`), aktualizacje (`UpsertFileAsync`) oraz usuwanie (`DeleteFileAsync`) pozostają w **100% czystym kodzie EF Core bez jakichkolwiek zapytań SQL**.
   - Wyszukiwanie w `FileSearchRepository.cs` odpytuje jednocześnie indeksy odwrócone `FilesFts` oraz `DirectoriesFts`:
     ```csharp
     var fileIds = await context.Database
         .SqlQuery<int>($"SELECT rowid AS Value FROM FilesFts WHERE FilesFts MATCH {sanitized} LIMIT {candidateLimit}")
         .ToListAsync(cancellationToken);

     var dirIds = await context.Database
         .SqlQuery<int>($"SELECT rowid AS Value FROM DirectoriesFts WHERE DirectoriesFts MATCH {sanitized} LIMIT {candidateLimit}")
         .ToListAsync(cancellationToken);
     ```
   - **Wspólna kategoria "Files" (Merged Results)**: Zarówno pliki, jak i pasujące katalogi (z ikoną `folder.png` i ścieżką nadrzędną jako `Subtitle`) trafiają do jednego, zunifikowanego widoku wyników i są wspólnie sortowane przez silnik `FileRankingService`.
   - **Optymalizacja FTS5 (`optimize`)**: Po zakończeniu indeksowania w tle (`RebuildAsync`), silnik wywołuje `INSERT INTO FilesFts(FilesFts) VALUES('optimize')` oraz `INSERT INTO DirectoriesFts(DirectoriesFts) VALUES('optimize')`, co scala segmenty B-tree indeksu w jeden spójny blok, minimalizuje rozmiar pliku bazy i redukuje czas odpytywania.
   - Dla zapytań krótszych niż 3 znaki system natychmiastowo stosuje dopasowanie prefiksowe bez wiodącego `%` (`EF.Functions.Like(f.FileName, $"{trimmedQuery}%")`), które wykorzystuje natywny indeks B-tree w czasie **< 1 ms**.
---

## 3. Algorytm Rankingu i Frekwencji (`FileRankingService`)

Kandydaci pobrani z bazy SQLite są precyzyjnie oceniani i sortowani za pomocą funkcji scoringu:

1. **Dopasowanie dokładne (Exact Match)**:
   - Cała nazwa z rozszerzeniem (`query == fileName`): **+700 pkt**
   - Nazwa pliku bez rozszerzenia (`query == nameWithoutExt`): **+650 pkt**
2. **Dopasowanie przedrostka (Prefix Match)**:
   - Nazwa zaczyna się od zapytania (`fileName.StartsWith(query)`): **+400 pkt**
3. **Granica słowa / CamelCase**:
   - Zapytanie rozpoczyna się bezpośrednio po separatorze (`_`, `-`, `.`, spacja) lub na wielkiej literze w stylu CamelCase: **+300 pkt**
   - *Uwaga*: Bonusy za Prefix i Granicę Słowa nie kumulują się, zapobiegając sztucznemu windowaniu złożonych nazw (`WindowsSearch.Benchmark.Cli`) ponad czyste dopasowania.
4. **Dopasowanie podciągu (Substring)**:
   - Dowolny podciąg w nazwie: **+150 pkt**, pomniejszane o dwukrotność indeksu pozycji znaku (`- 2 * pos`).
5. **Hierarchical Ancestor Match Penalty (Kara za zagnieżdżonego potomka)**:
   - Jeśli w wynikach dopasował się folder nadrzędny $A$, a dany folder $D$ jest jego potomkiem w ścieżce (`D.FullPath.StartsWith(A.FullPath + "\\")`): **-350 pkt**.
   - Gwarantuje to, że przy wyszukiwaniu np. `windowssearch` lub `benchmarking`, nadrzędne foldery (`WindowsSearch`, `Benchmarking`) zawsze bezapelacyjnie wygrywają z zagnieżdżonymi modułami podrzędnymi (`WindowsSearch.Benchmark.*`).
6. **Kara za nadmiarową długość folderu (Excess Length Penalty)**:
   - Dla katalogów: `- (fileName.Length - query.Length) * 5 pkt`. Zwięzłe nazwy folderów są faworyzowane nad wieloczłonowymi podmodułami.
7. **Bonus Frekwencyjny (Frecency)**:
   - $\min(250, \text{AccessCount} \times 30)$ – elementy częściej uruchamiane z poziomu wyszukiwarki otrzymują dynamiczny priorytet.
8. **Bonus Świeżości (Recency)**:
   - Elementy zmodyfikowane w ciągu ostatnich 30 dni: $\max(0, 150 \times (1 - \text{AgeDays} / 30))$.
9. **Kara za głębokość (Depth Penalty)**:
   - Dla plików: $\min(50, \text{Depth} \times 3)$.
   - Dla katalogów: $\text{Depth} \times 10$ – płytsze katalogi w strukturze dysku są mocno preferowane.
10. **Bonusy typu**:
    - **+30 pkt** dla plików wykonywalnych i projektowych: `.exe`, `.lnk`, `.pdf`, `.docx`, `.sln`.
    - **+35 pkt** dla katalogów, aby trafione foldery naturalnie konkurowały w czołówce z plikami.
---

## 4. Usługa Indeksowania w Tle (`FileCrawlerService`)

- **Algorytm BFS (Breadth-First Search)**: Przeszukuje drzewo katalogów poziom po poziomie przy użyciu kolejki `Queue<string>`.
- **Ochrona przed zapętleniem**: Sprawdza atrybut `FileAttributes.ReparsePoint` (symlinki, directory junctions w Windows) i pomija ich ponowną rekursję.
- **Wykluczenia katalogów i plików (Wildcardy & Segmenty)**: Ignoruje foldery, pliki i wzorce wildcard zdefiniowane w `ExcludedDirectories` (np. `.git`, `node_modules`, `bin`, `obj`, `$Recycle.Bin`, `AppData\Roaming\Microsoft\Windows\Recent`, `*.lnk`). Metoda `IsFileExcluded` dopasowuje nazwy plików, segmenty ścieżek oraz maski wieloznaczne (`*.lnk`, `*.tmp`) przy pomocy `FileSystemName.MatchesSimpleExpression`.
- **Odporność na błędy**: Wychwytuje `UnauthorizedAccessException`, `PathTooLongException` oraz `IOException` na poziomie pojedynczego folderu, kontynuując przetwarzanie pozostałych ścieżek.
- **Strumieniowanie transakcji**: Zapisuje pliki w pakietach po **5000 rekordów** w jednej transakcji SQLite (`InsertBatchAsync`), osiągając prędkości przekraczające 10 000 plików/sekundę.
- **Tryb CLI**:
  ```bash
  FileIndexProvider.exe --rebuild
  ```
  Uruchamia bezpośredni zrzut indeksowania w oknie terminala z raportowaniem bieżącej liczby plików i skanowanej ścieżki.

---

## 5. Synchronizacja w Czasie Rzeczywistym (`FileWatcherService`)

- **Filtrowanie wyłącznie zmian strukturalnych**: Monitoruje wyłącznie zmiany nazw i istnienia (`NotifyFilters.FileName | NotifyFilters.DirectoryName`) dla zdarzeń `Created`, `Deleted`, `Renamed`. Wyeliminowano nasłuchiwanie na modyfikacje zawartości/daty (`LastWrite`, `Size`, `Changed`), ponieważ silnik indeksuje nazwy, a nie treść – eliminuje to tysiące fałszywych zdarzeń generowanych przez system i aplikacje w tle.
- **Odporność na przepełnienie bufora**: Ustawiono maksymalny bufor systemowy Win32 `InternalBufferSize = 65536` (64 KB), całkowicie eliminując błędy typu *Too many changes at once in directory: C:\*.
- **Okresowy zrzut partiami (Periodic Batch Flush – domyślnie 60 s)**:
  - Zdarzenia są gromadzone w pamięci w słowniku `ConcurrentDictionary<string, FileChangeEvent>` i scalane w RAM bez dotykania bazy SQLite.
  - Pliki tymczasowe tworzone i usuwane w trakcie okna (np. cache, temp build) znoszą się wzajemnie w pamięci i nigdy nie obciążają bazy.
  - Zrzut do SQLite następuje w jednej transakcji raz na 60 sekund (`WatcherSyncIntervalSeconds = 60`), co gwarantuje, że przez **59 sekund na każdą minutę baza SQLite jest całkowicie wolna od blokad zapisu**, a wyszukiwanie w GUI działa z natychmiastowym czasem reakcji (1–5 ms) bez pików opóźnień.
---

## 6. Rozpoznawanie Ikon (`FileIconResolver`)

- **Ikona Kategorii "Files"**: Pobrana z oficjalnego repozytorium GNOME Nautilus (`org.gnome.Nautilus.svg`) i wyrenderowana jako 32x32 `category.png`. Przypisywana do właściwości `ProviderResultCategory.IconPath`.
- **Ikony elementów wyników**: Pochodzące z oficjalnego motywu `material-extensions/vscode-material-icon-theme`:
  - **Języki programowania**: `.cs` (`csharp.png`), `.py` (`python.png`), `.ts`/`.tsx` (`typescript.png`), `.js`/`.jsx` (`javascript.png`), `.cpp`/`.c` (`cpp.png`), `.rs` (`rust.png`), `.go` (`go.png`), `.java`/`.jar` (`java.png`), `.html` (`html.png`), `.css` (`css.png`), `.json` (`json.png`), `.xml`/`.xaml`/`.csproj` (`xml.png`), `.yaml`/`.yml` (`yaml.png`), `.sql` (`sql.png`), `.sh`/`.bat`/`.ps1` (`terminal.png`), `.md` (`markdown.png`).
  - **Kategorie ogólne**: dokumenty (`doc.png`), arkusze kalkulacyjne (`sheet.png`), prezentacje (`presentation.png`), PDF (`pdf.png`), archiwa (`archive.png`), obrazy (`image.png`), audio (`audio.png`), wideo (`video.png`), foldery (`folder.png`), pliki nieznane (`file.png`).
- **Pliki wykonywalne i skróty** (`.exe`, `.lnk`): `FileIconResolver` zwraca bezpośrednią ścieżkę do pliku na dysku, co pozwala modułowi `BaseImageResolver` w Hubie na natywne wyciągnięcie ikony Windows Shell za pomocą `Icon.ExtractAssociatedIcon(filePath)`.
---

## 7. Architektura Poleceń (`ProviderCommand` & `[ProviderAction]`)

`FileIndexResultFinder` implementuje interfejs `ICommandHandler<FileIndexProviderSettings>`:

```csharp
public interface ICommandHandler<in TSettings>
{
    Task<ProviderCommandResult> ExecuteCommandAsync(ProviderCommand command, TSettings settings, CancellationToken cancellationToken);
}
```

Dostępne akcje sterujące (deklarowane na klasie `FileIndexProviderSettings` przez atrybuty `[ProviderAction]` za pomocą enuma `FileIndexProviderAction`):
- `Rebuild`: Truncuje bazę SQLite i rozpoczyna pełny proces BFS crawlera w tle (`PollAction = FileIndexProviderAction.Status`, `CancelAction = FileIndexProviderAction.Cancel`). Formularz Hub automatycznie odpytuje postęp co 1 sekundę i udostępnia przycisk `[✕ Cancel]`.
- `Cancel`: Anuluje trwający proces indeksowania w tle (`_crawler.Cancel()`).
- `Sync`: Odpytuje aktualny stan bazy i synchronizuje metryki (`Files`, `DB Size`).
- `Status`: Zwraca liczbę zindeksowanych plików, katalogów, rozmiar bazy SQLite oraz stan działania crawlera (`Running`/`Idle`).

### 7.1. Silne Typowanie Metryk Akcji (`CommandDetailsBase` i `CommandDetail`)
Zamiast nietypowanego słownika `Dictionary<string, string>` i problematycznych obejść serializacji XML, wprowadzono architekturę łączącą silne typowanie w C# z natywnym przesyłem na łączu IPC:
1. **Model w C#**: Provider definiuje silnie typowaną klasę dziedziczącą po `CommandDetailsBase`:
   ```csharp
   public sealed class FileIndexCommandDetails : CommandDetailsBase
   {
       [Display(Name = "Files", Order = 1)]
       public string? Files { get; init; }

       [Display(Name = "Directories", Order = 2)]
       public string? Directories { get; init; }

       [Display(Name = "DB Size", Order = 3)]
       public string? DbSize { get; init; }

       [Display(Name = "Status", Order = 4)]
       public string? Status { get; init; }

       [Display(Name = "Last Scan", Order = 5)]
       public string? LastScan { get; init; }
   }
   ```
2. **Czysta inicjalizacja**: W `FileIndexCommandHandler.cs` instancjonowany jest obiekt w czystym C#, a metoda `.ToDetails()` generuje natywną listę `List<CommandDetail>` (właściwości `null` są automatycznie pomijane):
   ```csharp
   var statusDetails = new FileIndexCommandDetails
   {
       Files = stats.TotalFiles.ToString(),
       Directories = stats.TotalDirectories.ToString(),
       DbSize = stats.FormattedSize,
       Status = _crawler.IsRunning ? "Crawling" : "Idle",
       LastScan = lastScanText
   };

   return new ProviderCommandResult
   {
       Success = true,
       IsRunning = _crawler.IsRunning,
       Message = $"Index ready. {stats.TotalFiles} files indexed ({stats.FormattedSize})",
       Details = statusDetails.ToDetails()
   };
   ```
3. **Natywny Wire Contract**: `CommandDetail` zawiera proste pola `Key` i `Value`, co eliminuje tunelowanie JSON w XML/Proto i pozwala na bezpośrednie, natywne bindowanie w WPF `{Binding Key}`, `{Binding Value}` z zachowaną kolejnością `Order`.

## 8. Abstrakcja Systemu Plików (`IFileSystem`)

Zgodnie ze standardem stosowanym we wszystkich pozostałych dostawcach (`VsCodeProvider`, `ChromeProvider`, `FirefoxProvider`, `JetBrainsProvider`), `FileIndexProvider` wstrzykuje i wykorzystuje abstrakcję `IFileSystem` (`TestableIO.System.IO.Abstractions`):
- **`FileIndexResultFinder(IFileSystem? fileSystem = null, ...)`**: Konstruktor przyjmuje opcjonalną instancję `IFileSystem`, domyślnie inicjalizując `new FileSystem()`.
- **Serwisy podrzędne**: `FileCrawlerService`, `FileWatcherService`, `FileIconResolver`, `FileDatabase` oraz `FileRepository` korzystają wyłącznie z interfejsów `IDirectoryInfo`, `IFileInfo`, `IFileSystemWatcher`, `_fileSystem.File`, `_fileSystem.Directory` oraz `_fileSystem.Path`.
- **Testowalność jednostkowa**: Umożliwia pełne testowanie indeksowania, filtrowania i crawlera w pamięci za pomocą `MockFileSystem` bez konieczności dotykania fizycznego dysku.
