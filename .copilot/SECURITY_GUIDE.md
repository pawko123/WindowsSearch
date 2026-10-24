# Przewodnik Bezpieczeństwa i Szyfrowania (Security Guide)

## 1. Wstęp i Architektura Bezpieczeństwa IPC
Aplikacja **WindowsSearch** wykorzystuje architekturę wieloprocesową. Główny proces UI (`Hub`) komunikuje się z wieloma procesami potomnymi (`Providers` m.in. Chrome, Firefox, VSCode, JetBrains) za pośrednictwem lokalnych mechanizmów IPC: **Named Pipes**, **HTTP** (loopback) oraz **gRPC** (loopback).

Do ochrony danych w tranzycie oraz zapobiegania atakom ze strony innych aplikacji działających w systemie zaimplementowano warstwę kryptograficzną:
1. **Szyfrowanie Wire Payload**: Każda ramka przesyłana między Hubem a Dostawcami (oraz kanał kontrolny benchmarku) jest hermetyzowana w binarną kopertę (`WireEnvelopeCodec`).
2. **Ochrona Integralności (AEAD / Encrypt-then-MAC)**: Zapewnienie poufności oraz autentyczności nagłówków metadanych (AAD - *Associated Authenticated Data*).
3. **Zarządzanie Kluczami DPAPI**: Klucz główny jest chroniony przez mechanizm Windows DPAPI (`DataProtectionScope.CurrentUser`), uniemożliwiając odczytanie go przez procesy innych użytkowników i eliminując przekazywanie sekretów w argumentach CLI (niewidoczne w Task Managerze).
4. **Hartowanie Endpointów HTTP**: Ochrona przed atakami typu *DNS Rebinding* i *Cross-Origin Fetch* z przeglądarek internetowych.
5. **Ochrona Kanału Benchmarku**: Kanał kontrolny potoku nazwanego `hub_bench_control` jest w pełni szyfrowany przy użyciu `Aes256Gcm` i klucza DPAPI.

---

## 2. Obsługiwane Algorytmy Szyfrowania (`EncryptionKind`)

Hub oraz Dostawcy obsługują macierz algorytmów zoptymalizowaną do badań akademickich:

| Wartość Enum | Identyfikator YAML | Długość Klucza | Typ Szyfru | Narzut Nagłówka | Zastosowanie Badawcze |
|---|---|---|---|---|---|
| `None` (0) | `none` | 0 bitów | Brak (Plaintext) | 2 bajty | Punkt odniesienia (Baseline), zerowy narzut CPU |
| `Aes128Gcm` (1) | `aes128_gcm` | 128 bitów (16 B) | AEAD (Hardware AES-NI) | 30 bajtów | Wpływ długości klucza na przepustowość AES |
| `Aes256Gcm` (2) | `aes256_gcm` | 256 bitów (32 B) | AEAD (Hardware AES-NI) | 30 bajtów | Standard branżowy (domyślny tryb produkcyjny) |
| `Aes256Ccm` (3) | `aes256_ccm` | 256 bitów (32 B) | AEAD (Hardware AES-NI) | 30 bajtów | Porównanie CCM vs GCM w środowisku desktop |
| `Aes256CbcHmac` (4) | `aes256_cbc_hmac` | 256 bitów AES + 256 bitów HMAC | Złożony (Encrypt-then-MAC) | 50 bajtów | Tradycyjny tryb złożony vs nowoczesne AEAD |
| `ChaCha20Poly1305` (5) | `cha_cha20_poly1305` | 256 bitów (32 B) | AEAD (Szyfr strumieniowy) | 30 bajtów | Porównanie wydajności czysto programowej vs sprzętowe AES-NI |

---

## 3. Binarny Format Koperty Sieciowej (`WireEnvelopeCodec`)

Każda ramka transportowa (Named Pipe, HTTP body, gRPC message payload) posiada ustandaryzowaną strukturę binarną:

