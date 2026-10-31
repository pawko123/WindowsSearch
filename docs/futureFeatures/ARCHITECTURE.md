# Architektura Techniczna Huba: Silnik, Wtyczki i Platforma

> **Status**: Dokument Techniczny (Szczegółowa specyfikacja architektoniczna, modele danych, kod i platforma uruchomieniowa)

Dokument zawiera kompletną specyfikację techniczną przekształcenia aplikacji Hub w nowoczesny, lekki, wieloplatformowy launcher.

---

## 1. Dekompozycja Architektury (Rozbicie Monolitu)

Obecny projekt `Hub.csproj` skupia w jednym miejscu widoki WPF, komunikację IPC, indeksowanie aplikacji i orkiestrację zapytań. Dzielimy go na **3 ściśle odseparowane warstwy**:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Hub.App (Powłoka UI)                            │  <- Czysty C# (zero XAML)
│  • Avalonia.Markup.Declarative (Styl Jetpack Compose)                  │     Windows, Linux, macOS
│  • Glass UI & Raycast Aesthetic (#CC000000, zaokrąglenie 14px)         │     Natywny TrayIcon, Global Hotkey
│  • Bindowanie modeli DTO z Core do widoków                             │     Wykonywanie akcji systemowych
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │ referencjonuje
┌──────────────────────────────────▼─────────────────────────────────────┐
│                      Hub.Core (Silnik Aplikacji)                       │  <- Czysty .NET 9
│  • SearchOrchestrator: Potok zapytań, Scope Pill, tagi (#tag)          │     ZERO powiązań z WPF / XAML
│  • ExtensionManager: Dynamiczny ALC (isCollectible)                   │     W 100% testowalny w xUnit!
│  • Baza danych SQLite: Tabela LaunchHistory & Algorytm Frecency        │
│  • Smart Intent Sniffing: Auto-analiza ZPL, Base64, JSON, Kolorów      │
│  • Lekki Profiler Czasów Odpowiedzi (Stopwatch.GetElapsedTime)         │
└──────────────┬───────────────────────────┬─────────────┘
               │ referencjonuje            │ ładuje dynamicznie w locie
┌──────────────▼─────────────┐   ┌─────────▼──────────────────────────┐
│     Hub.Extensions.Sdk     │   │     Katalog ./Extensions/<id>/     │
│  • IHubPlugin              │   │  ├── Math/Hub.Extensions.Math.dll  │
│  • SearchResultItem        │◄──┤  ├── Spotify/SpotifyPlugin.dll     │
│  • ResultAction            │   │  └── Wtyczki użytkownika (ZIP)     │
└────────────────────────────┘   └────────────────────────────────────┘
```

### 1.1. Usunięcie podsystemu benchmarków (Oczyszczenie długu magisterskiego)
W kodzie produkcyjnym podsystem benchmarków stanowi zbędny narzut (deadweight). Zostaje całkowicie usunięty:
* **Usuwane projekty**: Cała solucja `WindowsSearch.Benchmark.slnx` oraz projekty `Harness`, `Cli`, `Reporting`, `Core`.
* **Usuwane usługi z Huba**: `BenchmarkControlService.cs` (kanał sterowania z zewnątrz), metody `ExecuteBenchmarkSearchAsync`, `StressTestImageResolverAsync`, zdarzenie `SearchCompleted` oraz zbieranie raportów telemetrycznych w `MainViewModel.cs`.
* **Usuwane modele z `WindowsSearch.Common`**: `BenchmarkControlCommand`, `SearchBenchmarkReport`, `ProviderBenchmarkMetrics`.
* **Czym zastępujemy**: Projektem **`Hub.Core.Tests`** zawierającym deterministyczne testy xUnit sprawdzające logikę wyszukiwania, debounce, izolację wtyczek i odporność na błędy.

### 1.2. Uniwersalne modele DTO (SDK)
Modele nie zależą od technologii UI (brak `INotifyPropertyChanged` czy `ImageSource` w warstwie silnika):

```csharp
namespace Hub.Extensions.Sdk.Models;

public enum ActionKind
{
    CopyToClipboard,   // Kopiuje tekst/obiekt do schowka
    ReplaceQuery,        // Wstawia wartość do pola wyszukiwania (do kontynuacji obliczeń)
    LaunchProcess,       // Uruchamia program / plik
    OpenUrl,             // Otwiera stronę w domyślnej przeglądarce
    Custom               // Dedykowana akcja wtyczki
}

public sealed class ResultAction
{
    public required string Title { get; init; }          // np. "Kopiuj wynik"
    public string? ShortcutHint { get; init; }          // np. "Enter", "Tab"
    public ActionKind Kind { get; init; }
    public required string Payload { get; init; }       // Wartość do schowka, URL, ścieżka exe
}

public sealed class SearchResultItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public IconDescriptor? Icon { get; init; }
    public string? Category { get; init; }               // "Tools", "Applications", "Files"
    public required ResultAction PrimaryAction { get; init; }
    public IReadOnlyList<ResultAction> SecondaryActions { get; init; } = [];
}
```

---

## 2. Unifikacja Wtyczek w Model In-Process (`IHubPlugin`)

Zastępujemy ciężkie procesy zewnętrzne `.exe` komunikujące się po IPC bibliotekami DLL ładowanymi w procesie Huba.

### 2.1. Korzyści z unifikacji in-process:
1. **Drastyczny spadek zużycia RAM**: Zamiast 5–6 procesów .NET CLR zjadających łącznie **~300 MB**, pojedynczy proces Huba ze wszystkimi wtyczkami zajmuje zaledwie **~25 MB**.
2. **Sub-milisekundowa latencja**: Wywołanie metody w pamięci (< 0.1 ms) zamiast serializacji i przepychania bajtów przez potoki NamedPipe (15–30 ms).
3. **Pojedynczy projekt**: Twórca wtyczki tworzy jeden projekt C# zamiast dwóch (`.Settings.csproj` + `.csproj`).

### 2.2. Interfejs wtyczki i hierarchia komend:

```csharp
namespace Hub.Extensions.Sdk.Abstractions;

public enum CommandKind
{
    DirectAnswer,      // Błyskawiczna ewaluacja w locie podczas pisania (Kalkulator, Czas, Waluty)
    CommandAction,     // Cicha akcja w tle po Enterze z powiadomieniem HUD (Next Song, Mute, Kosz)
    InteractiveTool,   // Pełnowymiarowy widok na stosie nawigacji (Tłumacz, Base64/ZPL, Mini-Player)
    SearchProvider     // Asynchroniczne przeszukiwanie strumieniowe (Pliki, Spotify, Zakładki)
}

public sealed class PluginCommandDescriptor
{
    public required string Id { get; init; }                 // np. "dev.zpl-viewer"
    public required string Name { get; init; }               // np. "ZPL & Base64 Inspector"
    public string? Description { get; init; }
    public required CommandKind Kind { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];   // np. ["Dev", "Logistics", "Tools"]
    public IReadOnlyList<string> TriggerKeywords { get; init; } = []; // np. ["zpl", "label", "base64"]
    public bool IsEnabled { get; set; } = true;
}

public interface IHubPlugin
{
    PluginMetadata Metadata { get; }
    IReadOnlyList<PluginCommandDescriptor> Commands { get; }

    // Opcjonalna ewaluacja w locie (Direct Answer)
    bool CanHandleDirectAnswer(string query) => false;
    InstantActionResult? EvaluateDirectAnswer(string query) => null;

    // Opcjonalne przeszukiwanie strumieniowe (Search Provider)
    IAsyncEnumerable<SearchResultItem> SearchAsync(string query, SearchContext context, CancellationToken ct);
}
```

### 2.3. Dynamiczne formularze opcji wtyczek (`IConfigurablePlugin`):
Twórca wtyczki nie pisze ani jednej linijki UI dla ustawień. Definiuje czystą klasę z atrybutami:

```csharp
public interface IConfigurablePlugin<TSettings> : IHubPlugin where TSettings : class, new()
{
    TSettings Settings { get; set; }
    void OnSettingsSaved();
}

// Przykład klasy opcji wtyczki:
public sealed class WeatherSettings
{
    [Display(Name = "Domyślne miasto", Description = "Miasto używane przy braku parametru")]
    public string DefaultCity { get; set; } = "Warszawa";

    [Display(Name = "Jednostka temperatury")]
    public TemperatureUnit Unit { get; set; } = TemperatureUnit.Celsius;

    [Range(1, 60)]
    [Display(Name = "Częstotliwość odświeżania (minuty)")]
    public int RefreshMinutes { get; set; } = 15;

    [PluginAction("Wyczyść pamięć podręczną pogody")]
    public void ClearCache() { /* wykonuje się bezpośrednio w procesie */ }
}
```
Silnik refleksji Huba (`BuildSettingsForm`) automatycznie generuje z tych atrybutów szklane karty pól tekstowych, list rozwijanych (ComboBox), przełączników i przycisków akcji.

### 2.4. Opcjonalne własne widoki GUI (`ICustomSettingsViewPlugin`):
Dla zaawansowanych wtyczek wymagających własnego UI w Ustawieniach (np. selektor kolorów, interaktywna mapa):
```csharp
public interface ICustomSettingsViewPlugin
{
    Control CreateSettingsView(); // Zwraca kontrolkę w deklaratywnym C#
}
```

---

## 3. Dynamiczne Ładowanie i Dystrybucja z Plików ZIP

Wtyczki są dystrybuowane jako zwykłe archiwa `.zip` zawierające plik `manifest.yaml`:

```yaml
id: "hub.extension.base64-zpl"
name: "Base64 & ZPL Label Inspector"
version: "1.0.0"
author: "Pawel"
entryPoint: "Hub.Extensions.ZplInspector.dll"
commands:
  - id: "zpl-viewer"
    name: "Podgląd Etykiet ZPL i Base64"
    kind: "InteractiveTool"
    tags: ["Dev", "Logistics"]
    triggers: ["zpl", "label", "base64"]
```

### 3.1. Instalacja metodą Drag & Drop w oknie Ustawień:
Użytkownik przeciąga plik `.zip` na okno Ustawień. Hub:
1. Odczytuje w pamięci RAM plik `manifest.yaml` z archiwum (`ZipArchive`).
2. Przeprowadza walidację bezpieczeństwa i **chroni przed atakiem Zip Slip**:
   ```csharp
   public static void ExtractSafe(ZipArchive archive, string destinationDir)
   {
       var fullDestPath = Path.GetFullPath(destinationDir);
       if (!fullDestPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
           fullDestPath += Path.DirectorySeparatorChar;

       foreach (var entry in archive.Entries)
       {
           var targetPath = Path.GetFullPath(Path.Combine(fullDestPath, entry.FullName));
           if (!targetPath.StartsWith(fullDestPath, StringComparison.OrdinalIgnoreCase))
           {
               throw new InvalidOperationException($"Wykryto próbę ataku Zip Slip: {entry.FullName}");
           }

           if (string.IsNullOrEmpty(entry.Name))
           {
               Directory.CreateDirectory(targetPath);
               continue;
           }

           Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
           entry.ExtractToFile(targetPath, overwrite: true);
       }
   }
   ```
3. Wypakowuje zawartość do katalogu `./Extensions/<id>/`.
4. Ładuje bibliotekę przez **`AssemblyLoadContext` z flagą `isCollectible: true`**. Pozwala to na pełne wyładowanie biblioteki z pamięci RAM i aktualizację wtyczki bez konieczności restartowania Huba.

---

## 4. Tworzenie Wtyczek Poza Repozytorium (SDK & MSBuild)

Zewnętrzny programista nie musi klonować kodu Huba.

1. **Instalacja SDK**:
   ```bash
   dotnet new install Hub.Templates
   dotnet new hub-extension -n MyCustomPlugin
   ```
2. **Plik `.csproj` wtyczki z automatycznym pakowaniem MSBuild**:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net9.0</TargetFramework>
       <ImplicitUsings>enable</ImplicitUsings>
       <Nullable>enable</Nullable>
       <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
     </PropertyGroup>

     <ItemGroup>
       <PackageReference Include="Hub.Extensions.Sdk" Version="1.0.0" />
       <None Include="manifest.yaml" CopyToOutputDirectory="PreserveNewest" />
     </ItemGroup>

     <!-- Automatyczne tworzenie pliku .zip przy kompilacji Release -->
     <Target Name="PackageToZip" AfterTargets="Build" Condition="'$(Configuration)' == 'Release'">
       <ZipDirectory SourceDirectory="$(TargetDir)" DestinationFile="$(TargetDir)..\$(MSBuildProjectName).zip" Overwrite="true" />
       <Message Importance="High" Text="--> Gotowa paczka rozszerzenia: $(TargetDir)..\$(MSBuildProjectName).zip" />
     </Target>
   </Project>
   ```
3. **Tryb Deweloperski (Live Hot-Reload)**:
   W Ustawieniach programista wskazuje ścieżkę do `bin/Debug/net9.0/` swojego projektu. `ExtensionManager` zakłada `FileSystemWatcher` na plik `.dll` i przeładowuje wtyczkę w pamięci w ułamku sekundy po każdym kliknięciu "Build" w IDE.

---

## 5. Deklaratywne UI w C# (Styl Jetpack Compose / Flutter)

**Całkowita likwidacja plików XML/XAML**: Cały interfejs piszemy w czystym C# .NET 9 za pomocą **`Avalonia.Markup.Declarative`**.

### 5.1. Mapowanie pojęć: Jetpack Compose (Kotlin) ➔ C# Declarative:

| Koncepcja | Jetpack Compose (Kotlin) | C# Declarative (.NET 9) |
| :--- | :--- | :--- |
| **Definicja widoku** | `@Composable fun Card()` | Metoda C# zwracająca `Control` lub klasa komponentu |
| **Modyfikatory stylu** | `Modifier.padding(16.dp).background(...).clip(RoundedCornerShape(14.dp))` | Płynne metody: `.Padding(16).Background(...).CornerRadius(14)` |
| **Warunkowe renderowanie** | `if (state.hasAction) ActionCard()` | **Zwykły C# `if`** lub `.IsVisible(vm.HasAction)` — zero konwerterów XML! |
| **Listy elementów** | `LazyColumn { items(list) { ... } }` | `.Children(vm.Results.Select(r => BuildRow(r)))` |
| **Zarządzanie stanem** | `remember { mutableStateOf() }` | `[ObservableProperty]` (CommunityToolkit.Mvvm) |

### 5.2. Przykłady komponentów w C#:

```csharp
// 1. Karta Szybkiej Akcji (Instant Result Card)
public static Control BuildInstantResultCard(MainViewModel vm) =>
    new Border()
        .CornerRadius(14)
        .Background(Color.Parse("#CC000000"))
        .BorderBrush(Color.Parse("#FFB00000"))
        .BorderThickness(2)
        .Padding(18)
        .IsVisible(vm.HasActiveAction)
        .Child(
            new Grid().Cols("Auto, *, Auto").Children(
                new Border().Width(52).Height(52).CornerRadius(12).Background(Color.Parse("#1AFFFFFF")).Child(new Image().Source(vm.ActiveActionIcon)),
                new StackPanel().Col(1).Margin(16, 0).VerticalAlignment(VerticalAlignment.Center).Children(
                    new TextBlock().Text(vm.ActiveActionTitle).FontSize(22).FontWeight(FontWeight.Bold).Foreground(Brushes.White),
                    new TextBlock().Text(vm.ActiveActionSubtitle).FontSize(12).Foreground(Color.Parse("#99FFFFFF"))
                ),
                new StackPanel().Col(2).Spacing(6).Children(
                    vm.ActiveActionSecondaryList.Select(action =>
                        new Button().Content(action.Title).Background(Color.Parse("#1AFFFFFF")).CornerRadius(6).Padding(10, 4).OnClick(_ => vm.ExecuteAction(action))
                    )
                )
            )
        );

// 2. Karta Ustawień (Glass Setting Card)
public static Control BuildSettingCard(string title, string description, Control control) =>
    new Border()
        .CornerRadius(10)
        .Background(Color.Parse("#0EFFFFFF"))
        .BorderBrush(Color.Parse("#20FFFFFF"))
        .BorderThickness(1)
        .Padding(16, 12)
        .Margin(0, 0, 0, 8)
        .Child(
            new Grid().Cols("*, Auto").Children(
                new StackPanel().VerticalAlignment(VerticalAlignment.Center).Children(
                    new TextBlock().Text(title).FontWeight(FontWeight.SemiBold).Foreground(Brushes.White),
                    new TextBlock().Text(description).FontSize(11).Foreground(Color.Parse("#99FFFFFF"))
                ),
                control.Col(1).VerticalAlignment(VerticalAlignment.Center)
            )
        );

// 3. Wieloplatformowy Zasobnik Systemowy (TrayIcon)
public static TrayIcon BuildAppTrayIcon(Action toggleLauncher, Action openSettings, Action shutdownApp) =>
    new TrayIcon()
        .Icon(new WindowIcon("Assets/hub.ico"))
        .ToolTipText("Hub Launcher")
        .Menu(
            new NativeMenu()
                .Add("Pokaż / Ukryj", toggleLauncher)
                .Add("Ustawienia", openSettings)
                .AddSeparator()
                .Add("Zakończ", shutdownApp)
        )
        .OnClicked(_ => toggleLauncher());
```

---

## 6. Wieloplatformowość (Linux & macOS)

| Komponent | Obecna implementacja (Windows) | Odpowiednik Linux | Odpowiednik macOS |
| :--- | :--- | :--- | :--- |
| **Warstwa UI** | WPF (`PresentationFramework`, DirectX) | Avalonia UI (X11 / Wayland) | Avalonia UI (Cocoa / Metal) |
| **Globalny Skrót** | `user32.dll` (`RegisterHotKey`) | X11 `XGrabKey` / Wayland Portal | `CGEventTap` / `Carbon` HotKey |
| **Indeks Aplikacji** | Skróty `.lnk` (COM `WScript.Shell`) | Pliki `.desktop` (FreeDesktop) | Paczki `.app` (`Info.plist`) |
| **Zasobnik (Tray)** | `H.NotifyIcon.Wpf` | DBus `StatusNotifierItem` (SNI) | Apple Menu Bar `NSStatusItem` |

### 6.1. Globalne skróty klawiszowe (`SharpHook`):
```csharp
public sealed class CrossPlatformHotkeyService
{
    private readonly TaskPoolGlobalHook _hook = new();

    public void Register(ModifierMask modifier, KeyCode key, Action onTriggered)
    {
        _hook.KeyPressed += (s, e) =>
        {
            if (e.Data.KeyCode == key && e.RawEvent.Mask.HasFlag(modifier))
            {
                onTriggered();
            }
        };
        _hook.RunAsync();
    }
}
```

### 6.2. Indeksowanie programów (`IAppIndexProvider`):
* **Linux**: Skanowanie `/usr/share/applications/` i `~/.local/share/applications/` – czytanie zwykłych plików tekstowych `.desktop` (`Name=Firefox`, `Exec=firefox %u`).
* **macOS**: Skanowanie `/Applications/` i odczyt plików XML `Contents/Info.plist` z paczek `.app`.
* **Windows**: Skanowanie menu Start.

---

## 7. Zunifikowany System Ikon Wektorowych & HiDPI (4K)

Likwidujemy wszystkie małe pliki rastrowe PNG na rzecz bezstratnych wektorów SVG i miniatur wysokiej rozdzielczości:

```csharp
namespace Hub.Extensions.Sdk.Models;

public enum IconKind
{
    MaterialSymbol, // Nazwa glifu wektorowego (np. "folder", "calculate", "code")
    SvgPath,        // Geometria ścieżki SVG (np. logotypy z Material Icon Theme)
    HighResImage    // Ścieżka do podglądu pliku/zdjęcia (min. 64x64 px)
}

public sealed class IconDescriptor
{
    public required IconKind Kind { get; init; }
    public required string Value { get; init; }
    public string? AccentColor { get; init; }
}
```

### 7.1. Trzy źródła wektorów:
1. **Google Material Symbols (SVG)**: Akcje systemowe, foldery, kategorie ogólne (`folder`, `calculate`, `schedule`, `bookmark`, `delete`, `settings`).
2. **Material Icon Theme (Philipp Kief, Licencja MIT)**:
   * **Kwestia prawna**: Projekt na GitHubie jest wydany na pełnej, darmowej licencji MIT. Pozwala na bezpłatne wykorzystanie komercyjne i prywatne z zachowaniem noty licencyjnej.
   * **Zestaw ~50 dedykowanych wektorów**: `.cs` (fioletowa tarcza C#), `.py` (węże Pythona), `.ts` (błękitny TS), `.js` (żółty JS), `.json`, `.yaml`, `.md`, `.dockerfile`, `.gitignore`, `.pdf`, `.zip` itp.
3. **Simple Icons (CC0 / Public Domain)**: Oficjalne wektory marek (JetBrains Rider, CLion, IntelliJ, VS Code, Chrome, Firefox, Spotify).

### 7.2. Pobieranie ikon programów HiDPI (`ISystemAppIconResolver`):
* **Windows**: Wywołanie Win32 `SHGetImageList(SHIL_JUMBO)` wyciągające oryginalną ikonę **256x256 px** z pliku `.exe` (zamiast rozmytej miniatury 32x32).
* **macOS**: Odczyt plików `.icns` z paczek `.app`, które posiadają fabrycznie zaszyte miniatury Retina o rozdzielczościach **512x512 px** i **1024x1024 px**.
* **Linux**: Odczyt wektorów SVG lub grafik 256x256 z motywów hicolor.

### 7.3. Kompletny audyt 57 plików graficznych w obecnym repozytorium:
* **28 plików `.png` w `FileIndexProvider/Icons/`** (języki i kategorie): zastąpione wektorami SVG z Material Icon Theme i Material Symbols.
* **12 plików `.png` w `JetBrainsProvider/` i `VsCodeProvider/`**: zastąpione oficjalnymi wektorami SVG z bazy Simple Icons.
* **10 plików `.png` w `ChromeProvider/` i `FirefoxProvider/`**: loga przeglądarek z Simple Icons, akcje (`bookmark`, `history`, `web`) z Material Symbols.
* **2 pliki w `Hub/Assets/`**: `fallback-provider-icon.png` ➔ szklany kafelek z wektorem `extension`; `hub.ico` ➔ multi-res `.ico`/`.icns`/`.svg`.
* **Miniatury multimediów**: Dla rzeczywistych plików `.jpg`/`.png`/`.mp4` system generuje w locie ostre miniatury podglądu zamiast generycznego `image.png`.

---

## 8. Szklane Okno Ustawień, Motywy i Profiler Wydajności

Nowoczesne okno Ustawień z paskiem bocznym (Sidebar: General, Appearance, Plugins, Diagnostics/About) i automatycznym zapisem (`Auto-Save`):

### 8.1. Pełna Personalizacja Kolorów i Motywów:
Wyeliminowanie zahardkodowanych wartości z `MainWindow.xaml` (`#FFB00000`, `#FFFF4A4A`, `#FFFFB000`):
* **4 Role Kolorystyczne**:
  * `AccentColor`: obramowanie okna, paska wyszukiwania i kafelków `168x168 px`.
  * `AccentHoverColor`: neonowa poświata ramki przy najechaniu myszką.
  * `FocusColor`: złocisty obrys aktywnego elementu przy nawigacji strzałkami.
  * `WindowBackground & Opacity`: suwak przezroczystości ciemnego szkła (40–95%).
* **Gotowe Palety (Theme Presets)**:
  1. **`Systemowy (Zgodny z Systemem / Auto)`**: 
     * **Dark / Light Mode**: Automatyczne przełączanie trybu ciemnego/jasnego w zależności od preferencji systemu operacyjnego.
     * **Systemowy Kolor Akcentu (System Accent)**: Automatyczne pobieranie wiodącego koloru z Windowsa (`UISettings.GetColorValue(UIColorType.Accent)`), macOS (`NSColor.controlAccentColor`) lub Linuxa (FreeDesktop Accent Portal).
     * **Reaktywność w locie**: Rejestracja zdarzenia `PlatformSettings.ColorValuesChanged` w Avalonia UI – gdy użytkownik zmieni kolor w Ustawieniach Windowsa lub Maca, Hub natychmiast dostosowuje obrysy bez restartu!
  2. *Cyber Red* (domyślny Huba: `#FFB00000`, hover `#FFFF4A4A`, fokus `#FFFFB000`)
  3. *Matrix Emerald* (szmaragdowa zieleń `#00FF66`)
  4. *Cyberpunk Amber* (złocisty bursztyn `#FFB000`)
  5. *Dracula Purple* (ciemny fiolet `#BD93F9`)
  6. *Nordic Frost* (chłodny błękit `#00D2FF`)
  7. *Stealth Monochrome* (minimalistyczna biel i szarości `#FFFFFF`)
  8. *Własny (Custom Color Picker)*: dowolny kod HEX/RGB z automatycznym wyliczaniem jaśniejszego odcienia hover.
* **Reaktywny `ThemeManager`**: Zmiana koloru w Ustawieniach natychmiast w czasie rzeczywistym aktualizuje otwarte okno launchera bez restartowania aplikacji.

---

### 8.2. Profiler Wydajności w GUI (Zastąpienie telemetrii magisterskiej):
* Pomiar w `SearchOrchestrator` za pomocą `Stopwatch.GetElapsedTime` (dostępny w .NET 8+, działający bez alokacji na stercie).
* W zakładce **Diagnostyka**:
  * Wykres wodospadowy (waterfall timeline) czasów odpowiedzi wtyczek dla ostatniego zapytania:
    ```text
    Kalkulator (Instant)    [  0.1 ms ]  █ (1 wynik)
    Aplikacje (Cache)       [  1.2 ms ]  ███ (2 wyniki)
    Pliki NTFS              [  5.4 ms ]  █████████ (15 wyników)
    Wtyczka Spotify (API)   [ 85.0 ms ]  ██████████████████████████████ (5 wyników)
    ```
  * **Detektor Wąskich Gardeł (Bottleneck Alert)**: Automatyczne podświetlenie wtyczek odpowiadających > 100 ms z sugestią zwiększenia debounce lub wyłączenia wtyczki.
  * Opcjonalny przełącznik *"Zbieraj dane diagnostyczne"* – przy wyłączeniu Hub nie wykonuje żadnych pomiarów.
