# Roadmapa Rozwoju Huba (Fazy 0–7)

> **Dokument Nadrzędny**: Kolejność realizacji zaplanowanych innowacji oparta na minimalizacji podwójnego pisania kodu.

Przejście na **deklaratywne UI w C#** oraz **multiplatformę** realizujemy na początku (na prostej aplikacji), dzięki czemu wszystkie nowe ekrany, narzędzia i wtyczki piszemy **tylko raz w docelowej technologii**, bez podwójnej pracy w starym XAML-u.

---

```
[ Faza 0: Fundamenty & Porządki ]
                │
                ▼
[ Faza 1: Deklaratywne UI & Multiplatform ]  <-- Wcześnie: zero podwójnego pisania w XAML!
                │
                ▼
[ Faza 2: Szklane Ustawienia & Profiler ]    <-- Nowy wygląd, motywy i zamiana telemetrii
                │
                ▼
[ Faza 3: Wtyczki In-Process & Instalator ZIP ] <-- Od razu po unifikacji wtyczek
                │
                ▼
[ Faza 4: Wyszukiwarka, Tagi & Frecency ]
                │
                ▼
[ Faza 5: Błyskawiczne Akcje (Direct Answers & Silent Commands) ]
                │
                ▼
[ Faza 6: Narzędzia Interaktywne & Eksplorator Plików ]
                │
                ▼
[ Faza 7: Automatyzacje dla Zaawansowanych (Hotkeys, Quicklinks, Skrypty) ]
```

---

## Przegląd Faz Realizacji

### 🔹 Faza 0: Fundament Architektoniczny & Oczyszczenie Długu Magisterskiego
* **Usunięcie benchmarków**: Wycięcie solucji `Benchmarking/`, serwisu `BenchmarkControlService.cs`, stoperów i telemetrii z kodu produkcyjnego.
* **Rozbicie na 3 projekty**:
  * `Hub.Core`: czysty silnik .NET 9 (zero WPF/XAML/Win32, w 100% testowalny jednostkowo).
  * `Hub.Extensions.Sdk`: ultralekkie kontrakty dla wtyczek (`IHubPlugin`, `SearchResultItem`, `ResultAction`).
  * `Hub.App`: powłoka wizualna i integracja z systemem.
* **Testy automatyczne**: Projekt `Hub.Core.Tests` w xUnit zastępujący harness badawczy.

---

### 🔹 Faza 1: Deklaratywne UI & Multiplatforma (Avalonia C#)
*Robimy to na prostej aplikacji, zanim powstaną nowe ekrany i narzędzia!*
* **Deklaratywne UI w C# (`Avalonia.Markup.Declarative`)**: Całkowita likwidacja plików `.xaml` na rzecz czystych komponentów C# w stylu Jetpack Compose / Flutter (brak konwerterów widoczności, 120 FPS na SkiaSharp).
* **Zachowanie oryginalnego układu Glass UI**: Ciemne szkło (`#CC000000`), promień 14px, duże kafelki `168x168 px` w górnym slocie.
* **Zunifikowane Wektory**: Material Symbols dla akcji, Material Icon Theme (PKief, MIT) dla plików (.cs, .py, .ts, .json itp.), Simple Icons (CC0) dla marek.
* **Multiplatforma od zaraz**: Działa na Windows (Mica/Acrylic), Linux (X11/Wayland) oraz macOS (Cocoa/Vibrancy). Globalny skrót przez `SharpHook`, indeksowanie `.desktop`/`.app` oraz natywny wieloplatformowy `TrayIcon`.

---

### 🔹 Faza 2: Nowe Okno Ustawień, Personalizacja Kolorów i Profiler
* **Nowoczesne Okno Ustawień w Glass UI**: Szklany pasek boczny (Sidebar: General, Appearance, Plugins, Diagnostics/About) i karty `GlassSettingCardControl`.
* **Pełna personalizacja kolorów**: Wybór barw dla 4 ról (`Accent`, `Hover`, `Focus`, `Opacity`), gotowe palety (*Cyber Red*, *Emerald*, *Amber*, *Dracula* itp.) i Color Picker aktualizujący otwarty interfejs w locie bez restartu.
* **Zamiana telemetrii na Profiler w GUI**: Zakładka Diagnostyka z wykresem wodospadowym (waterfall) czasów odpowiedzi wtyczek (`Stopwatch.GetElapsedTime`) i detektorem wąskich gardeł (> 100 ms).

---

