# Katalog Funkcjonalności Huba: Specyfikacja Narzędzi i UX

> **Dokument Funkcjonalny**: Pełna specyfikacja interfejsu, narzędzi interaktywnych, akcji, zaawansowanego wyszukiwania, historii Frecency i automatyzacji.

---

## 1. Rzeczywisty Układ Okna Głównego (Glass UI)

Układ okna Huba zachowuje swoją sprawdzoną geometrię z `MainWindow.xaml` (szerokość 860 px, zaokrąglenie rogów `CornerRadius="14"`, dymione ciemne szkło `Background="#CC000000"`, karmazynowy obrys `BorderBrush="#FFB00000"`):

```text
┌─ [Border CornerRadius="14", BorderBrush="#FFB00000", Background="#CC000000"] ─────────┐
│                                                                                        │
│  ┌─ [TextBox SearchBox Height="44", BorderBrush="#55FF0000"] ──────┐ [ 🏷️ Tab ] [⏳] │
│  │ 🔍 Wpisz zapytanie, matematykę lub #tag...                      │ (Pigułka) (Bounce)│
│  └─────────────────────────────────────────────────────────────────┘                   │
│                                                                                        │
│  GÓRNY SLOT DYNAMICZNY:                                                                │
│  • DOMYŚLNIE: CZĘSTO UŻYWANE I PRZYPIĘTE (Frequent & Pinned)                           │
│  ┌──────────────────────────────────────────────────────────────────────────────────┐  │
│  │ [WrapPanel z DUŻYMI KWADRATOWYMI KAFELKAMI 168x168 px, BorderBrush="#44FF0000"]  │  │
│  │                                                                                  │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │  │
│  │  │ ┌──────────┐ │  │ ┌──────────┐ │  │ ┌──────────┐ │  │ ┌──────────┐ │          │  │
│  │  │ │ Box 72px │ │  │ │ Box 72px │ │  │ │ Box 72px │ │  │ │ Box 72px │ │          │  │
│  │  │ │  💻 (52) │ │  │ │  🔤 (52) │ │  │ │  🌐 (52) │ │  │ │  💬 (52) │ │          │  │
│  │  │ └──────────┘ │  │ └──────────┘ │  │ └──────────┘ │  │ └──────────┘ │          │  │
│  │  │ Rider (14px) │  │Base64/ZPL(14)│  │ Tłumacz(14) │  │ Slack (14px) │          │  │
│  │  │ Program (11) │  │ Narzędzie(11)│  │ Narzędzie(11)│  │ Program (11) │          │  │
│  │  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘          │  │
│  └──────────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                        │
│  • GDY WPISANO RÓWNANIE LUB CZAS: KARTA DIRECT ANSWER (Zastępuje kafelki):             │
│  ┌──────────────────────────────────────────────────────────────────────────────────┐  │
│  │  🧮  1 000                                           [↵ Kopiuj wynik (1 000)]    │  │
│  │      25 * 40                                         [→ Wstaw do zapytania]      │  │
│  │                                                      [→ Kopiuj całe równanie]    │  │
│  └──────────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                        │
│  PIONOWA LISTA WYNIKÓW (KATEGORIE W RAMKACH CornerRadius="10", Background="#16000000"):│
│                                                                                        │
│  ➔ 1. OSTATNIO UŻYWANE (Recent - Zunifikowany strumień chronologiczny):                │
│     ┌──────────────────────────────────────────────────────────────────────────────┐ │  │
│     │ [Boks 40x40] 🔤 Base64 & ZPL Inspector     [ Narzędzie ]      [ 8 min temu ] │ │  │
│     │ [Boks 40x40] 📄 specyfikacja_v2.docx       [ Plik ]           [ 35 min temu] │ │  │
│     │ [Boks 40x40] 🌐 Tłumacz                    [ Narzędzie ]      [ 2 godz. temu]│ │  │
│     │ [Boks 40x40] 🔖 Jira - PROJ-104            [ Zakładka ]       [ Wczoraj ]    │ │  │
│     └──────────────────────────────────────────────────────────────────────────────┘ │  │
│                                                                                        │
│  ➔ 2. NARZĘDZIA (Tools - Dedykowana kategoria na szczycie):                            │
│     ┌──────────────────────────────────────────────────────────────────────────────┐ │  │
│     │ [Boks 40x40] 🌐 Tłumacz (Google / DeepL)   [ Narzędzie ]                     │ │  │
│     │ [Boks 40x40] 🔤 Base64 & ZPL Inspector     [ Narzędzie ]                     │ │  │
│     │ [Boks 40x40] 🎵 Spotify Mini-Player        [ Narzędzie ]                     │ │  │
│     │ [Boks 40x40] 📁 Zaawansowana Wyszukiwarka  [ Narzędzie ]                     │ │  │
│     └──────────────────────────────────────────────────────────────────────────────┘ │  │
│                                                                                        │
│  ➔ 3. PLIKI I DANE (Wyniki wyszukiwania z indeksu i wtyczek):                          │
│     ┌──────────────────────────────────────────────────────────────────────────────┐ │  │
│     │ [Boks 40x40] 📄 Program.cs                 [ Plik C# ]        C:\Projekty\...│ │  │
│     └──────────────────────────────────────────────────────────────────────────────┘ │  │
│                                                                                        │
│  Type to search. Arrows move, Enter launches, Esc hides, click launches. (#99FFFFFF)   │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Dwa Typy Akcji: Direct Answers vs Command Actions

| Cecha | 1. Direct Answer (Instant Evaluation) | 2. Command Action (Silent / No-View Action) |
| :--- | :--- | :--- |
| **Kiedy się wykonuje?** | **W locie podczas pisania** (< 0.2 ms na każdy znak) | **Dopiero po naciśnięciu `Enter`** lub ze skrótu |
| **Charakter** | **Czyste obliczenie matematyczne / informacja** | **Efekt uboczny (Side-effect)** w systemie/aplikacji |
| **Reakcja UI** | Szeroka karta z wynikiem w górnym slocie okna | **Zero okna** (chowa Hub) + szklany wskaźnik HUD Toast |
| **Przykłady** | `25 * 4` ➔ `1 000`<br>`time tokyo` ➔ `18:45`<br>`100 usd in pln` ➔ `398.50 PLN` | `spotify next` ➔ przełącza utwór<br>`mute` ➔ wycisza dźwięk<br>`empty trash` ➔ opróżnia kosz |

### 2.1. Silnik Matematyczny (Shunting-Yard Parser)
* **Tokenizacja**: Oparta o `ReadOnlySpan<char>` – zero alokacji pamięci na stercie (heap allocations).
* **Precyzja**: Obliczenia podstawowe na typie `decimal` (eliminuje artefakty zmiennoprzecinkowe `0.1 + 0.2 = 0.3`), funkcje naukowe na `double`.
* **Operatory**: `+`, `-`, `*`, `/`, `^` (potęga), `%` (modulo), nawiasy `()`.
* **Funkcje**: `sqrt`, `sin`, `cos`, `tan`, `abs`, `log`, `ln`, `round`, `pi`, `e`.
* **Odporność na błędy**: Dzielenie przez zero zwraca informację tekstową `Błąd: Dzielenie przez zero` zamiast wyjątku; zapytania niedokończone (np. `2 + `) czekają na kolejny znak.

### 2.2. Silnik Stref Czasowych (World Clock)
* **Detekcja wzorców PL/EN**:
  * `^(?:time|czas|godzina)\s+(?:in|at|w|we)?\s*(?<place>.+)$`
  * `^(?<place>.+?)\s+(?:time|czas|godzina)$`
  * Kody stref: `utc`, `cet`, `cest`, `est`, `edt`, `pst`, `pdt`, `jst`, `gmt`, `cst`.
* **Baza i obliczenia**: Wykorzystanie systemowego `TimeZoneInfo` z mapowaniem IANA (`Asia/Tokyo`, `America/New_York`, `Europe/Warsaw`).
* **Formatowanie**: Lokalna godzina (np. `18:45`), przesunięcie względem czasu lokalnego użytkownika (np. `8h do przodu`) oraz informacja o dacie (`Jutro` / `Wczoraj`).

### 2.3. Nawigacja Przestrzenna 2D z Klawiatury (Spatial Navigation):
```text
┌────────────────────────────────────────────────────────┐
│  [SEKCJA GŁÓWNA KARTY]         │  [KOLUMNA AKCJI]      │
│                                │                       │
│   🧮   1 000                   │  [1. Kopiuj wynik]    │
│        25 * 40                 │  [2. Wstaw do pola]   │
│                                │  [3. Kopiuj równanie] │
└────────────────────────────────┼───────────────────────┘
  │                              │        │
  │ [↓ Dół]                      │ [←]    │ [↓ Dół po liście akcji]
  │ (od razu do providerów)      │ wraca  │ (z ostatniej schodzi niżej)
  ▼                              ▼        ▼
