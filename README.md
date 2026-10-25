# Mikrokosmos – Godot 4.7 (.NET / C#)

Symulator mikroświata: fag T4, Bdellovibrio, E. coli, neutrofil, makrofag, pantofelek, Didinium, ameba, klejnotka, niesporczak, ameba społeczna, plemnik, komórka nowotworowa, świdrowiec i młody neuron kory.
Każdy organizm ma własną skalę, czas i typ mikroskopu, a do tego duży świat z celami do wykonania.

## Uruchamianie
- `Graj.bat` lub skrót „Mikrokosmos” na pulpicie – uruchamia grę
- `Edytor.bat` – otwiera projekt w edytorze Godot (wersja **.NET**)
- `Test.bat` – kompiluje projekt i przepuszcza wszystkie scenariusze przez test automatyczny (bez okna)

W edytorze: F5 uruchamia grę. Kod C# edytujesz w VS Code lub Visual Studio. Z konsoli
projekt kompilujesz poleceniem `dotnet build`.

Wymagane: Godot 4.7 .NET i .NET SDK 8 (oba zainstalowane przez winget).

## Sterowanie
Lista klawiszy jest tylko w **Ustawienia → Sterowanie** (ogólne + klawisze bieżącego trybu, `Main.CommonKeys`, `Scenario.Controls`);
na ekranie gry, w menu i w pauzie nie ma podpowiedzi klawiszy.
- **Ruch:** WASD albo przytrzymany LPM · C – podążanie za kursorem · Shift – szybciej
- **Powiększenie:** kółko myszy – zatrzaski jak w metalowym pokrętle zoomu: kulka sunie po grzbiecie (cichy szmer stali),
  wpada w wycięcie (trzask ~1 ms + dosiad), stalowe kółko krótko dzwoni (mody 1 : 1,47 : 2,09 : 2,76 : 3,52 od ~3,9 kHz,
  ~25 ms), gałka daje głuche „tuk”; oddalanie – o włos niżej, koniec zakresu – ciężki ogranicznik. Szybkie kręcenie = seria
  trzasków co ≥ 24 ms (grzechotka). Tor zatrzasków omija filtr 8 kHz, a bufor dźwięku jest napełniany tylko ~65 ms naprzód,
  więc klik nadąża za kółkiem.
  Powiększenie całkowite rośnie w szeregu liczb normalnych R20 (co ~12%: X400 → X450 → X500 …), bazą jest obiektyw
  trybu (`Scenario.MagX` z opisu „Obiektyw 40× (400×)”, TEM ≈ 25 000×).
- **Akcja:** Spacja / PPM – akcja organizmu (koziołek, trichocysty, przyspieszenie czasu; u neutrofila przytrzymanie = NEToza)
- **Widok:** M – tryb mikroskopu · G – mapy pól · F – podgląd · **F8 – szczegóły** (pełne statystyki, zdarzenia, radar, FPS; K – radar, F3 – FPS)
- **Menu i informacje:** I / Tab – wiedza i źródła · Esc / P – pauza (menu pauzy)
- **Ustawienia (O, także w menu i pauzie):** tryb okna (okno / pełny ekran w oknie / pełny ekran wyłączny), V-Sync, licznik FPS,
  głośność ogólna, efektów i tła, lista sterowania. Zapis w `user://settings.cfg`. N – wycisz · F11 – okno ↔ pełny ekran · R – restart po przegranej

## HUD jak na mikrografii
- Domyślnie tylko: nazwa, 2–3 najważniejsze odczyty trybu (`Scenario.KeyRows`), paski życiowe i bieżący etap cyklu; na dole
  pasek danych jak z mikroskopu elektronowego: tryb (PH – kontrast fazowy, DF – ciemne pole, BF – jasne pole, TEM), obiektyw
  albo napięcie (80.0kV), powiększenie (X450, X1.12K, X25.0K), tempo czasu (TL×240) i kropkowana podziałka (10 działek) z długością.
- Czcionka `fonts/MikroScope.ttf` – własna, kreskowa, w stylu generatora znaków z paska danych SEM (stała szerokość, przekreślone
  zero, lekko nierówne kreski); generator: `python tools/make_scope_font.py fonts/MikroScope.ttf [podgląd.png]` (zapisuje TTF bez
  zewnętrznych bibliotek; podgląd wymaga Pillow).
  Znaki spoza czcionki – z czcionki systemowej. Menu, wiedza i podgląd mają zwykłą, czytelną czcionkę.

## Menu
Tło menu jest nieruchome (bez symulacji w tle): ciemne pole widzenia z winietą i szumem, przez które powoli przepływają
podwójne helisy DNA formy B (`src/Render/MenuDna.cs`) – prawoskrętne, średnica ≈ 2 nm, wznios 0,34 nm/pz, ≈ 10,4 pz na
skręt (Wang 1979), bruzda mniejsza i większa (nici przesunięte o ≈ 130°), pary A–T i G–C, fosforany szkieletu. Helisy leżą
na różnych głębokościach – ostre pod tytułem i nad stopką, bliższe i dalsze rozmyte za kartami.

## Tło: głębia preparatu
Preparat ma grubość, więc poza płaszczyzną ostrości zawsze leży materia widoczna jako rozmyte plamy – jak na prawdziwych
zdjęciach (`shaders/scope_bg.gdshader`, `src/Render/Backdrop.cs`). Obiekty są losowe (siatka z przesunięciami, hash z ziarna
świata), w µm świata (maleją przy oddalaniu i przesuwają się ze szkiełkiem), w skupiskach (szum dużej skali), a ich rodzaj
zależy od trybu (`Scenario.Look`: staw, mech, biofilm, tkanka, krew, drogi rodne, agar, TEM), a gęstość – od strefy
(`Scenario.Zones` / `Scenario.Backdrop`): np. osad denny – grudki detrytusu i zmętnienie, zarośla – nici skrętnicy z taśmą
chloroplastu i okrzemki, chmury bakterii – drobiny i mętna woda, wybroczyna – rozmyte erytrocyty, gniazdo guza – rozmyte
komórki nad i pod płaszczyzną, murawa na agarze – tam, gdzie są jeszcze bakterie. Strefy próbkowane są do tekstury 16 × 16
wokół kamery, więc przejścia są płynne. Zrzut konkretnej strefy: `MIKRO_BGZONE=<nr>`.

## Liczebność organizmów (zgodna ze źródłami)
- T4 (TEM): kilka wolnych wirionów w polu (lizat ~10¹⁰ PFU/ml), mikrokolonie E. coli.
- Biofilm (Bdellovibrio, E. coli): poza pływającymi komórkami – mikrokolonie osiadłych komórek (O'Toole i in. 2000,
  Hall-Stoodley i in. 2004); `ParkCap` = 110, żeby kolonie nie znikały po oddaleniu.
