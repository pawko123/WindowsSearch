# GitHub Actions — Build Providers

## Cel

Workflow buduje wszystkie providery znajdujące się w katalogu `/exampleProviders/`. Każdy provider jest budowany niezależnie (strategia matrycowa) i uploadowany jako osobny artefakt ZIP. Przy tagu `v*` wszystkie ZIPy dołączane są do GitHub Release.

---

## Zakładana struktura katalogu providerów

```
/exampleProviders/
├── FirefoxProvider/                        ← korzeń solucji
│   ├── FirefoxProvider.sln                 ← otwierany w Rider / VS
│   ├── FirefoxProvider/                    ← projekt EXE
│   │   ├── FirefoxProvider.csproj
│   │   └── Program.cs
│   └── FirefoxProvider.Tests/              ← opcjonalny projekt testowy
│       └── FirefoxProvider.Tests.csproj
├── ExampleProvider/
│   ├── ExampleProvider.sln
│   └── ExampleProvider/
│       ├── ExampleProvider.csproj
│       └── Program.cs
└── MyPythonProvider/                       ← przykład innego języka (patrz sekcja niżej)
    ├── main.py
    └── build.sh
```

Konwencja wymagana przez workflow:
- Każdy provider ma **własny podkatalog** w `/exampleProviders/`.
- C# provider zawiera plik `{Nazwa}.sln` bezpośrednio w katalogu providera.
- Projekt EXE leży w podkatalogu o tej samej nazwie co solucja.
- Nazwa katalogu = nazwa providera używana jako nazwa artefaktu.

---

## Strategia matrycowa — dwa warianty

### Wariant A: Dynamiczna macierz (zalecany — skaluje się automatycznie)

Workflow sam wykrywa podkatalogi w `/exampleProviders/` i buduje macierz na bieżąco. Dodanie nowego providera nie wymaga edycji workflow.

### Wariant B: Statyczna macierz

Lista providerów wpisana ręcznie w YAML. Prosta, brak zewnętrznych narzędzi, ale wymaga edycji workflow przy każdym nowym providerze.

Oba warianty opisane są jako osobne pliki poniżej.

---

## Triggery

| Trigger | Kiedy uruchamiany |
|---|---|
| `push` → `main` / `master` | Każdy push do gałęzi `main` lub `master` (w tym merge PR) |
| `push` → tag `v*` | Pchanie tagu — dodatkowo dołącza do GitHub Release |

> **Dlaczego brak `pull_request`?** Merge PR-a generuje zdarzenie `push` na gałęzi docelowej — workflow uruchomi się automatycznie bez osobnego triggera `pull_request`.

---

## Plik workflow (Wariant A — dynamiczna macierz): `.github/workflows/build-providers.yml`

```yaml
name: Build Providers

on:
  push:
    branches: [ main, master ]   # merge PR → push na main/master; brak osobnego pull_request
    tags:    [ "v*" ]

jobs:
  # ── Krok 1: wykryj providery i zbuduj macierz ──────────────────────────────
  detect-providers:
    name: Detect provider list
    runs-on: ubuntu-latest
    outputs:
      matrix: ${{ steps.set-matrix.outputs.matrix }}

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      # Znajdź wszystkie bezpośrednie podkatalogi exampleProviders i zamień na JSON array
      - name: Build provider matrix
        id: set-matrix
        run: |
          providers=$(ls -d exampleProviders/*/ | xargs -I{} basename {} | jq -R . | jq -sc .)
          echo "matrix={\"provider\":$providers}" >> "$GITHUB_OUTPUT"
          echo "Detected providers: $providers"

  # ── Krok 2: buduj każdy provider niezależnie ───────────────────────────────
  build-provider:
    name: Build ${{ matrix.provider }}
    needs: detect-providers
    runs-on: windows-latest        # win-x64 target; zmień na ubuntu-latest jeśli provider nie wymaga Windows
    strategy:
      matrix: ${{ fromJson(needs.detect-providers.outputs.matrix) }}
      fail-fast: false             # nieudany jeden provider nie blokuje pozostałych

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      # ── C# / .NET provider ──────────────────────────────────────────────────
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"

      - name: Restore NuGet packages
        # Wskaż plik .sln — Rider/VS używa tej samej ścieżki
        run: dotnet restore exampleProviders/${{ matrix.provider }}/${{ matrix.provider }}.sln

      # ── Testy jednostkowe przed buildem produkcyjnym ───────────────────────
      - name: Run unit tests
        run: |
          dotnet test exampleProviders/${{ matrix.provider }}/${{ matrix.provider }}.sln `
            --configuration Release `
            --no-restore `
            --logger "trx;LogFileName=test-results.trx"
        shell: pwsh

      - name: Publish provider (self-contained)
        run: |
          # Projekt EXE leży w podkatalogu o tej samej nazwie co solucja
          $csproj = "exampleProviders/${{ matrix.provider }}/${{ matrix.provider }}/${{ matrix.provider }}.csproj"
          dotnet publish $csproj `
            --configuration Release `
            --runtime win-x64 `
            --self-contained true `
            -p:PublishSingleFile=true `
            --output ./publish/${{ matrix.provider }}
        shell: pwsh

      # ── Pakowanie output ────────────────────────────────────────────────────
      - name: Zip provider output
        run: Compress-Archive -Path ./publish/${{ matrix.provider }}/* -DestinationPath ./${{ matrix.provider }}.zip
        shell: pwsh

      # ── Upload artefaktu ────────────────────────────────────────────────────
      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: ${{ matrix.provider }}-win-x64
          path: ./${{ matrix.provider }}.zip
          retention-days: 30

  # ── Krok 3: GitHub Release (tylko przy tagu v*) ────────────────────────────
  release:
    name: Create GitHub Release
    if: startsWith(github.ref, 'refs/tags/v')
    needs: build-provider
    runs-on: ubuntu-latest

    steps:
      - name: Download all provider artifacts
        uses: actions/download-artifact@v4
        with:
          path: ./release-assets

      - name: Flatten ZIPs into one directory
        run: find ./release-assets -name "*.zip" -exec cp {} ./release-assets/ \;

      - name: Create Release with all provider ZIPs
        uses: softprops/action-gh-release@v2
        with:
          files: ./release-assets/*.zip
          generate_release_notes: true
```