┌────────────────────────────────────────────────────────┐
│  [WYNIKI LISTY PONIŻEJ (Tools / Pliki)]                │
└────────────────────────────────────────────────────────┘
```
* **Stan A (Fokus na głównej karcie)**: `Enter` kopiuje wynik do schowka i zamyka okno. `Strzałka w prawo (→)` przechodzi do kolumny akcji. `Strzałka w dół (↓)` natychmiast omija kolumnę akcji i schodzi do wyników poniżej.
* **Stan B (Fokus w kolumnie akcji)**: `Strzałka w lewo (←)` wraca do karty głównej. `Góra/Dół (↑/↓)` porusza się po akcjach (`Wstaw do zapytania`, `Kopiuj całe równanie`). Z ostatniej akcji `↓` schodzi do listy wyników poniżej.

### 2.4. Command Actions na Liście Wyników (Ciche akcje z wskaźnikiem HUD)
Gdy wpisujesz np. `next`, `mute` lub `trash`, komenda pojawia się na liście z wyraźnym badgem **`[ Akcja ]`**:
```text
➔ ⏭️ Następny utwór                     [ Akcja ]     [↵ Wykonaj]
   Spotify • Przełącza piosenkę na następną w kolejce
```
Naciśnięcie `Enter` natychmiast chowa Hub, wykonuje operację w tle i wyświetla na dole ekranu szklany wskaźnik HUD:
```text
┌────────────────────────────────────────────────────────┐
│  🎵 Następny utwór: Queen — Bohemian Rhapsody          │
└────────────────────────────────────────────────────────┘
```
Pod strzałką w prawo `→` użytkownik ma opcję: `[→ Przypisz skrót globalny]` (np. `Ctrl+Alt+N`).

---

## 3. Narzędzia Interaktywne (Interactive Tools & Mini-Apps)

Dedykowane mini-aplikacje działające na **Stosie Nawigacji (`NavigationStack`)**:
Wpisujesz nazwę narzędzia ➔ `Enter` ➔ okno Huba płynnie wsuwa dedykowany widok ➔ robisz swoje ➔ `Esc` cofa do wyszukiwarki.

### 3.1. Live Translator (Google / DeepL)
* Górny selektor języków: `[ Angielski ▾ ]` `⇄ (Ctrl+S)` `[ Polski ▾ ]`.
* Dwa połączone obszary tekstowe w stylu Glass UI:
  * Górne pole: tekst źródłowy (tłumaczenie w locie z mikro-debounce).
  * Dolne pole: przetłumaczony wynik z możliwością edycji.
* Skróty: `Enter` (kopiuje wynik i zamyka Hub), `Tab` (przełącza fokus), `Ctrl+S` (zamienia języki miejscami).

### 3.2. Base64, Image & ZPL Label Inspector
* **Tryb Kodowania (Encode)**: Wpisanie tekstu lub przeciągnięcie pliku (Drag & Drop) ➔ natychmiastowy ciąg Base64 + przycisk `[Kopiuj]`.
* **Tryb Dekodowania (Decode - Auto-Detect)**:
  1. *Tekst / JSON / XML*: Kolorowanie składni, formatowanie, walidacja.
  2. *Obrazy (PNG, JPG, WebP, SVG)*: Podgląd grafiki w oknie + wymiary (`1920x1080 px`), waga i przycisk `[↵ Kopiuj jako bitmapę]`.
  3. *Etykiety ZPL (Zebra Programming Language)*:
     * **100% lokalny, bezpieczny silnik SkiaSharp (Offline)**: Kody kreskowe Code 128, EAN, kody QR i tekst renderowane są prosto do grafiki w pamięci RAM – **żadne dane adresowe nie wyciekają do sieci**.
     * Podgląd wyrenderowanej etykiety transportowej w skali 1:1.
     * Przełącznik na podgląd surowego kodu ZPL z możliwością edycji parametrów w locie.
     * Akcje: `[Kopiuj jako PNG]`, `[Zapisz do PDF]`, `[Drukuj na drukarce Zebra]`.

### 3.3. Spotify Mini-Player & Search (Hybryda 3 w 1)
W ramach jednej wtyczki łączymy 3 zastosowania:
1. *Search Provider*: Utwory pojawiają się w wynikach wyszukiwania Huba po wpisaniu nazwy artysty/piosenki.
2. *Command Actions*: Ciche komendy w tle (`spotify next`, `spotify pause`).
3. *Interactive Mini-Player View* (po wpisaniu `spotify` + `Enter`):
   * Duża okładka albumu w szklanym kafelku, tytuł, wykonawca, pasek postępu (`2:14 / 3:45`).
   * Kontrolki: `[Poprzedni]` `[Play / Pause]` `[Następny]` `[Polub (♥)]` (skrót: `Spacja` = Play/Pause).
   * Pod spodem: Wbudowana wyszukiwarka utworów i kolejka odtwarzania.

### 3.4. Zaawansowana Przeglądarka Plików (`files`)
Dedykowany eksplorator wtyczki File Indexera:
* **Kapsułki filtrów typu**: `[ 📁 Tylko foldery ]`, `[ 🖼️ Zdjęcia ]`, `[ 📄 Dokumenty ]`, `[ 💻 Kod ]`, `[ 🎬 Wideo ]`, `[ 📦 Archiwa ]`.
* **Filtry zaawansowane**:
  * Rozszerzenie: wpisanie z ręki (np. `.sql`) lub wybór z listy.
  * Rozmiar (Disk Cleaner): *Duże (>100 MB)* i *Gigantyczne (>1 GB)* do błyskawicznego czyszczenia dysku.
  * Data modyfikacji: *Dzisiaj*, *Ostatni tydzień*, *Ostatni miesiąc*.
  * Zakres folderu: ograniczenie szukania np. tylko do `C:\Projekty`.
* **Szybki podgląd pod Spacją (Quick Look)**:
  * Naciśnięcie `Spacji` wyświetla natychmiastowy szklany podgląd:
    * Zdjęcia: miniatura w wysokiej rozdzielczości.
    * Kod (`.cs`, `.py`, `.ts` itp.): pierwsze 40 linii kodu z kolorowaniem składni.
    * Dokumenty / PDF: podgląd tekstu i pierwszej strony.
* **Panel szczegółów pliku**: Duża wektorowa ikona z Material Icon Theme, waga, data i skróty: `Enter` (otwórz), `Ctrl+Enter` (pokaż w Eksploratorze Windows), `Ctrl+C` (kopiuj ścieżkę), `Ctrl+T` (otwórz w terminalu).
* **Wydajność SQLite (< 3 ms)**: Indeksy na kolumnach `Extension`, `IsDirectory`, `SizeBytes`, `LastModifiedUtc`.

---

## 4. Inteligentne Wykrywanie Zawartości (Smart Intent Sniffing)

Gdy użytkownik wkleja (`Ctrl+V`) lub wpisuje dane, Hub automatycznie analizuje strukturę zapytania i sugeruje właściwe narzędzie na **1. miejscu sekcji `Tools`**:

1. **Wklejenie kodu ZPL / Base64**:
   * Ciąg `^XA^FO50,50^ADN,36,20^FDTest^FS^XZ` lub długi Base64 z paddingiem `==`
   * ➔ Sugestia: `🔤 Base64 & ZPL Label Inspector • Wykryto etykietę ZPL [↵ Otwórz podgląd]`
2. **Wklejenie tekstu obcojęzycznego**:
   * Dłuższe zdanie w obcym języku lub prefiks `jak jest po angielsku...`
   * ➔ Sugestia: `🌐 Tłumacz • Przetłumacz wpisany tekst w locie [↵ Otwórz]`
3. **Wklejenie niesformatowanego JSON / XML**:
   * Ciąg `{"id":102,"name":"Jan","status":"active"}`
   * ➔ Sugestia: `🛠️ JSON Formatter & Validator • Wykryto dane JSON [↵ Formatuj i zbadaj]`
4. **Wpisanie kodu koloru (HEX / RGB / HSL)**:
   * Ciąg `#FF5733` lub `rgb(255, 87, 51)`
   * ➔ Sugestia: `🎨 Próbnik Kolorów • Kolor #FF5733 [↵ Otwórz paletę i konwerter]`
