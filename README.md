# Mikrokosmos – Godot 4.7 (.NET / C#)

Symulator mikroświata: fag T4, Bdellovibrio, E. coli, neutrofil, pantofelek, Didinium i ameba.
Każdy organizm ma własną skalę, czas i typ mikroskopu, a do tego duży świat z celami do wykonania.

## Uruchamianie
- `Graj.bat` lub skrót „Mikrokosmos” na pulpicie – uruchamia grę
- `Edytor.bat` – otwiera projekt w edytorze Godot (wersja **.NET**)
- `Test.bat` – kompiluje projekt i przepuszcza wszystkie scenariusze przez test automatyczny (bez okna)

W edytorze: F5 uruchamia grę. Kod C# edytujesz w VS Code lub Visual Studio. Z konsoli
projekt kompilujesz poleceniem `dotnet build`.

Wymagane: Godot 4.7 .NET i .NET SDK 8 (oba zainstalowane przez winget).

## Sterowanie
- **Ruch:** WASD albo przytrzymany LPM · C – podążanie za kursorem · Shift – szybciej · kółko myszy – zoom
- **Akcja:** Spacja / PPM – akcja organizmu (koziołek, trichocysty, przyspieszenie czasu; u neutrofila przytrzymanie = NEToza)
- **Widok:** M – tryb mikroskopu · K – minimapa · G – mapy stężeń (E. coli)
- **Menu i informacje:** I / Tab – wiedza i źródła · Esc / P – pauza (menu pauzy)
- **Ustawienia (O, także w menu i pauzie):** tryb okna (okno / pełny ekran w oknie / pełny ekran wyłączny), V-Sync, licznik FPS,
  głośność ogólna, efektów i tła. Zapis w `user://settings.cfg`. Skróty: N – wycisz · F11 – okno ↔ pełny ekran · R – restart po przegranej

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
src/Render/Painter.cs     wsadowy renderer: cała geometria → kilka CanvasItemAddTriangleArray
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
  - światło w stawie, fotosynteza, fotoinhibicja, metabolia w detrytusie i podział podłużny Eugleny.
- `-- --shot=sp-daphnia@0.4,nano-para` – zrzut wybranego gatunku (`sp-...`) albo trybu nanobota (`nano-...`), `@` = zoom.
- `-- --shot=menu,t4,neutro:1,vort,spiro,net,rotifer --out=<folder> [--frames=N]` – zapisuje zrzuty ekranu
  (liczba po dwukropku oznacza tryb mikroskopu).