- Tkanka (neutrofil, makrofag, guz): tkanka luźna bogata w fibroblasty, zbita – uboga w komórki (Korosec i in. 2019);
  mastocytów ~50/mm² przekroju (Wilkinson i in. 1992), więcej w ognisku zapalnym; włókien kolagenu więcej.
- Staw: więcej glonów (Chlamydomonas, okrzemki przy osadzie).
- Mech: wrotki bdelloidalne liczniejsze od niesporczaków (setki–dziesiątki tysięcy/g vs 84–321/g; Lukashanets i in. 2022,
  Mäenpää i in. 2024) – ~4–5 wrotków na niesporczaka; nicienie.
- Ameba społeczna: 360 ameb na płytce 1,6 × 1,6 mm (część prawdziwej populacji ~10³/mm²; sygnał cAMP niesie całość).
- Plemniki: bez zmian – pula ~620 w pochwie i gęstości odcinków wg Suarez i Pacey 2006 (nie miliony – nie mają znaczenia
  dla rozgrywki, a kosztowałyby płynność).

## Wydajność (≥ 100 FPS przy pełnym oddaleniu, 4K)
- Geometria klatki budowana jest na wszystkich rdzeniach (`World.DrawParallel`): tło sceny w kilku częściach
  (`Scenario.UnderParts`), encje w zakresach o podobnym koszcie (wierzchołki z poprzedniej klatki), nakładka. Każda część ma
  własną parę elementów płótna (tekstury, wektory) i wysyła je z wątku roboczego; kolejność rysowania (warstwa po warstwie)
  nadają indeksy rysowania. **Metody `Draw` nie mogą zmieniać stanu świata ani losować** (są wołane równolegle).
- Ciała z tekstury (`src/Render/SpriteAtlas.cs`): ziarnista cytoplazma (orzęski, ameby, leukocyty, makrofagi, komórki guza,
  fibroblasty, wirczyki, wrotki), erytrocyty, mastocyty z ziarnistościami – rozpięte na membranie (`Painter.TexBody` – drabinka
  wzdłuż osi, działa też dla ciał wygiętych), więc wyginają się razem z obrysem; kilkadziesiąt wierzchołków zamiast setek.
- Pamięć statyczna: nieruchome, nieanimowane encje (`Ent.StaticDraw`: kolagen, ziarna, listki mchu, skrętnica) rysowane są raz
  do osobnych elementów płótna i przebudowywane tylko przy zmianie powiększenia (> 4%), punktu odniesienia, wyjściu kadru
  poza zapamiętany obszar albo zmianie zbioru (`StaticKey`, np. strawione włókno).
- Szczegóły znikają, gdy są poniżej piksela (rzęski co drugą, ziarna co n-te, trichocysty jako pas, rzęski E. coli tylko
  z bliska), wgniecenia membran liczone tylko dla ciał dość dużych na ekranie (`Ent.MemPushPx`).
- Symulacja: membrany i odroczone kształty (`Ent.PreMem`) równolegle; kolizje – kandydaci i testy kształtów równolegle,
  rozsuwanie po kolei (wynik jak w jednym wątku), przy rozsuwaniu tylko translacja kształtu (`Ent.ShiftPrim`); pole przepływu
  pływaków ma siatkę źródeł (`World.SwimFlow` sprawdza tylko pobliskie pływaki).
- Pomiar: `-- --fpstest=<id> --zoom=0.35 --seed=7 [--advance=60] [--prof]` (z `--prof` także średni czas każdej części
  geometrii). Zmierzone na RTX 4080 SUPER + Ryzen 7 5800X3D, 3840 × 2160, pełne oddalenie: na starcie 105–240 FPS,
  po 60 s gry (większe populacje, guz) 109–320 FPS we wszystkich 14 trybach.

