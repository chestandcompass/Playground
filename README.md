# Playground

Samodzielny mod multiplayer dla **Slime Rancher 2 1.3.0 na Windows x64**. Natywne menu gry, osobne zapisy multiplayer i lokalny profil goscia dla kazdego swiata hosta.

**Wersja testowa LAN.** Tworzenie, zapis i ponowne wczytanie swiata hosta oraz interfejs zostaly sprawdzone w grze. Pelny scenariusz goscia na drugim komputerze pozostaje do walidacji. Polaczenia przez internet za NAT nie sa jeszcze obslugiwane.

## Instalacja

Pobierz **PlaygroundSetup.exe** z [wydan](https://github.com/robertbrania-code/Playground/releases) i uruchom. Instalator sam znajduje gre w bibliotekach Steam, instaluje wymagane skladniki i Playground. Nie trzeba kopiowac DLL ani uruchamiac skryptow. Instalator ma rowniez biezaca wersje offline.

W menu gry wybierz **Graj ze znajomym / Play with a friend**. Host tworzy lub wybiera swiat; w pauzie moze skopiowac kod zaproszenia. Gosc kopiuje otrzymany kod i wybiera dolaczenie do znajomego. Oba komputery musza miec to samo wydanie Playground.

Playground nie wymaga Starlight, Ranching Together, DiscordRPC ani SharpOpenNat. Skladnikiem ladujacym mod jest MelonLoader 0.7.3; instalator dolacza go oraz wymagany Microsoft .NET 6.

## Aktualizacje i zapisy

Gra sprawdza publiczny plik `update.json`. Dostepna aktualizacja pojawia sie w jej natywnym menu. Po wybraniu aktualizacji zweryfikowany instalator czeka na zamkniecie gry, a potem podmienia DLL i zachowuje poprzednia wersje. Ponowne uruchomienie instalatora rowniez pobiera najnowsze wydanie. Starszy instalator nie cofa nowszej wersji.

Instalator sprawdza zgodnosc buildu gry i sumy SHA-256. Zapisy znajduja sie poza instalacja: `%LOCALAPPDATA%\Playground\SlimeRancher2`. Zapisy singleplayer pozostaja w katalogu gry w LocalLow. Pliki zapisow nie sa czescia aktualizacji.

## Budowanie ze zrodel

Wymagane: Windows, .NET SDK 8, wlasna legalna instalacja gry 1.3.0 oraz MelonLoader 0.7.3. Uruchom gre z loaderem raz, aby wygenerowac lokalne biblioteki IL2CPP.

```powershell
.\Prepare-References.ps1 -GameDirectory 'sciezka-do-gry'
dotnet build SR2MP -c Release
dotnet run --project tests/StorageTests -c Release
.\Build-Installer.ps1 -GameDirectory 'sciezka-do-gry'
```

Biblioteki gry nie sa dystrybuowane i nie wolno ich dodawac do repozytorium. Do kolejnego wydania zmien `release.json`; builder synchronizuje wersje w kodzie, tworzy instalator, DLL i manifest z hashami. Plik `update.json` nalezy publikowac wraz z odpowiadajacymi mu plikami wydania.

## Licencja i pochodzenie

GPL-3.0, patrz [LICENSE](LICENSE). Kod synchronizacji zostal zaadaptowany z [Ranching Together / pyeight](https://github.com/pyeight/SlimeRancher2Multiplayer), autorstwa Shark (py8) i wspoltworcow. Zachowujemy autorstwo upstream. Playground dodaje samodzielna integracje z gra, natywne menu, izolacje zapisow, instalator i aktualizacje. Projekt nie jest oficjalnym dodatkiem Monomi Park i nie zawiera plikow gry.

## Konsola i odinstalowanie

Instalator domyslnie ukrywa konsole MelonLoadera takze przy uruchamianiu gry ze Steam. Logi nadal zapisuja sie do plikow; inne ustawienia loadera sa zachowywane.

Aby odinstalowac, otworz instalator ponownie i wybierz **Odinstaluj**, albo wybierz **Playground — Slime Rancher 2** w zainstalowanych aplikacjach Windows. Zapisy pozostaja na dysku. Gdy inne mody, pluginy lub UserLibs potrzebuja loadera, zostaje on aktywny; bez nich znany loader Playground zostaje wylaczony. Poprzednie pliki sa archiwizowane w PlaygroundBackups. Wspolny Microsoft .NET nie jest usuwany.

## Kopia swiata singleplayer
W menu Playground wybierz **Skopiuj swiat singleplayer / Copy a singleplayer world**, wybierz swiat i potwierdz kopiowanie. Najnowszy poprawny zapis trafi do **Moje swiaty / My worlds**. Oryginal pozostaje bez zmian; oba swiaty rozwijaja sie niezaleznie. Ponowny import tego samego swiata jest blokowany, aby chronic postepy multiplayer. Kopiowanie plikow sprawdzono automatycznie; caly przebieg importu i wczytania w grze wymaga jeszcze walidacji.


## Usuwanie swiata multiplayer
**Moje swiaty → wybierz swiat → Usun swiat multiplayer** otwiera potwierdzenie z nazwa swiata. Domyslnie zaznaczone jest **Anuluj**. Usuwanie obejmuje zapisy wybranego swiata hosta; oryginal singleplayer i lokalne profile gosci pozostaja. Przebieg usuwania przez natywne menu wymaga jeszcze sprawdzenia w grze.


## Jezyki menu Playground
Menu, pomoc, pauza, potwierdzenia i komunikaty aktualizacji uzywaja jezyka wybranego w grze: en, de, fr, es, pt, ru, ja, ko, zh (uproszczony). Zachowano rowniez polskie tlumaczenia na wypadek dostepnego locale pl; Playground nie dodaje polskiego jezyka do samej gry. Nieznany jezyk korzysta z angielskiego. Nazwy swiatow nie sa tlumaczone. 34 teksty sprawdzono automatycznie dla wszystkich 10 katalogow; wyglad, dlugosc tekstow i zmiana jezyka na zywo wymagaja jeszcze kontroli w grze. Instalator pozostaje po polsku.


Instalator preview 7 rozszerza wykrywanie Steam o rejestr uzytkownika i systemu (32/64 bit), InstallLocation gry, standardowe foldery Steam, stare i nowe libraryfolders.vdf oraz rzeczywisty folder z appmanifest_1657630.acf. Niepelne instalacje sa pomijane. Przy braku jednoznacznego wyniku mozna wskazac SlimeRancher2.exe recznie.


## Dopasowanie przyciskow
Przyciski Playground w natywnym menu glownym i pauzie dopasowuja szerokosc do przetlumaczonego napisu. Przy granicy ekranu tekst zawija sie, a przycisk rosnie na wysokosc. Wspoldzielone przyciski gry odzyskuja poprzednie ustawienia po wyjsciu z menu dodatku.


## Dolaczanie bez kodu w LAN
Host otwiera swiat multiplayer. Drugi gracz wybiera **Join a friend → Find games on LAN**, a nastepnie **Join: nazwa swiata**. Lista odswieza sie automatycznie; zamkniete swiaty wygasaja po kilku sekundach. Widoczne sa hosty z identycznym wydaniem Playground. Nie trzeba konta ani zaproszenia Steam do samego wykrywania.
Wyszukiwanie uzywa broadcastu UDP 19190 i odpowiedzi hosta w sieci lokalnej; polaczenie gry korzysta z portu hosta (domyslnie UDP 1919). Zapora musi pozwalac grze komunikowac sie w sieci prywatnej. Siec goscinna Wi-Fi lub izolacja klientow routera moze blokowac wyszukiwanie. Kod zaproszenia pozostaje dostepny. Testy transportu nie zastepuja pelnego testu dolaczenia z dwoch komputerow.


## Pierwsze dolaczenie goscia
Od preview 10 dolaczanie z LAN lub kodu nie otwiera ustawien nowego swiata. Playground sam przygotowuje lokalny staging w katalogu goscia, automatycznie przechodzi prolog i pokazuje **Joining...**. Gotowy staging jest zapisywany i wykorzystywany przy kolejnych dolaczeniach. Ustawienia tworzenia swiata hosta pozostaja dostepne tylko dla hosta.


## Imie gracza
Przed utworzeniem/otwarciem swiata hosta oraz dolaczeniem z LAN/kodu pojawia sie pole **Your name / Twoje imie** z natywnymi przyciskami Dalej/Wroc. Wpisana i zatwierdzona nazwa jest zapamietywana lokalnie w preferencjach Playground i podpowiadana przy nastepnym razie. Enter zatwierdza, Escape anuluje. Puste imie nie przechodzi dalej, dlugosc jest ograniczona do 24 znakow. Imie trafia do istniejacych nazw nad postaciami i znacznikow; identyfikator gracza i przypisanie zapisow nie zmieniaja sie.
Pole dopasowuje sie do tresci. Brakujace znaki polskie sa renderowane przez dynamiczny fallback z systemowej czcionki Windows; pliki fontow nie sa dystrybuowane. Klawiature, Enter, przycisk Dalej, polskie znaki, zapamietywanie i anulowanie sprawdzono w grze. Pelna sesja dwoch PC z nowymi nazwami nie byla jeszcze sprawdzona.


## Preview 12: formularze i nazwy swiatow
Naprawiono przyciski Continue i Back po wpisaniu imienia: edytor nie odbiera juz stale fokusu przyciskom, a natywne zatwierdzanie wybiera wlasciwa akcje. Klikniecie Continue, klikniecie Back bez zapisu edycji, Enter oraz ponowne otwarcie z zapamietanym imieniem sprawdzono w grze.

W Moje swiaty mozna zmienic nazwe swiata multiplayer. Etykieta jest osobnymi metadanymi: nie zmienia natywnego identyfikatora swiata, plikow .sav ani przypisania profili gosci. Import singleplayer pozostaje osobna, swiadoma operacja. Testy zapisu etykiet i izolacji danych przechodza; scenariusz goscia na drugim komputerze po zmianie nazwy nadal wymaga walidacji.

## Zasady swiata ustala host
Od preview 13 Tarr, dzikie slime i mnoznik obrazen sa przesylane od hosta do goscia przy dolaczeniu i po zmianie zasad. U goscia te opcje sa tylko do odczytu z opisem "Controlled by the host". Zmiana imienia, grafiki, dzwieku, sterowania, napisow i samouczkow pozostaje lokalna. Reguly maja numer rewizji, aby opozniony pakiet nie przywrocil starszego ustawienia. Host i gosc musza miec to samo wydanie Playground.

Walidacja preview 13: 79 testow automatycznych PASS i 7 kontroli na rzeczywistych modelach gry PASS: serializacja pakietu, zmiany trzech regul przez hosta, wylaczone wiersze goscia, blokada zmian/resetu, zastosowanie ustawien hosta, odrzucenie starszej rewizji i zachowanie osobnego samouczka. Testowano jedna instancje z symulowana rola goscia, bez drugiej kopii gry. Pelna proba sieciowa miedzy dwoma komputerami pozostaje do wykonania.

## Preview 14: magazyny i ostatnia sztuka gadzetu
Naprawiono ponowna rejestracje magazynow po wczytaniu swiata, usuwanie starych map slotow i zbyt wczesna inicjalizacje. Zawartosc magazynow goscia jest uzgadniana z pelnymi stanami hosta z numerem rewizji; opoznione stany czekaja na magazyn zamiast odtwarzac przyrosty kilka razy. Host moze obsluzyc magazyn przez model swiata takze bez zaladowanego obiektu sceny. Przy pobieraniu synchronizowana jest rzeczywista liczba usunietych przedmiotow, bez podwojnego zliczania wywolan slotu i calego magazynu. Puste sloty zachowuja definicje, a kolejnosc slotow nie zalezy od kolejnosci slownika.

Nowe stawianie gadzetow z puli jest rozstrzygane przez hosta: jedna sztuka oznacza jedna zaakceptowana prosbe, rezerwacje stanu i jeden obiekt. Powtorzenie tej samej prosby nie zuzywa kolejnej sztuki; nieudane tworzenie zwraca rezerwacje. Przenoszenie juz podniesionych gadzetow pozostaje na natywnej sciezce. Zachowane sa czas ladowania gadzetu i zdarzenie jego postawienia.

Walidacja: 87 testow automatycznych PASS oraz 12 sprawdzen w jednej instancji gry PASS (9 magazynow, 3 stawiania gadzetow). Obejmuje rzeczywisty format pakietow, podmiane magazynu, aktualizacje przed rejestracja, pelny pierwszy slot, model bez komponentu sceny, dwie prosby o ostatnia sztuke i ponowienie prosby. To nadal wersja eksperymentalna: pelna proba dwoch PC oraz pozostale przypadki rownoczesnych interakcji wymagaja dalszej walidacji.
