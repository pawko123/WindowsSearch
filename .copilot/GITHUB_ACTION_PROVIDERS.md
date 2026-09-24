# GitHub Actions - Dostawcy (Providers)

## 1. Jak budowani są dostawcy?

W pliku `.github/workflows/build-release.yml` zdefiniowany jest proces ciągłej integracji i kompilacji całego projektu, w tym wszystkich wtyczek / dostawców.

Kluczowe kroki podczas wydania (build-release) dla dostawców:
1. **Dynamiczne Wyszukiwanie**: Skrypt napisany w środowisku PowerShell skanuje katalog `Providers/` wyszukując podfoldery reprezentujące poszczególnych dostawców.
2. **Ignorowanie Projektów Bazowych**: Foldery takie jak `DemoProvider` (czysty przykład) oraz `BaseProvider` (projekt używany jako biblioteka bazowa) są celowo pomijane.
3. **Kompilacja i Publikacja**: Dla każdego znalezionego katalogu dostawcy sprawdzane jest, czy istnieje odpowiedni plik projektu (np. `Providers/MyNewProvider/MyNewProvider/MyNewProvider.csproj`). Jeżeli plik istnieje, polecenie `dotnet publish` uruchamiane jest z użyciem frameworka **.NET 10.0** dla architektury docelowej (Release) z odpowiednimi flagami przekazującymi wersję (zmienna `${{ steps.version.outputs.VERSION }}`).

## 2. Na co zwrócić uwagę tworząc nowego dostawcę?
- Zawsze używaj docelowej wersji SDK (`net10.0-windows`).
- Twój główny plik `csproj` powinien znajdować się we właściwej hierarchii (np. `Providers/MyName/MyName/MyName.csproj`), aby skrypt w akcji potrafił go łatwo odnaleźć. Jeżeli chcesz zmienić hierarchię, zaktualizuj ścieżkę w `build-release.yml` wewnątrz pętli PowerShell'a.
- Wszystkie pliki wynikowe są lądowane do katalogu `publish-output/Providers/[NazwaDostawcy]`, a następnie pakowane do pliku `.zip` (WindowsSearch-Release.zip).
