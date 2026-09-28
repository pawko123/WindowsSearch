# GitHub Actions - CI/CD i Wydania (Build & Release)

Plik konfiguracyjny `.github/workflows/build-release.yml` to ujednolicone środowisko CI/CD (Continuous Integration & Continuous Deployment). Odpowiada za testowanie całego repozytorium (Hub, Providers, Shared) oraz budowanie i paczkowanie oprogramowania w przypadku wydań.

## 1. Uruchamianie Testów (Job: test)
Testy uruchamiane są **bezwarunkowo** na każdy `push` oraz `pull_request` do gałęzi `main`.
Skrypt używa polecenia `dotnet test` niezależnie dla 3 dedykowanych plików rozwiązań:
- `Shared/Shared.slnx`
- `Hub/Hub.slnx`
- `Providers/Providers.slnx`

Testy izolują interakcje ze środowiskiem (np. dostęp do dysku za pomocą biblioteki `TestableIO.System.IO.Abstractions`) oraz w pełni sprawdzają zasady walidacyjne przy użyciu aktualnych modeli produkcyjnych.

## 2. Budowanie i Publikacja (Job: build)
Ten proces jest **zależny od testów** (`needs: test`). Oznacza to, że paczka instalacyjna nigdy nie zostanie wygenerowana, jeżeli chociaż jeden test w repozytorium zakończy się niepowodzeniem.

Zadanie budowania uruchamia się wyłącznie, jeżeli wiadomość commitu zawiera flagę `--build` lub jeśli przypisano nowy tag (np. `v1.2.3`).

### Wyliczanie Wersji
Zanim dojdzie do publikacji, środowisko PowerShell analizuje zmienną `github.ref`. 
- Jeżeli commit pochodzi z tagu zaczynającego się od `v` (np. `v1.2.3`), wersja ustawiana jest na `1.2.3`.
- Dla ciągłych kompilacji na głównym branchu `main`, wersja tworzona jest jako `1.0.X`, gdzie `X` to numer uruchomienia.

### Publikacja
Projekt rozwiązany jest przy pomocy pliku `Hub/Hub.slnx` a następnie publikowany w procesie:
```powershell
dotnet publish Hub/Hub/Hub.csproj -c Release --no-build -p:Version=${{ steps.version.outputs.VERSION }} -o ./publish-output
```
Pliki lądują we wspólnym folderze, a gotowa aplikacja (wraz ze skryptem `Install-Prerequisites.ps1` i bibliotekami dostawców) zostaje zarchiwizowana do pliku `WindowsSearch-<Wersja>.zip`. Menedżer sam zajmuje się tworzeniem releasów w interfejsie GitHuba.