5. **Wpisanie filtrów plików**:
   * Ciąg `*.cs`, `ext:pdf` lub `size:>100mb`
   * ➔ Sugestia: `📁 Zaawansowana Wyszukiwarka Plików • Filtruj z tym kryterium [↵ Otwórz]`

---

## 5. Tagi, Filtry Zakresu i Wielozadaniowe Wtyczki

Pojedyncza wtyczka (np. `DeveloperTools.dll`) może dostarczać wiele komend, każda z osobnymi tagami:
* **Pigułka Zakresu (Scope Pill)**: W prawym rogu paska wyszukiwania `SearchBox`. Naciśnięcie `Tab` rozwija listę kategorii:
  `[ 🌐 Wszystko ]`, `[ 🛠️ Programowanie & Dev ]` (`#dev`), `[ 📁 Pliki i Kod ]` (`#files`), `[ 🎵 Muzyka & Media ]` (`#music`), `[ ⚙️ System ]` (`#system`).
* **Szybkie tagi `#tag`**: Wpisanie `#dev zpl` lub `#files projekt` natychmiast zawęża wyszukiwarkę i omija niepasujące wtyczki (< 1 ms).
* **Granularne zarządzanie**: W oknie Ustawień użytkownik widzi listę komend wtyczki i może wyłączać pojedyncze funkcje bez wyłączania całej wtyczki.

