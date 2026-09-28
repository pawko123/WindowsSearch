# GitHub Actions - CI/CD i Wydania (Build & Release)

Plik konfiguracyjny `.github/workflows/build-release.yml` to ujednolicone środowisko CI/CD (Continuous Integration & Continuous Deployment). Odpowiada za testowanie całego repozytorium (Hub, Providers, Shared) oraz budowanie i paczkowanie oprogramowania w przypadku wydań.

## 1. Testowanie i Budowanie (Uruchamiane zawsze)
Akcja uruchamia się **bezwarunkowo** jako pojedynczy proces (`Job: build-and-test`) na każdy `push` oraz `pull_request` do gałęzi `main`.
Skrypt używa polecenia `dotnet test` z parametrem `-c Release` niezależnie dla 3 dedykowanych rozwiązań:
- `Shared/Shared.slnx`
- `Hub/Hub.slnx`
- `Providers/Providers.slnx`

Z racji tego, że polecenie `test` pod spodem automatycznie kompiluje kod, repozytorium jest budowane tylko raz. Jeśli którykolwiek test zakończy się niepowodzeniem, cała akcja natychmiast przerywa działanie.

## 2. Publikacja (Uruchamiana warunkowo)
Kroki odpowiedzialne za paczkowanie (Publish) oraz Release uruchamiają się na wybudowanym już kodzie w ramach tego samego procesu.

Zadanie to jest chronione warunkiem `if` i dochodzi do skutku wyłącznie, jeżeli testy zakończyły się sukcesem **oraz** wiadomość commitu zawiera flagę `--build` (lub jeśli przypisano nowy tag).

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