---

## Plik workflow (Wariant B — statyczna macierz): `.github/workflows/build-providers.yml`

```yaml
name: Build Providers

on:
  push:
    branches: [ main, master ]   # merge PR → push na main/master; brak osobnego pull_request
    tags:    [ "v*" ]

jobs:
  build-provider:
    name: Build ${{ matrix.provider }}
    runs-on: windows-latest
    strategy:
      matrix:
        provider:
          - FirefoxProvider    # ← ręcznie dodawaj kolejne providery tutaj
          - ExampleProvider
      fail-fast: false

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"

      - name: Restore
        run: dotnet restore exampleProviders/${{ matrix.provider }}/${{ matrix.provider }}.sln

      - name: Run unit tests
        run: |
          dotnet test exampleProviders/${{ matrix.provider }}/${{ matrix.provider }}.sln `
            --configuration Release `
            --no-restore `
            --logger "trx;LogFileName=test-results.trx"
        shell: pwsh

      - name: Publish
        run: |
          $csproj = "exampleProviders/${{ matrix.provider }}/${{ matrix.provider }}/${{ matrix.provider }}.csproj"
          dotnet publish $csproj `
            --configuration Release `
            --runtime win-x64 `
            --self-contained true `
            -p:PublishSingleFile=true `
            --output ./publish/${{ matrix.provider }}
        shell: pwsh

      - name: Zip
        run: Compress-Archive -Path ./publish/${{ matrix.provider }}/* -DestinationPath ./${{ matrix.provider }}.zip
        shell: pwsh

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: ${{ matrix.provider }}-win-x64
          path: ./${{ matrix.provider }}.zip

  release:
    if: startsWith(github.ref, 'refs/tags/v')
    needs: build-provider
    runs-on: ubuntu-latest
    steps:
      - uses: actions/download-artifact@v4
        with:
          path: ./release-assets
      - uses: softprops/action-gh-release@v2
        with:
          files: ./release-assets/**/*.zip
          generate_release_notes: true
```

---

## Adaptacja workflow dla innych języków

Zmieniasz tylko kroki **Restore** i **Publish** — reszta workflow (checkout, zip, upload, release) pozostaje identyczna.

### Python (PyInstaller)

```yaml
- name: Setup Python
  uses: actions/setup-python@v5
  with:
    python-version: "3.12"

- name: Install dependencies
  run: pip install -r exampleProviders/${{ matrix.provider }}/requirements.txt pyinstaller

- name: Publish (PyInstaller → single EXE)
  run: |
    cd exampleProviders/${{ matrix.provider }}
    pyinstaller --onefile --name ${{ matrix.provider }} main.py
    cp dist/${{ matrix.provider }}.exe ../../publish/${{ matrix.provider }}/
```

### Go

```yaml
- name: Setup Go
  uses: actions/setup-go@v5
  with:
    go-version: "1.22"

- name: Build (Go)
  run: |
    cd exampleProviders/${{ matrix.provider }}
    GOOS=windows GOARCH=amd64 go build -o ../../publish/${{ matrix.provider }}/${{ matrix.provider }}.exe .
```

### Rust

```yaml
- name: Setup Rust
  uses: dtolnay/rust-toolchain@stable
  with:
    targets: x86_64-pc-windows-msvc

- name: Build (Cargo)
  run: |
    cd exampleProviders/${{ matrix.provider }}
    cargo build --release --target x86_64-pc-windows-msvc
    cp target/x86_64-pc-windows-msvc/release/*.exe ../../publish/${{ matrix.provider }}/
```

---

## Wymagany plik `.csproj` providera (C#)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <RuntimeIdentifiers>win-x64</RuntimeIdentifiers>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- Grpc.Tools wygeneruje kod C# z .proto podczas buildu -->
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Google.Protobuf" Version="3.*" />
    <PackageReference Include="Grpc.Tools" Version="2.*" PrivateAssets="All" />
  </ItemGroup>

  <!-- Projekt leży w: exampleProviders/{Nazwa}/{Nazwa}/{Nazwa}.csproj -->
  <!-- Proto leży w:   proto/search.proto (korzeń repo) → 3 poziomy wyżej  -->
  <ItemGroup>
    <Protobuf Include="..\..\..\proto\search.proto" GrpcServices="None" />
  </ItemGroup>
</Project>
```

> `GrpcServices="None"` — generujemy tylko klasy wiadomości Protobuf, nie stub gRPC (komunikacja odbywa się przez Named Pipe, nie gRPC).

---

## Artefakt — co zawiera ZIP providera

```
FirefoxProvider-win-x64.zip
└── FirefoxProvider.exe         ← (PublishSingleFile=true) wszystko w jednym pliku
```

Lub przy `PublishSingleFile=false`:

```
FirefoxProvider-win-x64.zip
└── FirefoxProvider.exe
└── Google.Protobuf.dll
└── ...
```

Po pobraniu ZIP → wypakować do dowolnego katalogu → skopiować `*.exe` do folderu `providers/` hostowanej aplikacji (obok `*_provider.yaml`).
