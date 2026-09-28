# GitHub Actions - Dostawcy (Providers)

## 1. Uruchamianie Testów

Plik `.github/workflows/build-release.yml` jako pierwszy krok (job `test`) analizuje i uruchamia testy używając pliku `Providers/Providers.slnx`. Uruchamia on dedykowane projekty testów dla bazowych mechanizmów (`BaseProvider.Tests`) jak i dla unikalnych ustawień poszczególnych wtyczek (np. `FirefoxProvider.Tests`).
Zakończenie testów z jakimkolwiek błędem uniemożliwia publikację (zablokowany krok `build`).

## 2. Jak budowani są dostawcy?

W przypadku spełnienia warunków wydania, akcja przystępuje do budowania i generowania paczki wydaniowej:
1. **Dynamiczne Wyszukiwanie**: Skrypt napisany w środowisku PowerShell skanuje katalog `Providers/` wyszukując podfoldery reprezentujące poszczególnych dostawców.
2. **Ignorowanie Projektów Bazowych**: Foldery takie jak `DemoProvider` oraz `BaseProvider` (projekt używany jako biblioteka bazowa) są celowo pomijane przy publikacji na zewnątrz. Testy (katalogi `*.Tests`) również nie są publikowane.
3. **Kompilacja i Publikacja**: Dla każdego znalezionego katalogu dostawcy sprawdzane jest, czy istnieje odpowiedni plik projektu (np. `Providers/MyNewProvider/MyNewProvider/MyNewProvider.csproj`). Jeżeli plik istnieje, polecenie `dotnet publish` uruchamiane jest z użyciem frameworka **.NET 10.0** dla architektury docelowej (Release) z odpowiednimi flagami przekazującymi wersję (zmienna `${{ steps.version.outputs.VERSION }}`).

## 3. Na co zwrócić uwagę tworząc nowego dostawcę?
- Zawsze używaj docelowej wersji SDK (`net10.0-windows`).
- Twój główny plik `csproj` powinien znajdować się we właściwej hierarchii (np. `Providers/MyName/MyName/MyName.csproj`), aby skrypt w akcji potrafił go łatwo odnaleźć. Jeżeli chcesz zmienić hierarchię, zaktualizuj ścieżkę w `build-release.yml` wewnątrz pętli PowerShell'a.
- Upewnij się, że dodałeś projekt do `Providers.slnx` - bez tego krok automatycznego testowania pominie nową wtyczkę!
- Wszystkie pliki wynikowe są lądowane do katalogu `publish-output/Providers/[NazwaDostawcy]`, a następnie pakowane do pliku `.zip` (WindowsSearch-Release.zip).