---

## 6. Zunifikowana Historia i Algorytm Frecency (Recent & Frequent)

Zamiast sztucznych silosów, historia w bazie SQLite jest zunifikowana:
* **Górny slot (`Frequent & Pinned`)**: Łączy najczęstsze programy i narzędzia w kafelkach `168x168 px` (np. Rider obok Base64/ZPL i Tłumacza). Opcja przypięcia na stałe pod `Ctrl+P`.
* **Poniżej (`Recent`)**: Zunifikowany chronologiczny strumień łączący pliki, narzędzia, zakładki www i muzykę w jednej liście z etykietami czasu względnego (`8 min temu`, `Wczoraj`).

### 6.1. Model bazy SQLite (`LaunchHistory`):
```sql
CREATE TABLE LaunchHistory (
    ItemId TEXT PRIMARY KEY,            -- np. "tool.base64", "file:C:\doc.pdf"
    Title TEXT NOT NULL,
    Subtitle TEXT,
    Kind TEXT NOT NULL,                 -- "Tool", "File", "Bookmark", "Media", "App"
    IconKind TEXT NOT NULL,
    IconValue TEXT NOT NULL,
    ActionPayload TEXT NOT NULL,
    LaunchCount INTEGER DEFAULT 1,
    LastLaunchedUtc DATETIME NOT NULL
);

CREATE INDEX IX_LaunchHistory_LastLaunched ON LaunchHistory(LastLaunchedUtc DESC);
CREATE INDEX IX_LaunchHistory_Kind ON LaunchHistory(Kind);
```