```
Tryb None (Brak szyfrowania):
+----------------+--------------------+-------------------------------------------+
| Version (0x01) | EncryptionKind (0) | Plaintext Serialized Message (Bytes 2+)   |
| [1 bajt]       | [1 bajt]           | [N bajtów]                                |
+----------------+--------------------+-------------------------------------------+

Tryby AEAD (Aes128Gcm, Aes256Gcm, Aes256Ccm, ChaCha20Poly1305):
+----------------+--------------------+-------------------+--------------------+------------------------+
| Version (0x01) | EncryptionKind (K) | Nonce (IV)        | Auth Tag (MAC)     | Ciphertext             |
| [1 bajt]       | [1 bajt]           | [12 bajtów]       | [16 bajtów]        | [N bajtów]             |
+----------------+--------------------+-------------------+--------------------+------------------------+
|<------- AAD (2 bajty) ------------->|

Tryb Złożony (Aes256CbcHmac):
+----------------+--------------------+-------------------+--------------------------------+------------------------+
| Version (0x01) | EncryptionKind (4) | IV (CBC)          | HMAC-SHA256 Tag                | Ciphertext (PKCS7)     |
| [1 bajt]       | [1 bajt]           | [16 bajtów]       | [32 bajty]                     | [N + Padding bajtów]   |
+----------------+--------------------+-------------------+--------------------------------+------------------------+
|<----------------- Uwierzytelniane przez HMAC (Header + IV + Ciphertext) ---------------------------------------------->|
```

### Powiązanie AAD (Associated Authenticated Data)
Dla trybów AEAD pierwsze 2 bajty nagłówka (`Version` + `EncryptionKind`) są przekazywane do kryptograficznego silnika AEAD jako dane uwierzytelnione AAD. Zapobiega to atakom typu *type-confusion* i *downgrade attacks*, w których atakujący mógłby manipulować bajtem algorytmu w nagłówku ramki.

W przypadku `Aes256CbcHmac`, HMAC-SHA256 jest obliczany sekwencyjnie nad nagłówkiem (2 bajty), IV (16 bajtów) oraz szyfrogramem (*Encrypt-then-MAC*), a weryfikacja następuje w czasie stałym (`CryptographicOperations.FixedTimeEquals`), eliminując podatności typu *timing attack* i *padding oracle*.

### Abstrakcja Kodeków: `ClientMessageCodec` oraz `TransportMessageCodec`
W celu uniknięcia duplikacji logiki kryptograficznej i pomiarowej w poszczególnych klientach i transportach wprowadzono dwie wyspecjalizowane fasady:
1. **Strona Huba (`ClientMessageCodec`)**:
   - Klasa `ClientMessageCodec` (`Hub.Services.Providers.Transports`) udostępnia metody `EncodeRequest` i `DecodeResponse`.
   - Odpowiada za: serializację modelu, pomiar czasu serializacji CPU, szyfrowanie koperty (`WireEnvelopeCodec`), pomiar czasu szyfrowania, zliczanie bajtów ramki, deszyfrowanie, weryfikację integralności, pomiar czasu deszyfrowania oraz deserializację modelu.
   - Eliminuje powtarzalny kod w `NamedPipeProviderClient`, `HttpProviderClient` oraz `GrpcProviderClient` (włączając delegaty `Marshaller` gRPC).
2. **Strona Dostawcy (`TransportMessageCodec`)**:
   - Klasa `TransportMessageCodec` (`BaseProvider.Transports`) hermetyzuje serializację i szyfrowanie odpowiedzi oraz deszyfrowanie i deserializację zapytań dla wszystkich transportów serwera.
   - Poszczególne transporty (`NamedPipeProviderTransport`, `HttpProviderTransport`, `GrpcProviderTransport`) operują wyłącznie na gotowych bajtach koperty sieciowej.

---

## 4. Zarządzanie Kluczami i Windows DPAPI (`KeyManager`)

Do zarządzania kluczami kryptograficznymi wykorzystywana jest klasa `KeyManager` (`Shared/WindowsSearch.Common/Security`):
1. **Generowanie i Magazynowanie**:
   - Klucz główny (32 bajty losowane kryptograficznie przez `RandomNumberGenerator`) jest szyfrowany funkcją Windows DPAPI:
     `ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser)`.
   - Zaszyfrowany plik jest przechowywany w lokalnym katalogu profilu użytkownika:
     `%LOCALAPPDATA%\WindowsSearch\security.dat`.
   - Plik ten może odszyfrować wyłącznie proces uruchomiony w kontekście bezpieczeństwa tego samego zalogowanego użytkownika Windows.
