# GitHub Actions - Aplikacja Główna (Hub)

Plik konfiguracyjny `.github/workflows/build-release.yml` odpowiedzialny jest za budowanie głównej aplikacji (Hub) i jej paczkowanie.

## 1. Wyliczanie Wersji
Zanim dojdzie do publikacji, środowisko PowerShell analizuje zmienną `github.ref`. 
- Jeżeli commit pochodzi z tagu zaczynającego się od `v` (np. `v1.2.3`), wersja ustawiana jest na `1.2.3`.
- Dla ciągłych kompilacji na głównym branchu `main` / `master`, wersja tworzona jest jako `1.0.X`, gdzie `X` to numer uruchomienia na GitHub Actions.

## 2. Publikacja Hub
Projekt `Hub` zostaje skompilowany i opublikowany poleceniem:
```powershell
dotnet publish Hub/Hub.csproj -c Release -p:Version=${{ steps.version.outputs.VERSION }} -o ./publish-output
```
Wykorzystywany jest najnowszy `.NET 10.0`. Pliki trafiają do wspólnego folderu, z którego następnie aplikacja jest archiwizowana w paczce `WindowsSearch-Release.zip` razem z dostawcami (Providers) i skryptem instalacyjnym (`Install-Prerequisites.ps1`).
