# Tworzenie Dostawców (Providers) - Przewodnik

## 1. Architektura Dostawcy (Provider)

W nowej architekturze projektu `WindowsSearch`, dostawcy (Providers) nie są już budowani od zera przy użyciu surowych pipe'ów i protobufa. Zamiast tego, wykorzystujemy wspólny projekt bazowy **`BaseProvider`**.

Projekt ten udostępnia abstrakcje:
- Interfejs **`IResultFinder`**, który musisz zaimplementować.
- Klasę **`BaseProviderHost`**, która zarządza cyklem życia i komunikacją (NamedPipe z użyciem `System.Text.Json`, gRPC, HTTP).
- Modele danych (`ProviderSearchRequest`, `ProviderSearchResponse`, `ProviderResultCategory`, `ProviderResultItem`, `ProviderSettings`).

## 2. Struktura Katalogów

Nowi dostawcy powinni być umieszczani w folderze `Providers/`, a nie `exampleProviders/`.

Przykładowa struktura:
```text
Providers/
├── BaseProvider/         <-- Współdzielona logika i modele
├── DemoProvider/         <-- Przykładowy dostawca referencyjny
└── MyNewProvider/
    ├── MyNewProvider.csproj
    ├── MyResultFinder.cs
    └── Program.cs
```

## 3. Krok po Kroku: Tworzenie nowego dostawcy

### Krok 1: Utworzenie projektu
W terminalu (będąc w katalogu głównym projektu):
```bash
mkdir Providers\MyNewProvider
cd Providers\MyNewProvider
dotnet new console -n MyNewProvider -f net10.0
```

### Krok 2: Dodanie referencji do BaseProvider
W pliku `MyNewProvider.csproj` dodaj referencję do `BaseProvider`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\BaseProvider\BaseProvider.csproj" />
  </ItemGroup>
</Project>
```

### Krok 3: Implementacja `IResultFinder`
Stwórz klasę `MyResultFinder.cs`:
```csharp
using BaseProvider.Abstractions;
using BaseProvider.Models;

namespace MyNewProvider;

public class MyResultFinder : IResultFinder
{
    public async Task<ProviderSearchResponse> SearchAsync(ProviderSearchRequest request, CancellationToken cancellationToken)
    {
        var response = new ProviderSearchResponse();
        var category = new ProviderResultCategory { Name = "Wyniki z MyNewProvider" };
        
        category.Items.Add(new ProviderResultItem 
        { 
            Title = $"Wynik dla: {request.Query}",
            Subtitle = "Przykładowy podtytuł"
        });
        
        response.Categories.Add(category);
        return response;
    }

    public Task<ProviderSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new ProviderSettings());
    }
}
```

### Krok 4: Konfiguracja Host'a w `Program.cs`
Dzięki użyciu `BaseProviderHost`, plik startowy ogranicza się do jednej linijki:
```csharp
using BaseProvider.Host;
using MyNewProvider;

await BaseProviderHost.RunAsync(new MyResultFinder(), args);
```

### Krok 5: Publikacja i CI/CD
Twój dostawca zostanie automatycznie spakowany przez GitHub Actions. Upewnij się tylko, że kod dostawcy znajduje się w katalogu `Providers/MyNewProvider/MyNewProvider/MyNewProvider.csproj` lub zaktualizuj ścieżkę w `build-release.yml`. Skrypt wykrywa wszystkie foldery wewnątrz `Providers/` (z pominięciem `BaseProvider` i `DemoProvider`) i kompiluje je używając .NET 10.0.
