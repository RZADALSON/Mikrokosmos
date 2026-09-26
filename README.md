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
- **Ustawienia:** N – dźwięk · F – licznik FPS · V – V-Sync · F11 – pełny ekran · R – restart po przegranej

## Co jest w grze
**Światy** są 3–5× większe niż w wersji przeglądarkowej:
- staw 9 × 6,4 mm,
- tkanka 520 × 360 µm,
- biofilm 260 × 180 µm,
- pole TEM 40 × 28 µm.

**Nawigacja:**
- minimapa,
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

**Dźwięk:** generowany proceduralnie, bez plików audio (`src/Game/Audio.cs`).

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
src/Organisms/            EColi, PhageT4, Bdello, Staph (+Strep), Blood (neutrofil, RBC, płytki, limfocyt),
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
  - Vorticella.
- `-- --shot=menu,t4,neutro:1,vort,spiro,net,rotifer --out=<folder> [--frames=N]` – zapisuje zrzuty ekranu
  (liczba po dwukropku oznacza tryb mikroskopu).