## Język / Language
Polski i angielski. Wybór: Ustawienia → „Język / Language”, przycisk w stopce menu albo klawisz L w menu
(przy pierwszym uruchomieniu – język systemu). Z konsoli: `-- --lang=en`.
- Tekst w kodzie jest po polsku; `src/Game/Loc.cs` tłumaczy go w chwili wyświetlenia według `lang/en.tsv`
  („polski⇥english”, ucieczki jak w C#). Wpisy z `{0}`, `{1}`… to szablony tekstów składanych w locie.
- Etykiety i przyciski tłumaczy Godot (`LocTranslation` w TranslationServer); okno wiedzy, nakładka i ekran końca – jawnie przez `Loc.T`.
- Brakujące tłumaczenia: `MIKRO_LOC_MISS=plik.txt` przy uruchomieniu zapisuje nieprzetłumaczone teksty przy wyjściu.

## Dźwięk
Efekty są wyrównane pomiarowo (`-- --audio=<folder>` mierzy przy pełnej głośności): krótkie zdarzenia ~−8 dBFS szczytu,
sygnały melodyjne ~−9 dBFS, klik interfejsu ~−14 dBFS (`Audio.Gain`). Narodziny w linii gracza mają dźwięk „divide”,
wyklucie – „hatch”, encystacja – „cyst”.

Szum tła (Ustawienia → „Rodzaj szumu tła”, domyślnie biały): biały (płaskie widmo do 22 kHz), różowy (−3 dB/oktawę,
filtr P. Kelleta), brązowy (−6 dB/oktawę) albo dawna „woda”. Szumy barwne są stałe – bez wahań i bez ściszania w menu –
i dodawane za wyjściowym filtrem 8 kHz. Zmierzone przy pełnej głośności: biały −28, różowy −27, brązowy −22 dBFS RMS.

## Cel gry: potomstwo, nie wygrana
Nie ma zwycięstwa. Jak w naturze liczy się tylko dostosowanie – ilu potomków zostawi Twoja linia.
- HUD „TWOJA LINIA”: żyjący potomkowie (szczyt), najgłębsze pokolenie, liczba urodzeń i zmierzony średni czas
  pokolenia (w czasie organizmu). Śledzi to `World.Born(rodzic, potomek)` – każdy podział/wyklucie/wyrzut wirionów
  przekazuje przynależność do linii (`Ent.Lineage`) i numer pokolenia (`Ent.Gen`).
- Gdy Twój osobnik zginie, grasz dalej jego potomkiem. Koniec następuje dopiero, gdy linia wygaśnie (T4 nie dostaje już
  „obcego” wirionu – to inny genom). Rekord (`records.cfg`, klucz `fit`) to dostosowanie linii, a „cele” to etapy cyklu życiowego.
- Didinium w głodzie tworzy cystę i czeka (czas przyspiesza), a gdy wrócą pantofelki – ekscystuje (Beers 1946, Butzel i Horwitz 1965).
- Makrofag po eferocytozie może się podzielić (Gerlach i in. 2021). Neutrofil i makrofag to jednak komórki somatyczne –
  ich geny przechodzą dalej tylko przez gospodarza, więc tu „dostosowanie” to przeżycie gospodarza (HUD to wyjaśnia).
- E. coli kontroluje wielkość regułą „adder” (Taheri-Araghi i in. 2015).

## Tryb: plemnik (jajowód)
Plemnik człowieka przebywa całą drogę: sklepienie tylne pochwy → kanał szyjki (śluz, krypty) → jama macicy (perystaltyka) → połączenie maciczno-jajowodowe → cieśń → bańka jednego z dwóch jajowodów (komórka jajowa tylko po stronie owulacji); odległości skrócone ok. 12×, mikroskala prawdziwa (`src/Game/ScSperm.cs`, `src/Organisms/Sperm.cs`).
- Geometria: wąska cieśń (~220 µm) przechodzi w szeroką bańkę (~860 µm), fałdy śluzówki, nabłonek z rzęskami.
- Przepływ Poiseuille'a ku macicy (`Scenario.CustomFlow` – unosi wszystko, także drobiny w wodzie), pulsujący ze skurczami.
- Plemnik: nie może się zatrzymać; ściany przyciągają go i prowadzą wzdłuż krawędzi (Elgeti 2010, Denissenko 2012);
  reotaksja – moment „wiatrowskazu” obraca go pod prąd (Miki i Clapham 2013, Kantsler 2014); termotaksja (Bahat 2003)
  i chemotaksja do progesteronu przy wzgórku (Lishko, Strünker 2011).
- Cykl: rezerwuar (wiązanie z nabłonkiem cieśni) → kapacytacja → hiperaktywacja (Spacja) → wzgórek jajonośny → reakcja
  akrosomalna i przejście przez osłonkę → fuzja IZUMO1–JUNO → reakcja korowa (blokada polispermii) → przedjądrza.
- Rywale z tego samego ejakulatu i neutrofile fagocytujące plemniki. Dostosowanie = zapłodnienie.
- Liczebność jak w naturze: w sklepieniu pochwy pula ~620 plemników (w kadrze 400× dziesiątki, najgęściej przy ujściu
  szyjki); dalej `ScSperm.Population` utrzymuje gęstość odcinka w promieniu 650 µm od gracza – śluz szyjki ~18 w polu
  widzenia (test po stosunku: ~10–50), macica ~2,5, cieśń ~6 (połowa związana z nabłonkiem – rezerwuar), bańka ~1,
  przy wzgórku ~5 (Suarez i Pacey 2006). Nowe plemniki pojawiają się poza kadrem, a przy wejściu do nowego odcinka –
  także w kadrze, „wpływając w ostrość” (`Sperm.FadeIn`). HUD: „Plemniki w polu widzenia (ruch postępowy)”.
- Nasienie jak w badaniu WHO 2021: ruch postępowy ~55%, niepostępowy ~10% (ciasne kółka), nieruchome ~35%; wady budowy
  widoczne w 400× (`Sperm.Morph`: wydłużona/bezkształtna/za duża/za mała główka, dwie główki, zagięta szyjka, kropla
  cytoplazmy, zwinięta i podwójna witka). Śluz szyjki filtruje: wadliwe główki i witki grzęzną (lepkość ×0,3).
  Tryb podglądu (F) pokazuje ruchliwość i budowę każdego plemnika (`Ent.Note`).
- Pochwa: złuszczone komórki nabłonka płaskiego (`Squamous`), pałeczki kwasu mlekowego (2600 cząstek w tle, drgają
  w ruchach Browna), skrzep nasienia (semenogeliny) upłynniany przez PSA w ~50 s (w naturze zwykle ~15 min) – do tego
  czasu ruch ×0,55. pH: nasienie podnosi je z ~4,3 do ~7,2 w 8 s (Fox i in. 1973) i utrzymuje godzinami; powrót
  kwaśnego pH (~0,5 pH/h, Boskey i in. 1999) jest w grze skrócony do ~3 min. W kanale szyjki widać pasma mucyn.

## Tryb: komórka nowotworowa (kaskada przerzutowania)
Ten sam świat tkanki co neutrofil (bez ognisk zakażenia), time-lapse ×240, procesy życiowe ×8; we krwi czas rzeczywisty
(`src/Game/ScCancer.cs`, `src/Organisms/Cancer.cs`).
- Tlen: pO₂ ≈ 45 mmHg przy ścianie żyłki, zero ~185 µm dalej (Thomlinson i Gray 1955); sąsiednie komórki guza go zużywają.
  G – mapa niedotlenienia (jak barwienie pimonidazolem). Umiarkowaną hipoksję (kilka mmHg, typową dla raków – Vaupel 2004)
  komórki przeżywają (HIF-1α, wolniejsze podziały); ciężka hipoksja < 1,5 mmHg (≈ 0,2% O₂) bez mutacji TP53 → apoptoza
  (Graeber 1996, Hammond i Giaccia 2005); anoksja < 0,5 mmHg → martwica.
- HIF-1α i makrofagi (pętla EGF/CSF-1) → EMT; bez sygnałów i w bezruchu – MET (potrzebne do kolonii przerzutowej).
- Spacja: tryb mezenchymalny (MT1-MMP trawi włókna, `Collagen.Degrade`) ↔ ameboidalny (szybszy, ale jądro utyka w gęstej sieci).
- Angiogeneza: VEGF z niedotlenionych komórek → kiełek z komórką szczytową rośnie ~0,2 mm/dobę i dostarcza tlenu.
- Intrawazacja (szybciej obok makrofaga – TMEM), krążenie (naprężenia ścinające, komórki NK, osłona płytek), przyleganie
  (Spacja przy ścianie, łatwiej w rozkurczu), ekstrawazacja; przerzut liczy się ≥ 1 mm od guza.
- Mutacje kierujące przy podziale (KRAS, TP53, CDH1, B2M, PD-L1, F3) zmieniają fenotyp; limfocyty T CD8⁺ i komórki NK
  (`Killer`) – „brak swojego”, wyczerpanie przez PD-L1, trafienia sumują się.
- Guz wygląda jak w preparacie, a nie jak hodowla: gniazdo ~0,25 mm (~140 komórek, płatowaty zarys `NestR`) między dwiema
  żyłkami; komórki różnej wielkości (CV ~14%), z jądrami o różnej wielkości, zarysie i chromatynie, 1–3 jąderkami,
  czasem dwujądrowe lub olbrzymie (pleomorfizm); upakowane ciasno, błony spłaszczone o sąsiadów.
- Zrąb guza: fibroblasty CAF (`Fibroblast(caf: true)`) owijające gniazdo, kolagen styczny (TACS-2) i wyprostowany
  promienisty w miejscach inwazji (TACS-3, `Collagen(crimp)`; Provenzano i in. 2006), makrofagi TAM na obrzeżu, w gnieździe
  i przy żyłce (TMEM), limfocyty T/NK w zrębie na obrzeżu (wykluczenie immunologiczne).
- Tlen z drożnych naczyń: żyłek, naczyń włosowatych zrębu i połączonych kiełków (te słabiej – naczynia guza są nieszczelne).
  W obrębie gniazda naczynia włosowate są ściśnięte (`CapOpen`, Padera i in. 2004), więc środek jest niedotleniony,
  a zdrowy zrąb – nie. Gniazdo rośnie z liczbą komórek (`TumR`, limit 230 komórek w oknie).
- Śmierć zostawia ślad (`TumorDebris`): martwica – blady „cień” z okruchami chromatyny; apoptoza (p53, limfocyty) –
  ciałka apoptotyczne, które zjadają makrofagi (eferocytoza).

## Tryb: świdrowiec (Trypanosoma brucei we krwi)
Ruch i przeciwciała w czasie rzeczywistym, podziały i odporność ×720 (`src/Game/ScTryp.cs`, `src/Organisms/Trypano.cs`).
- Nie może stanąć (ruch niezbędny do życia – Broadhead 2006); pływanie naprzód spycha przeciwciała do kieszonki wiciowej
  (Engstler 2007), Spacja – pływanie wstecz (nie usuwa przeciwciał).
- Miano przeciwciał osobno dla każdego wariantu VSG rośnie po ekspozycji; przy podziale ~1/8 szans na nowy VSG → fale parazytemii.
- Krew gęsta jak naprawdę: ~44 erytrocyty na chunk 60 µm (≈ 60% kadru), płytki ~1 na 18 erytrocytów, a krwinka biała
  (neutrofil lub limfocyt) – raz na kilkaset czerwonych.
- SIF (gęstość) → forma krępa (nie dzieli się, żyje 2–3 doby); w strefie skóry mucha tse-tse wbija kłujkę i zasysa krew –
  wyssana forma krępa z Twojej linii = przeniesienie (wynik). Komórki Kupffera w wątrobie zjadają opłaszczone pasożyty.

## Tryb: młody neuron kory (mysz, od E15,5 do P6)
Neuron piramidowy warstwy II/III, neuron spoidłowy, od narodzin przy komorze do połączenia z drugą półkulą
(`src/Game/ScNeuron.cs`, `src/Organisms/Neuron.cs`, świat – `src/Organisms/Cortex.cs`). Time-lapse ×1800 (1 s gry = 30 min),
w oczekiwaniu na falę śmierci ×7200; Shift przyspiesza czas ×4.
- Świat: przekrój czołowy kory grzbietowej myszy w E16,5, „rozprostowany” – opona na górze (y = 0), komora na dole (482 µm,
  Tonosaki i in. 2023), linia środkowa (szczelina międzypółkulowa, ciało modzelowate, klin glejowy, szare pokrywające,
  przegroda) po lewej, a za nią druga półkula (lustrzanie). Tysiące komórek pola (`CortexField`) to tekstury, nie encje:
  glej promienisty z wędrówką jądra w cyklu 18,4 h (Takahashi i in. 1995; mitozy przy komorze), progenitory pośrednie w SVZ,
  komórki wielobiegunowe w IZ, upakowana płytka korowa, podpłytka, komórki Cajala–Retziusa, opony. Ruchome komórki je rozsuwają.
  Naczynia: splot opony miękkiej, splot okołokomorowy (w skrawku – odcinki z przerwami, przez które przechodzą komórki) i naczynia
  promieniste (Vasudevan i in. 2008); w time-lapse krwinki w każdej klatce są w innym miejscu.
- Etapy (rozkład czasu jak w znakowaniu FlashTag kohorty E15,5 w korze grzbietowo-przyśrodkowej – Yoshinaga i in. 2021):
  progenitor pośredni (Tbr2⁺) idzie z powierzchni komory do SVZ i dzieli się symetrycznie na dwa neurony (siostra wędruje obok;
  Noctor i in. 2004) → komórka wielobiegunowa ~4,4 µm/h (Tabata i Nakajima 2003); neuryt, który dotknie pęczka aksonów TAG-1
  w dolnej IZ, staje się aksonem (Namba i in. 2014) → pod podpłytką synapsy z neuronów podpłytki (NMDA) przełączają ją
  w komórkę dwubiegunową (Ohtaka-Maruyama i in. 2018) → lokomocja po włóknie gleju: wypustka wiodąca, poszerzenie, skok jądra
  ~36 µm/h, średnio ~10 µm/h (A/D – sąsiednie włókno, np. obok naczynia) → translokacja końcowa ~50 µm w < 1 h (Sekine i in. 2011)
  i osiedlenie u szczytu płytki (od środka na zewnątrz). Automat w teście osiada w E18,5 – tak jak w FlashTag.
- Akson rośnie sam, dopóki ciało wędruje (Spacja – widok stożka wzrostu), i czeka przed linią środkową; po osiedleniu gracz
  steruje stożkiem: ~37 µm/h przed linią środkową, ~55 µm/h za nią (Hutchins i in. 2011), w korze docelowej o połowę wolniej
  (Halloran i Kalil 1994). Klin glejowy i szare pokrywające (Slit2 → Robo) powodują zapadnięcie i cofnięcie stożka
  (Shu i Richards 2001); droga prowadzi przez pas ciała modzelowatego. Cel: miejsce lustrzane (homotopowe) pod korą drugiej
  półkuli; ~P6 gałęzie boczne wnikają do kory (Wang i in. 2007).
- Fala naturalnej śmierci: od P2 ginie ~12% neuronów płytki korowej i podpłytki (hazard gaussowski ze szczytem ~P4; Wong i in. 2018,
  Blanquie i in. 2017) – komórki pola przechodzą piknozę, a mikroglej (rozproszony w VZ/SVZ/IZ – Hattori i Miyata 2018,
  po urodzeniu także w płytce) je pochłania. Los gracza rozstrzyga się ok. P5: neuron osiadły na czas (≤ P0,5) przeżywa,
  później – ryzyko 15–35%, heterotopia w istocie białej – 60% (mniej wejść synaptycznych i aktywności). Nieukończona wędrówka
  do P2 → apoptoza. Koniec: neuron połączył półkule (wynik = długość aksonu).
- Interneurony z wyniosłości zwojowych w strumieniach stycznych (strefa brzeżna, podpłytka, dolna IZ/SVZ), 12 ± 6 µm/h
  (Yanagida i in. 2012), z rozgałęzioną wypustką wiodącą; stała gęstość w oknie, po urodzeniu strumienie słabną.
- Mikroskop (M): IR-DIC (nowy tryb filtra 6 – relief z różnicy jasności wzdłuż kierunku ścinania, obraz jednobarwny),
  fluorescencja GFP po elektroporacji in utero (filtr 7 – czarne tło, poświata; widać tylko kohortę GFP i kilka wyznakowanych
  komórek gleju promienistego) i jasne pole. `Scenario.StyleFor` / `LookFor` zmieniają tło i materię poza ostrością według filtra.
- Testy: `MIKRO_CHECK_ONLY=neuron -- --check` (podział w SVZ, akson z TAG-1, przejście pod podpłytką, prędkości lokomocji,
  translokacji i stożka, Slit2, linia środkowa, ~12% w fali śmierci, mikroglej, interneurony, pełna rozgrywka automatem);
  zrzuty `--shot=neuron-N[:tryb]` (N = 1–8 – etap), pomiar `--fpstest=neuron --stage=N`.

## Tryb podglądu (F) – atlas
F zatrzymuje grę bez menu pauzy: WASD przesuwa widok, kółko zmienia zoom, najechanie podświetla obiekt, a kliknięcie
otwiera kartę – prawdziwe zdjęcie, nazwa (PL/EN), krótki opis i najważniejsze dane oraz wiersz „Ten osobnik”
(rozmiar tego konkretnego obiektu, czy to gracz/linia, integralność). Działa na organizmy, włókna, kryształy, ale też
na tło: naczynie i śródbłonek, tkankę łączną, kiełki naczyń, śluz szyjki, jajowód, wodę stawu, chmury bakterii, cień
rzęsy wodnej, błonkę wody w mchu, agar, murawę bakterii, osocze, kłujkę tse-tse (`Scenario.InspectAt`). F lub Esc – powrót.
- Dane: `atlas/build_atlas.py` → `atlas/atlas.json` (93 karty); klucz encji – `Atlas.KeyOf` (gatunek/typ/wariant).
- Zdjęcia: `atlas/photo_plan.json` (plik źródłowy z Wikimedia Commons, licencja, autor, tryb obróbki) →
  `python atlas/process_photos.py` (wycięcie z jednolitego tła albo kadr) → `atlas/photos/` + `atlas/photos.json`
  (autor, licencja i link pokazywane na karcie). Potem `python atlas/build_atlas.py`.
- `--check` sprawdza, że każdy obiekt i każde tło we wszystkich światach ma kartę; `--shot=insp-<id>` / `inspbg-<id>`.
- Licznik FPS przeniesiony z F na F3.

## Odkształcenie ciała i animacja
- `Membrane.LagA` / `Membrane.BendEff`: po każdym `Follow` membrana dopasowuje (najmniejsze kwadraty) opóźnienie obrotu
  i efektywne wygięcie rzeczywistego obrysu. `Ent.BodyLocal(p)` = `p.Local(X, Y, A)` + `p.Warp(Mem)` – wnętrze ciała
  (ryjek Didinium, rowek gębowy pantofelka, jądra, organella) wygina się i obraca razem z obrysem przy skrętach.
  `--check` mierzy rozjazd przodu obrysu i wnętrza (np. Didinium: 11,5 µm → 0,4 µm).
- Niesporczak: ciało wygina się w stronę skrętu (`Painter.Warp(cx, cy, ang, bend)`, części kolizji też), stopy stoją na
  podłożu w fazie podporu i wracają w fazie przenoszenia; fala kroków od tyłu ku przodowi, pary w przeciwfazie (Nirody 2021).
- Wrotek pełza jak pijawka: zawsze jeden koniec stoi w miejscu (środek przesuwa się o połowę zmiany długości).
- Fibroblasty: prowadzenie kontaktowe wzdłuż kolagenu (Dunn i Heath 1976) i hamowanie kontaktowe (Abercrombie i Heaysman 1954).

## Kontakty
Sort & sweep + rozsuwanie proporcjonalne do oporu; po pierwszym przebiegu dwie dodatkowe iteracje Gaussa–Seidla dla par
w twardym kontakcie (tylko część ponad „miękkość”, bez zdarzeń). `-- --physaudit [--only=id] [--frames=N]` mierzy
w każdym świecie pary ciał nachodzące na siebie ponad zamierzoną miękkość (odsetek i najgorsze pary typów).
`--shot=zz-<id>` – zrzut w trakcie ostrego zygzaka (kontrola animacji przy skrętach).

## Kolagen a leukocyty
Włókno leży w trójwymiarowej sieci, więc nie odpycha pełzającego leukocytu, tylko go spowalnia (`Collagen.Seep`, `Ent.Mesh`):
prędkość dzielona przez 1 + opór, większy w poprzek włókna niż wzdłuż (Lämmermann 2008, Renkawitz 2019). Neutrofil nie utyka;
komórka nowotworowa w trybie ameboidalnym – tak (duże, sztywne jądro; Wolf 2013).

## Drożność świata (rzut 2D trójwymiarowego preparatu)
Preparat jest trójwymiarowy, a gra pokazuje jego rzut – nic, co w naturze da się obejść górą lub dołem, nie może być w grze
nieskończoną ścianą:
- **Listki mchu** (`MossLeaf`, `Solid = false`) to podłoże: blaszka ma grubość jednej komórki, a niesporczak, wrotek i nicień
  chodzą po listkach i pod nimi w błonce wody. Przeszkodą w mchu są tylko ziarna piasku (60–180 µm).
- **Fibroblasty** (`Fibroblast.Permeable/Seep`) to płaskie komórki w sieci kolagenu: każda ruchliwa komórka (leukocyt, limfocyt,
  komórka raka) mija je z oporem jak w sieci włókien (`Ent.Mesh`), bez odpychania – dawna sprężyna cofała wolno pełzającą
  komórkę szybciej, niż ta szła, więc fibroblast działał jak ściana. Zatrzymują się na sobie tylko fibroblasty (hamowanie kontaktowe).
- **Żyłki** w tkance (`ScNeutro.Constrain`) to rurki (Ø 32 µm, co 420 µm): neutrofile, makrofagi, limfocyty i komórki T/NK
  obchodzą je nad lub pod naczyniem – w paśmie żyłki ~1,6× wolniej (półobwód π·r zamiast średnicy). Komórka raka zatrzymuje się
  na ścianie (tu zaczyna się intrawazacja), bakterie – też.
- **Adipocyty** w tkance tłuszczowej świdrowca nie stykają się – kanał ≥ 9 µm odwzorowuje ciągłą przestrzeń śródmiąższową.
- Pomiar: `-- --pathaudit [--only=id,..] [--secs=S] [--seed=N] [--dirs=0,90] [--v] [--emt [--amo]]` – gracz idzie prosto
  w 8 kierunkach (świat od nowa dla każdego); okna 2 s z postępem < 15% najszybszego okna to „utknięcia”, a ciała dotykane
  w tych oknach są wypisane (`~` – ciało przepuszczalne). `--v` wypisuje każde okno, `--emt` – komórka raka po EMT.
  Pozostałe „utknięcia” to mechanika (przyczepienie Bdellovibrio, pożarcie), granice (brzeg płytki agaru, powierzchnia
  komory mózgu) albo zderzenie czołowe z prawdziwą przeszkodą (ziarno piasku, komórka Kupffera, rój neutrofili przy Candidzie).

## Płynne ciała
- `Membrane.ConserveArea` – cytoplazma jest nieściśliwa: wgnieciony obrys oddaje ubytek pola, wybrzuszając wolne węzły.
- Ślimak Dictyostelium: ciało sunie po torze czubka (oś z 18 punktów), obrys to membrana (wgniata się, zachowuje pole),
  kolizje – kapsuły wzdłuż wygiętej osi.
- Ameby Dictyostelium, mastocyty i neutrofile w jajowodzie mają miękkie membrany; ciała bez odkształcalnego obrysu
  (fibroblast, niesporczak) mają mniejszą „miękkość”, by się nie przenikały.

## Fizyka i optyka (przegląd naukowy)
- Pole przepływu wokół pływaka jest dipolem (siła netto = 0), a nie stokesletem: `Ent.Dipole` +1 „pchacz” (E. coli,
  Bdellovibrio, Pseudomonas), −1 „ciągacz” (Chlamydomonas), 0 squirmer (orzęski) – Drescher i in. 2010/2011, Lauga i Powers 2009.
  Komórki pełzające (`Ent.Swims = false`) pola nie wytwarzają; w tkance i na agarze (`Scenario.Fluid = false`) nie ma swobodnej wody.
- Stałe dyfuzji bakterii z równania Stokesa–Einsteina; E. coli przy dnie pływa po okręgach zgodnie z ruchem wskazówek zegara (Lauga i in. 2006).
- `shaders/scope_filter.gdshader`: kontrast fazowy z halo (Zernike, Otaki 2000), ciemne pole liczone z rozpraszania na krawędziach,
  blade jasne pole bez kontrastu.

## Co jest w grze
**Światy są nieskończone.** Mapa generuje się w chunkach wokół gracza, a każdy świat ma kilka stref (biomów):

| Scenariusz | Chunk | Strefy |
|---|---|---|
| T4 (TEM) | 6 µm | mikrokolonia, rozproszone, puste, kolonia odporna |
| Bdellovibrio | 40 µm | biofilm E. coli, czysta woda, kolonia Acanthamoeby, osady mineralne |
| E. coli | 40 µm | bogata, uboga, strefa ampicyliny, teren drapieżników |
| Neutrofil, makrofag | 100 µm | tkanka luźna, gęsty kolagen, ognisko zakażenia, wylew (żyłki co 420 µm) |
| Pantofelek, Didinium, ameba, klejnotka | 1,4–1,6 mm | otwarta woda, zarośla skrętnicy, osad, chmury bakterii |

**Makrofag** (time-lapse ×90): wolny fagocyt, który pochłania bakterie, drożdże *Candida*, erytrocyty z wylewów i apoptotyczne neutrofile.
- Spacja wydziela cytokiny, które ściągają neutrofile z żyłki.
- Neutrofile starzeją się w tkance i przechodzą apoptozę. Nieuprzątnięte ulegają martwicy wtórnej.
- Eferocytoza przełącza makrofaga w fenotyp naprawczy (TGF-β) i regeneruje błonę.
- Strawione erytrocyty zostawiają złotobrązowe ziarna hemosyderyny.
- Drożdżak, który nie zostanie strawiony na czas, wypuszcza strzępkę i przebija makrofaga (kandydalizyna).

**Rekrutacja neutrofili i rojenie** (neutrofil, makrofag):
- `ScNeutro.Chemo` – sygnał zapalny (fMLP, C5a, chemokiny, histamina) rośnie z liczbą bakterii w oknie; neutrofile
  wychodzą z żyłki najbliższej skupiskom bakterii (diapedeza) do ~1 na sekundę gry, aż do `3 + Chemo/2,5` (limit 44,
  w trybie makrofaga 36). Śledzenie: `-- --trace=neutro --idle` (sam rój utrzymuje ~0–80 bakterii przy 16–40 neutrofilach).
- Chemotaksja z daleka (`Attractant`, do 360 µm): skupiska bakterii i „latarnie” LTB4 od neutrofili, które fagocytują
  albo giną (Lämmermann i in. 2013); w środku roju komórki stają (sygnał stop, Kienle i in. 2021).
- Neutrofile w tkance starzeją się (~12 h) i przechodzą apoptozę; cele 1–2 liczą Twoje fagosomy (`p_engulfed`,
  `p_killed`), 3–4 – cały rój (`killed`).
- Naczynia włosowate (`CapCell`, oczko ~88 µm, Ø ≈ 6 µm) z erytrocytami płynącymi gęsiego („spadochron”), śródbłonkiem
  i pericytami; końce wychodzące z płaszczyzny ostrości bledną; odpływ do żyłek. W guzie ściśnięte, bez przepływu.

**Serce i krążenie** (neutrofil, makrofag):
- Serce bije w czasie rzeczywistym i słychać tony S1 („lub”, 50–130 Hz) i S2 („dub”, 75–200 Hz).
- Zakażenie wywołuje gorączkę (do ok. 40 °C), a gorączka tachykardię: ok. 9 uderzeń/min na 1 °C, więcej przy ciężkim zakażeniu.
- Przy szybkim rytmie skraca się skurcz, a przy ciężkim zakażeniu maleje zmienność rytmu (HRV).
- Krwinki w żyłkach płyną jako osobne krążki i przyspieszają po każdym skurczu.
- W zapaleniu przepływ zwalnia i więcej leukocytów toczy się po ścianie żyłki.

**Klejnotka** (*Euglena gracilis*): energia pochodzi z fotosyntezy.
- Światło zależy od strefy, dryfujących cieni rzęsy wodnej i chmur (0–2000 µmol/m²/s).
- Krzywa P–I nasyca się przy ok. 200–400. Powyżej ok. 800 narasta fotoinhibicja.
- Zapasem jest paramylon; pełny zapas uruchamia podział podłużny.
- W półmroku przy detrytusie działa osmotrofia.
- Spacja uruchamia metabolię: pełzanie i chowanie się w kłaczkach detrytusu przed drapieżnikami.

**Tryb nanobota (eksploracja):** osobna karta w menu. Wybierasz skalę (9 światów, w grze klawisze 1–9).
- Bot ma ok. 1/30 kadru i jest nieśmiertelny.
- Laser (Spacja / PPM) dezintegruje każdą komórkę.
- Skaner pokazuje nazwę, gatunek, rozmiar i stan organizmu pod kursorem.
- Z/X zmienia tempo czasu. Nie ma celów ani końca gry.

**Ekosystemy (zweryfikowane w literaturze – lista źródeł w grze pod klawiszem I):**
- **Staw:** Euglena (metabolia, plamka oczna), Blepharisma (blefaryzmina truje drapieżniki, kanibale-olbrzymy),
  P. bursaria (zielenice), Spirostomum (skurcz w ~5 ms, toksyny), Stentor (osiadły, wir), Coleps (stada, pancerz),
  Euplotes (chodzi na cirrach), Tetrahymena, Lacrymaria (szyja 7× ciała), słonecznica (aksopodia),
  Volvox, nicienie i nitki sinic (ciała z segmentów), rozwielitka (filtruje orzęski), detrytus.
- **Biofilm:** Pseudomonas, Bacillus (łańcuszki, przetrwalniki), Caulobacter (rozetki), Spirillum volutans,
  drożdże, drapieżne Bodo i przepływające olbrzymy Tetrahymena.
- **TEM:** pęcherzyki OMV (wabiki – T4 wiąże się nieodwracalnie), fagi T7 i λ (konkurencja, lizogenia),
  bakterie Gram+, zerwane rzęski.
- **Tkanka:** makrofagi, fibroblasty, adipocyty, mastocyty (histamina → więcej neutrofili),
  Candida (drożdżaki do fagocytozy, strzępki tylko przez NET).

**Miękkie ciała:** kontakt komórek rozwiązywany jest częściowo (`Ent.Soft`). Ciała mogą się wcisnąć,
a membrany radialne spłaszczają się nawzajem (`Membrane.PushOutMem`). Nicienie i nitki wyginają się na przeszkodach.

**Nawigacja:**
- radar (okno aktywnych chunków w kolorach stref),
- w HUD-zie wiersze „Strefa” i „Od startu”; wejście w nową strefę pokazuje komunikat,
- strzałki „zmysłu chemicznego”: zielona prowadzi do pokarmu, czerwona pokazuje zagrożenie.

**Cele i rekordy:** każdy organizm ma 4 cele. Po ich ukończeniu gra przechodzi w tryb swobodny.
Rekordy zapisują się w `user://records.cfg`.

**Nowe organizmy i środowisko:**
- **Staw:** wrotki (wir korony, mastaks, odruch chowania), kolonie wirczyków (skurcz trzonka), nitki skrętnicy, ziarna piasku, prądy wody.
- **Biofilm:** Acanthamoeba (drapieżnik bakterii), ziarna mineralne, ampicylina (liza wybrzuszeniowa przy podziale).
- **Tkanka:**
  - żyłka pozawłośniczkowa z przepływem krwi i diapedezą neutrofili,
  - włókna kolagenu,
  - paciorkowce (łańcuszki),
  - leukocydyny niszczące neutrofila,
  - NEToza.
- **TEM:** mikrokolonie E. coli, które powoli się dzielą, oraz odporne mutanty (OmpC⁻).

**Przetrwanie:** głód (pantofelek, Didinium, ameba), energia fazy ataku (Bdellovibrio), integralność błony (neutrofil).

**Dźwięk:** generowany proceduralnie (stereo 44,1 kHz, `src/Game/Audio.cs`) w stylu nagrań hydrofonowych: szum wody, bąbelki,
miękkie pęknięcia błon, bez trzasków. `-- --audio=<folder>` zapisuje każdy efekt do WAV i wypisuje poziomy.

**Ruch:** `Steer` – ciąg rzęsek/wici narasta w ułamku sekundy, obrót przyspiesza płynnie. Membrany są sprężyste
(`Membrane.Stiff`), wyginają się na zakrętach (`Membrane.Bend`) i wgniatają przy kontakcie.

## Architektura
```
scenes/Main.tscn          jedyna scena; wszystko budowane w kodzie
shaders/                  scope_bg (tło + kurz na optyce), scope_overlay (winieta + szum),
                          scope_filter (ciemne pole / TEM szary / negatyw)
src/Main.cs               warstwy, pętla gry, sterowanie, kamera, HUD, menu, pauza, testy
src/Core/                 M (matematyka, RNG), Geo (kolizje), Ent (encja), Membrane (soft-body),
                          FX (liza, pęcherzyki, igły), Field (stężenia), World (+ SpatialGrid, prądy, cele)
src/Render/Painter.cs     wsadowy renderer: cała geometria → kilka CanvasItemAddTriangleArray (+ ciała z tekstury)
src/Render/SpriteAtlas.cs atlas tekstur: cytoplazma (4 wydłużenia), erytrocyt, mastocyt
src/Render/Backdrop.cs    tło głębi preparatu: wygląd trybu i gęstości stref dla scope_bg
src/Render/WorldView.cs   węzeł rysujący świat (+ atlas detrytusu)
src/Organisms/            Protist (uniwersalny pierwotniak + katalog Species), Pond (Chain: nicienie/sinice,
                          słonecznica, Lacrymaria, Daphnia, Volvox, detrytus), Microbes (inne bakterie, OMV, fagi T7/λ),
                          Tissue (makrofag, fibroblast, adipocyt, mastocyt, Candida), Nanobot,
                          EColi, PhageT4, Bdello, Staph (+Strep), Blood (neutrofil, RBC, płytki, limfocyt),
                          Protists (pantofelek, Didinium, Chlamydomonas, chmury bakterii), Algae (okrzemki,
                          desmidie, ameba), Rotifer (+ Acanthamoeba), Environment (ziarna, kolagen,
                          skrętnica, wirczyki, NET)
src/Game/                 Scenario (baza: cele, minimapa, paski), Scenarios (7 światów), Overlay
                          (minimapa, strzałki, podziałka), Audio, Records, Ui
```

**Jednostki:** świat liczony jest w mikrometrach (µm).
- `dt` to czas biologiczny (czas gry × `TimeScale`).
- `gdt` to czas gry (animacje).
- Procesy życiowe (podziały, trawienie, infekcja) przyspiesza dodatkowo mnożnik `Bio`.

**Fizyka:** dynamika przetłumiona (niska liczba Reynoldsa, brak bezwładności), ruchy Browna,
kolizje metodą sort & sweep. Zapytania o sąsiedztwo idą przez siatkę przestrzenną (`World.Near` / `World.Query`).
Prąd wody to pole bezźródłowe liczone z funkcji prądu.

**Świat nieskończony (`World.UpdateChunks`):**
- Aktywne jest okno 5 × 5 chunków (`ActiveR = 2`) wokół gracza. Tylko ono jest symulowane i rysowane.
- Chunk, który wypada z okna, zostaje zamrożony. Jego organizmy trafiają do `Chunk.Parked` z pełnym stanem
  i wracają, gdy gracz się zbliży.
  - `Ent.Holder` wiąże zdobycz lub przyczepionego faga z właścicielem, więc parkują razem.
  - `ParkCap` ogranicza liczbę zamrożonych organizmów na chunk.
- Nowe chunki generuje `Scenario.GenerateChunk`. Strefę wybiera deterministycznie `BiomeAt` na podstawie
  szumu `Noise2` i ziarna `World.Seed`. Chunki startowe (|cx|, |cy| ≤ 1) mają zawsze strefę 0.
- Pola działają w obrębie okna:
  - siatka przestrzenna i prąd wody liczą się tylko w oknie;
  - chmury bakterii (`BactField`) oddają cząstki spoza okna do licznika `Chunk.Bact`;
  - stężenia (`TileField`) są przechowywane w kafelkach na chunk.
- `Painter.OX/OY`: wierzchołki są liczone względem punktu odniesienia przy kamerze, więc grafika nie drga
  daleko od startu.

## Jak dodać nowy organizm
1. Utwórz klasę dziedziczącą po `Ent` w `src/Organisms/`. Nadpisz w niej:
   - `Step` – zachowanie organizmu,
   - `Draw` – rysowanie przez `Painter`,
   - opcjonalnie `UpdatePrim`, `OnTouch` i `Collides`.
2. Miękką membranę dodajesz przez `Mem = new Membrane(n)`.
3. Organizm, który ma być pokarmem ameby, ustawia `Edible = true`, `Food` i `State = St.Free`.
   Musi też obsłużyć stany `Cup`/`Vac` (zatrzymanie w `Step`, zanikanie przez `Dig` w `Draw`).
4. W scenariuszu (`Scenarios.cs`):
   - `Init` – dodawanie encji,
   - `MapKind` – kolor na minimapie i strzałkach,
   - `Goals` – cele,
   - `Bars` – paski stanu,
   - `Facts` – fakty w oknie wiedzy.

## Testy
- `-- --autotest [--frames=N] [--prof]` – symuluje każdy scenariusz z losowym sterowaniem i mierzy
  czas symulacji oraz geometrii. `--prof` pokazuje, ile czasu zajmują poszczególne typy encji.
- `-- --pathaudit [--only=id]` – drożność: czy gracz nie utyka na przeszkodach (patrz „Drożność świata”).
- `-- --check` – celowane testy mechanik:
  - NEToza,
  - ampicylina,
  - Acanthamoeba,
  - odporność na T4,
  - ameba zjadająca wrotka,
  - Vorticella,
  - zamrażanie i odmrażanie chunków,
  - różnorodność stref,
  - długa podróż (stała liczba aktywnych encji),
  - miękkie odkształcenie, Coleps, blefaryzmina, wabik OMV, wir Stentora,
  - laser nanobota i start bota we wszystkich skalach, NET na strzępce Candida.
  - eferocytoza i hemosyderyna w makrofagu, apoptoza i martwica wtórna neutrofili,
  - światło w stawie, fotosynteza, fotoinhibicja, metabolia w detrytusie i podział podłużny Eugleny,
  - neutrofil w gęstym kolagenie (bez utykania),
  - komórka nowotworowa: gradient tlenu, EMT w niedotlenieniu, mitoza, trawienie kolagenu, intrawazacja → krążenie →
    ekstrawazacja, limfocyty T / NK / PD-L1,
  - świdrowiec: usuwanie przeciwciał ruchem, podział i zmiana VSG, SIF → formy krępe, przeniesienie do tse-tse, komórki Kupffera.
  - rekrutacja neutrofili przy zakażeniu, tlen w zrębie i w guzie (naczynia włosowate), pleomorfizm, martwica i apoptoza,
  - plemniki: gęstość puli nasienia i śluzu szyjki, filtr śluzu dla plemników z wadami budowy.
  - testy zależne od losowości sprawdzają populację, a nie pojedynczy los: reotaksja – 8 plemników (≥ 4 pod prąd, czas przy
    ścianie wyraźnie ponad udział pasa przyściennego w świetle), anhydrobioza – 10 beczułek (≥ 8 przeżywa).
- `MIKRO_CHECK_ONLY=new` – uruchamia tylko testy dwóch najnowszych trybów (`sperm` – od testów plemnika).
- `-- --trace=<id>[,<id>] [--frames=N] [--every=S] [--idle]` – przebieg bez okna, co S s gry wiersze HUD i liczebność typów
  (strojenie równowagi); `-- --autotest --only=<id>` – test wydajności wybranych trybów.
- `-- --fpstest=<id> [--zoom=Z] [--advance=S] [--prof] [--nomeasure]` – płynność w oknie gry (6 s): FPS, czasy symulacji,
  geometrii i renderu, oś czasu klatki, liczba wierzchołków (z `--prof` – wg typów encji). `--advance` najpierw przewija S s
  gry (stan w połowie rozgrywki), `--zoom` ustawia powiększenie. W trakcie pomiaru nic nie jest wypisywane – wypisywanie do
  konsoli w każdej klatce samo kosztuje kilka ms i fałszuje wynik.
- Nieruchome encje mogą zapamiętać swoją geometrię (`Painter.CacheFor/TryReplay/BeginCapture/EndCapture`, np. `Collagen`):
  trójkąty są odtwarzane, dopóki nie zmieni się powiększenie, punkt odniesienia bufora ani klucz stanu (stopień strawienia).
- Zrzuty: `neutro-swarm` (rój przy ognisku), `sperm-cervix`, `sperm-uterus`, `sperm-isthmus`, `sperm-egg`.
- Zrzuty specjalne: `cancer-inv`, `cancer-amo`, `cancer-blood`, `cancer-mit`, `tryp-ab`, `tryp-div`, `tryp-bite`.
- `-- --shot=sp-daphnia@0.4,nano-para` – zrzut wybranego gatunku (`sp-...`) albo trybu nanobota (`nano-...`), `@` = zoom.
- `-- --shot=menu,t4,neutro:1,vort,spiro,net,rotifer --out=<folder> [--frames=N]` – zapisuje zrzuty ekranu
  (liczba po dwukropku oznacza tryb mikroskopu).

## Licencja
Kod gry: **GNU GPL v3** (plik `LICENSE`). Możesz go używać, zmieniać i rozpowszechniać, także w płatnych projektach,
ale każda opublikowana zmieniona wersja musi być udostępniona z kodem źródłowym na tej samej licencji.

Zdjęcia w `atlas/photos/` nie są objęte GPL – mają własne licencje (CC BY, CC BY-SA, CC0, domena publiczna) i autorów
wymienionych w `atlas/photos/CREDITS.md`.
