# GitHub Actions - Dostawcy (Providers)

## 1. Uruchamianie Testów

Plik `.github/workflows/build-release.yml` analizuje i uruchamia testy używając pliku `Providers/Providers.slnx`. Polecenie `dotnet test` buduje i uruchamia dedykowane projekty testów dla bazowych mechanizmów (`BaseProvider.Tests`) jak i dla unikalnych ustawień poszczególnych wtyczek (np. `FirefoxProvider.Tests`).
Zakończenie testów z jakimkolwiek błędem przerywa cały proces i uniemożliwia ewentualną publikację.

## 2. Jak budowani są dostawcy?

W przypadku spełnienia warunków wydania, akcja przystępuje do budowania i generowania paczki wydaniowej:
1. **Dynamiczne Wyszukiwanie**: Skrypt napisany w środowisku PowerShell skanuje katalog `Providers/` wyszukując podfoldery reprezentujące poszczególnych dostawców.
2. **Ignorowanie Projektów Bazowych**: Foldery takie jak `DemoProvider` oraz `BaseProvider` (projekt używany jako biblioteka bazowa) są celowo pomijane przy publikacji na zewnątrz. Testy (katalogi `*.Tests`) również nie są publikowane.
3. **Kompilacja i Publikacja**: Dla każdego znalezionego katalogu dostawcy sprawdzane jest, czy istnieje odpowiedni plik projektu. Jeżeli plik istnieje, polecenie `dotnet publish` uruchamiane jest z dodatkową flagą `--no-build` (ponieważ faza testów skompilowała już kod). Używamy **.NET 10.0** dla architektury docelowej (Release) z odpowiednimi flagami przekazującymi wersję.

## 3. Na co zwrócić uwagę tworząc nowego dostawcę?
- Zawsze używaj docelowej wersji SDK (`net10.0-windows`).
- Twój główny plik `csproj` powinien znajdować się we właściwej hierarchii (np. `Providers/MyName/MyName/MyName.csproj`), aby skrypt w akcji potrafił go łatwo odnaleźć. Jeżeli chcesz zmienić hierarchię, zaktualizuj ścieżkę w `build-release.yml` wewnątrz pętli PowerShell'a.
- Upewnij się, że dodałeś projekt do `Providers.slnx` - bez tego krok automatycznego testowania pominie nową wtyczkę!
- Wszystkie pliki wynikowe są lądowane do katalogu `publish-output/Providers/[NazwaDostawcy]`, a następnie pakowane do pliku `.zip` (WindowsSearch-Release.zip).