### 🔹 Faza 3: Wtyczki In-Process i Dynamiczny Instalator ZIP
* **Wtyczki In-Process (`IHubPlugin`)**: Rezygnacja z procesów `.exe` – wtyczki stają się bibliotekami DLL ładowanymi do procesu Huba (spadek RAM z **~300 MB do ~25 MB**).
* **Dynamiczny `ExtensionManager`**: Izolacja bibliotek przez `AssemblyLoadContext` (`isCollectible: true`) – ładowanie i wyładowywanie bez restartu.
* **Instalator ZIP (Drag & Drop)**: Standard paczek `.zip` z plikiem `manifest.yaml` – upuszczenie pliku na okno Ustawień natychmiast instaluje wtyczkę do `./Extensions/` (zabezpieczenie Zip Slip).
* **SDK dla Twórców**: Paczka NuGet `Hub.Extensions.Sdk`, eksport offline z Ustawień i szablony `dotnet new hub-extension`.

---

### 🔹 Faza 4: Wyszukiwarka, Tagi i Zunifikowana Historia (Frecency)
* **Nowy `SearchOrchestrator`**: Obsługa wielozadaniowych wtyczek z hierarchią komend, pigułka zakresu (`Scope Pill` pod `Tab`) oraz szybkie tagi `#tag` (`#files`, `#dev`, `#music`) omijające niepasujące wtyczki (< 1 ms).
* **Zunifikowana Historia w SQLite**: Tabela `LaunchHistory` – jeden chronologiczny strumień **Recent** w stanie pustym (pliki, narzędzia, zakładki, muzyka w jednej liście z etykietami `5 min temu`, `Wczoraj`).
* **Algorytm Frecency**: Górny slot kafelków `168x168 px` staje się **`Frequent & Pinned: Aplikacje + Narzędzia`** (narzędzia naturalnie trafiają do górnego rzędu obok programów).

---

### 🔹 Faza 5: Błyskawiczne Akcje (Direct Answers & Command Actions)
*Ewaluacje jednoliniowe i ciche akcje bez konieczności przełączania widoków:*
* **Direct Answers w locie**: Kalkulator (Shunting-Yard) i strefy czasowe (baza IANA) w dynamicznym slocie zastępującym kafelki aplikacji na czas obliczeń. Dwuosiowa nawigacja przestrzenna 2D z klawiatury.
* **Command Actions**: Ciche akcje w tle (`spotify next`, `mute`, `empty trash`) widoczne na liście z badgem **`[ Akcja ]`**, wykonywane po `Enter` ze szklanym wskaźnikiem HUD Toast na dole ekranu.

---

### 🔹 Faza 6: Narzędzia Interaktywne & Eksplorator Plików (Mini-Apps)
*Pełnowymiarowe mini-aplikacje działające w oknie Huba na stosie nawigacji:*
* **Stos Nawigacji (`NavigationStack`)**: Płynne wsuwanie widoków narzędzi po `Enter` i powrót po `Esc`.
* **Kategoria `Tools` na szczycie pionowej listy**: Pierwsza sekcja pod aplikacjami z badgem `[ Narzędzie ]`.
* **Smart Intent Sniffing**: Auto-analiza zapytania i sugerowanie narzędzia na 1. miejscu (ZPL, Base64, JSON, kolory).
* **Flagowe Mini-Aplikacje**:
  * *Live Translator* (Google/DeepL, dwupanelowy edytor).
  * *Base64, Image & ZPL Label Inspector* (podgląd etykiet kurierskich Zebra w SkiaSharp offline, grafiki i tekst).
  * *Spotify Mini-Player* (odtwarzacz ze sterowaniem i wbudowaną wyszukiwarką).
  * *Zaawansowana Przeglądarka Plików (`files`)*: część wtyczki File Indexera z filtrami typu (foldery, zdjęcia, kod, dokumenty), rozszerzeń, daty, rozmiaru i podglądem Quick Look pod spacją.

---

### 🔹 Faza 7: Automatyzacje dla Zaawansowanych (Workflows, Hotkeys & Scripts)
* **Silnik Skrótów Globalnych (Hotkeys Engine)**: Przypisywanie skrótów systemowych do dowolnego narzędzia, akcji w tle czy skryptu (np. `Ctrl+Alt+T` ➔ Tłumacz).
* **Szybkie Łącza (Quicklinks)**: Szablony URL z parametrami (`gh {q}`, `jira {id}`).
* **Skrypty Użytkownika (Script Commands w stylu Raycast)**: Uruchamianie skryptów PowerShell (`.ps1`) i Python (`.py`) z folderu użytkownika z nagłówkami metadanych bez kompilacji C#.
* **Łączenie Akcji (Chained Actions)**: Przekazywanie pliku lub tekstu do kolejnych akcji pod strzałką w prawo `→`.