2. **Wyprowadzanie Podkluczy (HKDF-SHA256)**:
   - Z klucza głównego wyprowadzane są niezależne, deterministyczne podklucze dla poszczególnych algorytmów za pomocą `HKDF.DeriveKey` z jawnym parametrem kontekstowym (`info`):
     - `"WindowsSearch-Aes128Gcm"` (16 bajtów)
     - `"WindowsSearch-Aes256Gcm"` (32 bajty)
     - `"WindowsSearch-Aes256Ccm"` (32 bajty)
     - `"WindowsSearch-ChaCha20Poly1305"` (32 bajty)
     - `"WindowsSearch-Aes256Cbc"` (64 bajty: pierwsze 32 bajty = AES, kolejne 32 bajty = HMAC)
3. **Środowiska Testowe i CI**:
   - `KeyManager` sprawdza obecność zmiennej środowiskowej `WINDOWS_SEARCH_MASTER_KEY`. Jeśli jest ustawiona (np. 64 znaki hex lub 32 bajty base64), używa jej bezpośrednio, co umożliwia bezproblemowe uruchamianie testów jednostkowych w odizolowanych kontenerach CI.

---

## 5. Hartowanie Transportu HTTP i Anti-DNS Rebinding

Dostawcy uruchamiający transport HTTP (`HttpProviderTransport`) nasłuchują na lokalnym interfejsie `127.0.0.1`. Aby zapobiec atakom ze strony złośliwych witryn internetowych (odwiedzanych przez użytkownika w przeglądarce):
1. **Weryfikacja Nagłówka `Host`**:
   Każde żądanie HTTP weryfikuje nagłówek `Host`. Jeżeli nagłówek nie zaczyna się od `127.0.0.1` lub `localhost`, serwer natychmiast zwraca `403 Forbidden`. Zapobiega to atakom typu *DNS Rebinding*, gdzie domena zewnętrzna wskazuje na IP `127.0.0.1`.
2. **Blokada Nagłówka `Origin`**:
   Jeżeli żądanie zawiera nagłówek `Origin` (co oznacza wywołanie JavaScript `fetch()` lub `XMLHttpRequest` ze skryptu w przeglądarce), serwer odrzuca połączenie ze statusem `403 Forbidden`.
3. **Walidacja Integralności Ramki**:
   Przesłanie zmodyfikowanego szyfrogramu kończy się niepowodzeniem deszyfrowania AEAD / HMAC i natychmiastowym przerwaniem obsługi.

---

## 6. Ochrona Kanału Kontrolnego Benchmarku

Kanał komunikacji między aplikacją CLI `WindowsSearch.Benchmark.Cli` a procesem `Hub` oparty na potoku nazwanym `hub_bench_control`:
1. Został przeniesiony z protokołu tekstowego na binarną hermetyzację ramkową (`PipeFrameCodec`).
2. Każde polecenie (`BenchmarkControlCommand`) oraz raport pomiarowy (`BenchmarkControlResponse`) są w pełni szyfrowane algorytmem `Aes256Gcm` z użyciem klucza DPAPI.
3. Próba przesłania nieautoryzowanego polecenia przez inny proces kończy się błędem `CryptographicException` i natychmiastowym rozłączeniem potoku.

---

## 7. Weryfikacja i Inspekcja Logów (Hex Dump Logging)

W trybie diagnostycznym (`LogLevel: Debug` lub `Info`) aplikacja loguje zrzuty ramek zaszyfrowanych:
- Format logu: `[<EncryptionKind> envelope: <N> bytes | Hex: <HEX_PREVIEW>...]`
- Przykład w pliku logu dostawcy (`chromeLogs_...log`, `hubLogs_...log`):
  ```text
  [2026-10-24 12:30:15.123] [DEBUG] [BaseProvider] [Encrypted incoming payload] [Aes256Gcm envelope: 154 bytes | Hex: 01026AF419D410A1BC02E19830FA09...]
  [2026-10-24 12:30:15.125] [DEBUG] [BaseProvider] [Encrypted outgoing payload] [Aes256Gcm envelope: 1248 bytes | Hex: 01028BD1765AC4E28192138ABF0012...]
  ```
Można łatwo potwierdzić obecność nagłówka wersji `01` i identyfikatora algorytmu (`02` dla `Aes256Gcm`), losowego wektora inicjalizującego / nonce oraz brak czytelnych ciągów znaków (np. JSON `{` `0x7B`) w ciele wiadomości na łączu.
