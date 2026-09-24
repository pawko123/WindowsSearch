# Workflow: VS Code Search Provider

Ten dokument opisuje przepływ pracy (workflow) niezbędny do stworzenia dostawcy dla VS Code, zoptymalizowanego przy użyciu ustawień użytkownika oraz mechanizmu pamięci podręcznej (Cache). 

## 1. Ustawienia Providera (Konfiguracja)
*   **Struktura:** Utwórz model ustawień dedykowany dla dostawcy VS Code.
*   **Identyfikator Jump List:** Dodaj pole konfiguracyjne przechowujące ID pliku Jump List.
    *   **Wartość domyślna:** `1ced32d74a95c7bc`.
    *   **Walidacja:** Użyj atrybutu (np. Regex), aby wymusić dokładnie 16-znakowy ciąg znaków szesnastkowych (tylko cyfry oraz litery A-F).
*   **Odświeżanie Cache:** Dodaj pole określające, co ile minut dane mają być odświeżane z dysku.
    *   **Wartość domyślna:** Np. 5 minut.
    *   **Walidacja:** Ustal sensowny zakres, np. od 1 do 1440 minut.

## 2. Mechanizm Pamięci Podręcznej (Cache)
*   Zamiast czytać pliki z dysku przy każdym zapytaniu, przechowuj odczytane elementy w pamięci RAM (np. w postaci dwóch odrębnych kolekcji dla obszarów roboczych i plików).
*   Śledź czas ostatniej aktualizacji pamięci podręcznej.
*   W momencie otrzymania zapytania wyszukiwania, sprawdź, czy minął zdefiniowany w ustawieniach czas.
*   Jeśli czas wygasł (bądź pamięć jest pusta), zablokuj dostęp z innych wątków (zapewniając bezpieczeństwo współbieżności) i uruchom procedury odczytu z dysku.
*   Jeśli dane są aktualne, pomiń odczyt z dysku i przejdź od razu do filtrowania wyników w RAM.

## 3. Procedura Odczytu Obszarów Roboczych (Workspaces)
*   Odczytaj z dysku plik `%APPDATA%\Code\User\globalStorage\storage.json`.
*   Rozkoduj i przeanalizuj jego zawartość JSON, nawigując do klucza przypisującego projekty (węzeł `profileAssociations`, a następnie `workspaces`).
*   Wyodrębnij z tego słownika wszystkie klucze, które są tekstowymi reprezentacjami URIs.
*   Zdekoduj ścieżki (usuwając prefiks formatu pliku oraz odkodowując znaki specjalne, takie jak spacje czy dwukropki).
*   Wyciągnij i zachowaj do wyświetlania wyłącznie najgłębszy składnik ścieżki (ostateczną nazwę folderu / projektu).
*   Gotowe obiekty (z nazwą folderu jako tytułem oraz pełną ścieżką jako podtytułem) nadpisz do odpowiedniego bufora pamięci podręcznej dla Workspaces.

## 4. Procedura Odczytu Ostatnich Plików (Files)
*   Odczytaj plik list szybkiego dostępu (Jump Lists) w Windows zlokalizowany w `%APPDATA%\Microsoft\Windows\Recent\AutomaticDestinations\`. Użyj ID zapisanego w ustawieniach providera, aby uformować pełną nazwę pliku z końcówką `.automaticDestinations-ms`.
*   Ponieważ jest to binarny format OLE zawierający osadzone skróty (LNK), wykorzystaj parser radzący sobie z tym standardem w celu wyłuskania ciągów znaków prowadzących do pełnych ścieżek fizycznych plików.
*   Z każdej znalezionej i poprawnej ścieżki plikowej wyciągnij wyłącznie jego nazwę z rozszerzeniem (będzie to tytuł wpisu na liście).
*   Gotowe obiekty (z nazwą pliku jako tytułem) nadpisz do bufora pamięci podręcznej dla Plików.

## 5. Procedura Filtrowania i Zwracania Wyników (Search & Response)
*   Filtrowanie wykonuj wyłącznie na listach przetrzymywanych w Cache.
*   **Filtrowanie Workspaces:** Sprawdź zbuforowaną listę folderów i pozostaw tylko te pozycje, w których wyciągnięta wcześniej najgłębsza nazwa folderu zawiera frazę wpisaną przez użytkownika (proces ten powinien ignorować wielkość liter).
*   **Filtrowanie Files:** Sprawdź zbuforowaną listę plików na dokładnie takiej samej zasadzie, porównując wyłącznie wyciągniętą wcześniej nazwę pliku do frazy wyszukującej.
*   Zagreguj tak przefiltrowane elementy w odpowiednie kategorie docelowe: jedną jednoznacznie opisaną dla obszarów roboczych, a drugą dla pojedynczych plików.
*   Wyślij pogrupowaną odpowiedź na żądanie aplikacji bazowej. Akcja kliknięcia w tak zwrócony rekord (obojętne czy to folder, czy plik) powinna uruchamiać proces systemowy wywołujący interfejs linii poleceń Visual Studio Code (CLI) na powiązanej fizycznej ścieżce rekordu.
