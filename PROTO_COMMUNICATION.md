# Zadanie: Definicja kontraktu Protobuf i warstwy serializacji C#

## Plik `search.proto`

### `SearchRequest` — zapytanie wysyłane przez hosta do providera

| Pole | Typ | Opis |
|---|---|---|
| `query` | `string` | Wpisany przez użytkownika tekst wyszukiwania |
| `limit` | `int32` | Maksymalna liczba wyników do zwrócenia |
| `settings` | `map<string, string>` | Konfiguracja providera odczytana z sekcji `settings` pliku `{nazwa}_provider.yaml`; provider traktuje mapę jako read-only (np. `browser_path`, `profile_path`) |

Mapa `settings` jest wypełniana przez hosta przy każdym zapytaniu — provider nigdy jej nie odsyła z powrotem.

---

### `ResultItem` — pojedynczy wynik wyszukiwania

| Pole | Typ | Opis |
|---|---|---|
| `title` | `string` | Główna etykieta wyniku wyświetlana w UI |
| `subtitle` | `string` | Dodatkowy opis (np. URL, ścieżka pliku) |
| `score` | `float` | Ocena trafności — host może użyć do dodatkowego sortowania w obrębie kategorii |
| `action_path` | `string` | Ścieżka do pliku wykonywalnego lub zasobu uruchamianego po wybraniu wyniku |
| `action_args` | `repeated string` | Lista argumentów przekazywanych do `action_path` przy uruchomieniu; host używa `ProcessStartInfo.ArgumentList` (.NET 5+) dla poprawnego escapowania |
| `icon_path` | `string` | Ścieżka do pliku ikony (PNG/ICO/SVG) wyświetlanej przy tym wyniku; host ładuje obraz samodzielnie |

---

### `ResultCategory` — kategoria grupująca powiązane wyniki

Provider może zwracać wyniki pogrupowane w kategorie (np. Firefox zwraca osobno "Historia" i "Zakładki"). Każda kategoria jest wyświetlana w UI jako osobny blok z nagłówkiem i ikoną.

| Pole | Typ | Opis |
|---|---|---|
| `name` | `string` | Wyświetlana nazwa kategorii (np. `"Historia"`, `"Zakładki"`) |
| `icon_path` | `string` | Ścieżka do ikony wyświetlanej przy nagłówku tej kategorii w UI (np. ikona Firefoxa) |
| `items` | `repeated ResultItem` | Wyniki należące do tej kategorii, w kolejności zwracanej przez providera |

Provider bez potrzeby kategoryzacji zwraca jedną `ResultCategory` z generyczną nazwą.

---

### `SearchResponse` — odpowiedź providera

| Pole | Typ | Opis |
|---|---|---|
| `categories` | `repeated ResultCategory` | Lista kategorii wyników; kolejność elementów decyduje o kolejności wyświetlania bloków w obrębie tego providera |

---

## Klasa pomocnicza `PipeProtocol` (C#)

Dwie metody generyczne obsługujące komunikację przez NamedPipe:

```csharp
Task SendMessageAsync<T>(Stream stream, T message) where T : IMessage<T>
Task<T> ReceiveMessageAsync<T>(Stream stream) where T : IMessage<T>, new()
```

### Wzorzec Length-Prefix

Każda wiadomość Protobuf poprzedzona jest 4-bajtowym nagłówkiem (big-endian `int32`) zawierającym długość serializowanej wiadomości w bajtach. Odbiorca:
1. Czyta dokładnie 4 bajty → odczytuje długość `N`
2. Czyta dokładnie `N` bajtów → deserializuje wiadomość Protobuf

Gwarantuje to kompletny odczyt wiadomości z bufora pipe'a przed deserializacją, niezależnie od fragmentacji danych w buforze.