### 6.2. Wzór i Wagi Algorytmu Frecency:
$$\text{Frecency Score} = \text{LaunchCount} \times \text{RecencyWeight}$$

| Okres ostatniego użycia | Waga świeżości (`RecencyWeight`) |
| :--- | :--- |
| Ostatnie 4 godziny | **100** |
| Ostatnie 24 godziny | **80** |
| Ostatnie 7 dni | **40** |
| Ostatnie 30 dni | **20** |
| Starsze niż 30 dni | **5** |

Akcje pod ręką: `Enter` (wznawia element), `Ctrl+Shift+Delete` (usuwa wpis z historii).

---

## 7. Automatyzacje dla Zaawansowanych (Workflows, Hotkeys & Scripts)

1. **Silnik Skrótów Globalnych (Hotkeys Engine)**:
   * Przypisanie skrótu systemowego do dowolnego narzędzia, akcji w tle czy skryptu (np. `Ctrl+Alt+T` ➔ Tłumacz, `Ctrl+Alt+Z` ➔ Inspektor ZPL, `Ctrl+Alt+N` ➔ Następny utwór).
2. **Szybkie Łącza z Parametrami (Quicklinks)**:
   * Szablony URL z parametrami w konfiguracji YAML:
     ```yaml
     quicklinks:
       - trigger: "gh"
         name: "Szukaj na GitHubie"
         url: "https://github.com/search?q={query}"
       - trigger: "jira"
         name: "Zgłoszenie Jira"
         url: "https://firma.atlassian.net/browse/{query}"
     ```
   * Wpisanie: `gh search engine` + `Enter` ➔ otwiera wyszukiwanie na GitHubie.
3. **Skrypty Użytkownika (Script Commands w stylu Raycast)**:
   * Użytkownik wskazuje folder `~/HubScripts/` i wrzuca zwykłe skrypty **PowerShell (`.ps1`)**, **Python (`.py`)** lub **Bash (`.bat`)** z nagłówkiem w komentarzu:
     ```powershell
     # @hub.title Wyczyść Cache Dockera i Kosz
     # @hub.mode silent
     # @hub.icon terminal

     docker system prune -af --volumes
     Clear-RecycleBin -Force -ErrorAction SilentlyContinue
     Write-Output "Wyczyszczono środowisko deweloperskie!"
     ```
   * Skrypt pojawia się w Hubie jako komenda, wykonuje się w tle (`silent`), a po zakończeniu wyświetla powiadomienie HUD.
4. **Łączenie Akcji (Chained Actions)**:
   * Przekazywanie wyniku wyszukiwania (np. pliku) do kolejnej akcji pod strzałką w prawo `→` (Otwórz w VS Code, Wyślij na Slacku, Kopiuj jako Base64).
