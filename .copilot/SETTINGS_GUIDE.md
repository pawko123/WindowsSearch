# Przewodnik konfiguracji aplikacji (SETTINGS_GUIDE.md)

Ten dokument opisuje globalne ustawienia aplikacji hosta oraz sposób konfiguracji providerów.

## 1. Dostęp do ustawień

Dostęp do ustawień aplikacji będzie możliwy na dwa sposoby:
1.  **Domyślny skrót klawiszowy:** Istniejący skrót do uruchamiania wyszukiwarki.
2.  **Drugi skrót klawiszowy:** Nowy skrót, który otworzy interfejs użytkownika do zarządzania ustawieniami aplikacji i providerów.

## 2. Globalne ustawienia aplikacji (app_config.yaml)

Plik `app_config.yaml` (znajdujący się w głównym katalogu aplikacji hosta) będzie zawierał globalne ustawienia kontrolujące zachowanie aplikacji oraz wybór implementacji dla modularnych komponentów.

```yaml
# app_config.yaml
general:
  hotkey: "Ctrl+Space"
  settings_hotkey: "Ctrl+Alt+S" # Nowy skrót do otwierania ustawień

component_implementations:
  serialization_type: "protobuf" # Domyślny serializator: protobuf, json, xml
  communication_type: "named_pipe" # Domyślny kanał komunikacji: named_pipe, http, grpc
  response_handler_type: "default" # Domyślny handler odpowiedzi: default, image_caching
  # Inne globalne ustawienia, np. wygląd UI, ścieżki logów itp.
```

| Pole | Typ | Wymagane | Opis |
|---|---|---|---|
| `general.hotkey` | string | tak | Skrót klawiszowy do uruchamiania głównego okna wyszukiwania. |
| `general.settings_hotkey` | string | tak | Nowy skrót klawiszowy do otwierania okna zarządzania ustawieniami. |
| `component_implementations.serialization_type` | string | tak | Typ serializatora używany do komunikacji z providerami (musi być wspierany przez providera). |
| `component_implementations.communication_type` | string | tak | Typ kanału komunikacji używany do komunikacji z providerami (musi być wspierany przez providera). |
| `component_implementations.response_handler_type` | string | tak | Typ handlera odpowiedzialnego za przetwarzanie odpowiedzi od providerów. |

## 3. Zarządzanie ustawieniami providerów

Host będzie zarządzał ustawieniami providerów w następujący sposób:

### 3.1 Odkrywanie ustawień providera

1.  Po uruchomieniu providera, host nawiązuje z nim połączenie (zgodnie z `PROVIDER_GUIDE.md`).
2.  Host wysyła `GetSettingsRequest` do providera.
3.  Provider odpowiada `GetSettingsResponse`, zawierającym listę `SettingDescriptor`. Każdy deskryptor zawiera `key`, `display_name`, `description`, `current_value` oraz `value_type` (np. `STRING`, `INT`, `BOOL`, `FILE_PATH`, `DIRECTORY_PATH`).
4.  Jeśli provider nie zwróci deskryptora dla danego klucza, host zakłada, że provider używa wartości domyślnej.
5.  Host może zapisywać te ustawienia w swoim wewnętrznym pliku konfiguracyjnym (np. rozszerzając `app_config.yaml` lub w osobnym pliku `provider_settings.yaml`).

### 3.2 Interfejs użytkownika (UI) dla ustawień

Nowy skrót klawiszowy `general.settings_hotkey` otworzy dedykowane okno ustawień, które umożliwi:
*   Konfigurację globalnych ustawień aplikacji (np. motyw, język, domyślne implementacje komponentów).
*   Przeglądanie listy zainstalowanych providerów.
*   Dla każdego providera:
    *   Włączanie/wyłączanie (`enabled` z `settings.yaml`).
    *   Modyfikację pól z `settings.yaml` (np. `priority`).
    *   Edycję dynamicznych ustawień zwróconych przez `GetSettingsResponse`, z odpowiednimi kontrolkami UI w zależności od `value_type` (np. pole tekstowe dla `STRING`, checkbox dla `BOOL`, selektor plików dla `FILE_PATH`).
*   Zapisywanie zmian w konfiguracji hosta oraz, jeśli to konieczne, ponowne uruchamianie providerów w celu zastosowania nowych ustawień.

## 4. Wybór implementacji komponentów

Host będzie dynamicznie wybierał implementacje `ISerializer`, `ICommunicationChannel` i `IResponseHandler` na podstawie wartości `serialization_type`, `communication_type` i `response_handler_type` w `app_config.yaml`.
*   Jeśli `communication_type` w `app_config.yaml` to `named_pipe`, host użyje `NamedPipeChannel`.
*   Jeśli `serialization_type` to `json`, host użyje `JsonSerializer`.
*   Host sprawdzi również `capabilities` z `settings.yaml` providera, aby upewnić się, że wybrana implementacja jest wspierana przez dany provider. Jeśli nie, host może:
    *   Użyć domyślnej, kompatybilnej implementacji (np. zawsze `protobuf` over `named_pipe` jako fallback).
    *   Wyświetlić ostrzeżenie w UI.
    *   Zignorować providera.
