# GitHub Actions — Build Hub (Host App)

## Cel

Workflow buduje aplikację hosta (WPF) jako self-contained EXE dla platformy `win-x64`. Wynik jest uploadowany jako artefakt Actions. Przy tagu `v*` automatycznie tworzy GitHub Release i dołącza gotowy plik ZIP.

---

## Zakładana struktura repozytorium

```
/
├── .github/
│   └── workflows/
│       └── build-hub.yml       ← plik workflow (treść poniżej)
├── proto/
│   └── search.proto            ← wspólna definicja Protobuf
├── Hub/
│   ├── Hub.sln                 ← solution file (zawiera Hub + Hub.Tests)
│   ├── Hub/
│   │   └── Hub.csproj          ← projekt WPF (OutputType: WinExe)
│   └── Hub.Tests/
│       └── Hub.Tests.csproj    ← projekt testowy NUnit (dotnet test Hub.sln)
└── exampleProviders/
    └── {nazwa_providera}/
        └── ...
```

---

## Triggery

| Trigger | Kiedy uruchamiany |
|---|---|
| `push` → `main` / `master` | Każdy push do gałęzi `main` lub `master` (w tym merge PR) |
| `push` → tag `v*` | Pchanie tagu np. `v1.0.0` — dodatkowo tworzy GitHub Release |

> **Dlaczego brak `pull_request`?** W GitHub Actions merge PR-a generuje zdarzenie `push` na gałęzi docelowej — workflow uruchomi się automatycznie. Dodatkowy trigger `pull_request` skutkowałby podwójnym buildem. Testy i weryfikacja kodu mogą odbywać się w osobnym lightweight workflow (np. `dotnet build` + `dotnet test` bez publish) triggerowanym na PR.

---

## Plik workflow: `.github/workflows/build-hub.yml`

```yaml
name: Build Hub

on:
  push:
    branches: [ main, master ]   # merge PR → push na main/master; brak osobnego pull_request
    tags:    [ "v*" ]

jobs:
  build:
    name: Build & Publish Hub
    runs-on: windows-latest        # WPF wymaga Windows SDK — Linux runner nie zadziała

    steps:
      # 1. Pobierz kod źródłowy
      - name: Checkout
        uses: actions/checkout@v4
        with:
          fetch-depth: 0            # pełna historia potrzebna do tagów

      # 2. Zainstaluj .NET SDK (musi pasować do TargetFramework w .csproj)
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"

      # 3. Przywróć pakiety NuGet (w tym Google.Protobuf, Grpc.Tools, WPF)
      - name: Restore NuGet packages
        run: dotnet restore Hub/Hub.sln

      # 4. Uruchom testy jednostkowe przed buildem produkcyjnym
      #    Projekt testowy Hub.Tests musi być dodany do Hub.sln
      - name: Run unit tests
        run: dotnet test Hub/Hub.sln --configuration Release --no-restore --logger "trx;LogFileName=test-results.trx"

      # 5. Buduj i publikuj jako self-contained EXE
      #    --self-contained    → runtime .NET dołączony do outputu (brak wymogu instalacji .NET na maszynie użytkownika)
      #    --runtime win-x64   → platforma docelowa
      #    -p:PublishSingleFile=true → scalenie do jednego .exe (opcjonalne, można wyłączyć)
      #    -p:WindowsAppSDKSelfContained=true → wymagane dla Windows App SDK
      - name: Publish Hub (self-contained)
        run: |
          dotnet publish Hub/Hub/Hub.csproj `
            --configuration Release `
            --runtime win-x64 `
            --self-contained true `
            -p:PublishSingleFile=false `
            -p:WindowsAppSDKSelfContained=true `
            --output ./publish/hub

      # 6. Spakuj output do ZIP (łatwiejszy download artefaktu)
      - name: Zip publish output
        run: Compress-Archive -Path ./publish/hub/* -DestinationPath ./publish/Hub-win-x64.zip
        shell: pwsh

      # 7. Uploaduj ZIP jako artefakt Actions (dostępny 90 dni, widoczny w zakładce "Artifacts")
      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: Hub-win-x64-${{ github.sha }}
          path: ./publish/Hub-win-x64.zip
          retention-days: 30

      # 8. Utwórz GitHub Release i dołącz ZIP — tylko przy tagu v*
      - name: Create GitHub Release
        if: startsWith(github.ref, 'refs/tags/v')
        uses: softprops/action-gh-release@v2
        with:
          files: ./publish/Hub-win-x64.zip
          generate_release_notes: true   # automatyczne changelog z commit messages
```

---

## Wymagania po stronie projektu Hub

### `Hub.csproj` — wymagane właściwości

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <RuntimeIdentifiers>win-x64</RuntimeIdentifiers>
    <!-- Windows App SDK self-contained publish -->
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <SelfContained>true</SelfContained>
  </PropertyGroup>
</Project>
```

### Pakiety NuGet wymagane przez workflow

| Pakiet | Projekt | Rola |
|---|---|---|
| `Microsoft.WindowsAppSDK` | Hub | WPF runtime |
| `Google.Protobuf` | Hub | serializacja Protobuf |
| `Grpc.Tools` | Hub | generowanie kodu C# z `.proto` (BuildAction) |
| `NUnit` | Hub.Tests | framework testowy |
| `NUnit3TestAdapter` | Hub.Tests | integracja z Rider / VS Test Explorer |
| `Microsoft.NET.Test.Sdk` | Hub.Tests | wymagany przez `dotnet test` |

Projekt `Hub.Tests` musi być dodany do `Hub.sln` (`dotnet sln Hub.sln add Hub.Tests/Hub.Tests.csproj`), aby `dotnet test Hub/Hub.sln` go wykrył.

---

## Artefakt — co zawiera ZIP

```
Hub-win-x64.zip
└── Hub.exe                 ← główny executable
└── *.dll                   ← zależności WPF, Protobuf itp. (jeśli PublishSingleFile=false)
└── app_config.yaml         ← przykładowy plik konfiguracyjny (jeśli dodany do projektu jako Content)
└── providers/              ← pusty katalog (tworzony przez app, ale warto mieć placeholder)
```

> **Uwaga:** Przy `PublishSingleFile=true` wszystkie `.dll` są spakowane wewnątrz `.exe`. Przy WPF SingleFile może mieć ograniczenia — zalecane `PublishSingleFile=false` i dystrybucja całego folderu.

---

## Zmienne środowiskowe i sekrety

Workflow nie wymaga sekretów do standardowego buildu. Do kroku tworzenia Release (`softprops/action-gh-release`) używane jest automatyczne `GITHUB_TOKEN` — **nie trzeba go dodawać manualnie** do Settings → Secrets.

---

## Rozszerzenie: matryca platform

Jeśli w przyszłości dodana zostanie obsługa `win-arm64`:

```yaml
strategy:
  matrix:
    runtime: [ win-x64, win-arm64 ]
steps:
  - name: Publish Hub
    run: |
      dotnet publish Hub/Hub/Hub.csproj `
        --configuration Release `
        --runtime ${{ matrix.runtime }} `
        --self-contained true `
        --output ./publish/hub-${{ matrix.runtime }}
```
