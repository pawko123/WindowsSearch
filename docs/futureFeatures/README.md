# Przyszłościowe Rozszerzenia i Architektura Huba (Future Features)

Katalog zawiera skonsolidowaną dokumentację techniczną, architektoniczną oraz roadmapę rozwoju aplikacji **Hub** po zakończeniu prac magisterskich.

Całość dokumentacji została uporządkowana w **3 logiczne dokumenty**:

---

## 🗺️ 1. [ROADMAP.md](./ROADMAP.md) — Plan Rozwoju i Kolejność Wdrożenia
Optymalna kolejność realizacji podzielona na **8 faz (Fazy 0–7)**:
* **Faza 0**: Fundament Architektoniczny & Usunięcie Benchmarków
* **Faza 1**: Deklaratywne UI w C# & Multiplatforma (Avalonia UI, Windows, Linux, macOS)
* **Faza 2**: Szklane Okno Ustawień, Personalizacja Kolorów i Profiler Wydajności
* **Faza 3**: Wtyczki In-Process (`IHubPlugin`), Dynamiczny Instalator ZIP i SDK
* **Faza 4**: Potok Wyszukiwania, Tagi (`#tag`) i Zunifikowana Historia (Frecency)
* **Faza 5**: Błyskawiczne Akcje (Direct Answers: kalkulator/czas oraz ciche akcje z HUD)
* **Faza 6**: Narzędzia Interaktywne (Tłumacz, Base64/ZPL Inspector, Spotify, Wyszukiwarka Plików)
* **Faza 7**: Automatyzacje dla Zaawansowanych (Hotkeys, Quicklinks, Skrypty PowerShell/Python)

---

## 🏗️ 2. [ARCHITECTURE.md](./ARCHITECTURE.md) — Architektura Techniczna i Platforma
Specyfikacja techniczna silnika i platformy uruchomieniowej:
* Dekompozycja monolitu: `Hub.Core` (czysty silnik .NET 9), `Hub.Extensions.Sdk`, `Hub.App`.
* Wtyczki in-process DLL (`AssemblyLoadContext`) — spadek zużycia RAM z ~300 MB do ~25 MB.
* Standard paczek `.zip`, plik `manifest.yaml`, instalacja Drag & Drop i ochrona Zip Slip.
* Deklaratywne UI w C# (`Avalonia.Markup.Declarative`) w stylu Jetpack Compose / Flutter (zero XAML).
* Wieloplatformowość: natywny `TrayIcon` (Apple Menu Bar, Linux SNI), globalne skróty `SharpHook`.
* Zunifikowany system ikon wektorowych: Material Symbols, Material Icon Theme (PKief, MIT), Simple Icons.
* Nowe okno Ustawień z pełną personalizacją kolorów (4 role, presety, custom picker) oraz lekki Profiler Wydajności w GUI.

---

## ⚡ 3. [FEATURES.md](./FEATURES.md) — Katalog Funkcjonalności i Narzędzi
Kompletny przewodnik po funkcjach dla użytkownika:
* **Ergonomia okna**: Zachowanie kafelków `168x168 px` w górnym slocie, kategoria Tools i sekcje w ramkach.
* **Direct Answers vs Command Actions**: Różnica między obliczeniami w locie a cichymi akcjami w tle ze wskaźnikiem HUD.
* **Narzędzia Interaktywne (Mini-Apps)**:
  * Live Translator (Google/DeepL z dwukierunkowym edytorem).
  * Base64, Image & ZPL Label Inspector (lokalny podgląd etykiet logistycznych Zebra w SkiaSharp, grafiki i tekst).
  * Spotify Mini-Player (sterowanie + wyszukiwarka).
  * Zaawansowana Wyszukiwarka Plików (`files`) z filtrami typu, rozszerzeń, rozmiaru i podglądem Quick Look pod spacją.
* **Smart Intent Sniffing**: Auto-wykrywanie kodów ZPL, Base64, JSON, kolorów HEX/RGB i sugerowanie narzędzia na szczycie.
* **Zunifikowana Historia i Frecency**: `Frequent & Pinned` (aplikacje + narzędzia na górze) oraz chronologiczny strumień `Recent`.
* **Automatyzacje**: Silnik skrótów globalnych, szablony Quicklinks (`gh {q}`, `jira {id}`) oraz skrypty użytkownika (PowerShell `.ps1`, Python `.py`) z nagłówkami metadanych.
