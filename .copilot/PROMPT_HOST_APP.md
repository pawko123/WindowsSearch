# Aplikacja Główna (Host App / Hub) - Przewodnik

## 1. Zmiana Architektury Hosta
Główna aplikacja, potocznie nazywana **Hub** (znajdująca się w folderze `Hub/`), przeszła radykalne zmiany. Główne zasady:

1. **Agregacja Wyników**: Hub zbiera odpowiedzi (klasy `ProviderSearchResponse` zawierające `ProviderResultCategory` i `ProviderResultItem`) od różnych zdefiniowanych Providerów i prezentuje je użytkownikowi.
2. **Wielotransportowość**: W przeciwieństwie do starych rozwiązań ograniczonych tylko do protobuf + Named Pipes, obecnie Hub może łączyć się z dostawcami (Providers) poprzez NamedPipe (z użyciem serializacji System.Text.Json), gRPC lub HTTP.
3. **Użycie BaseProvider**: Projekty dostawców delegują całą logikę połączeniową do warstwy abstrakcji. Hub nawiązuje stabilną i szybką komunikację wiedząc, jak skontaktować się z `BaseProviderHost`.
4. **Wersja .NET**: Cały kod kompilowany jest przy użyciu najnowszej platformy **.NET 10.0-windows**.

## 2. Mechanizm Wyszukiwania
Po wpisaniu frazy przez użytkownika, Hub:
- Transformuje ją do postaci modelu `ProviderSearchRequest`.
- Wysyła asynchroniczne zapytania do podłączonych dostawców (Providers).
- Mapuje otrzymane kategorie i wyniki na lokalny model widoku.

Wszelkie parametry wyszukiwania przekazywane są jako zestandaryzowane pola `ProviderSearchRequest` (m.in. limit, token autoryzacyjny, itp.). Hub ma także możliwość pobierania dedykowanych ustawień (`ProviderSettings`) per każdy Provider.
