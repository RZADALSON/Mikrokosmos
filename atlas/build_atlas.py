# Źródło atlasu (tryb podglądu, klawisz F): python atlas/build_atlas.py → atlas/atlas.json
# Każdy wpis: nazwa, podtytuł (łacina / kategoria), krótki opis i najważniejsze dane – po polsku (pl) i angielsku (en).
# Zdjęcia i ich autorstwo (photo, credit, url) dopisuje atlas/photos.json (licencje Wikimedia Commons).
import json, os
HERE = os.path.dirname(os.path.abspath(__file__))

def E(pl, en):
    """pl/en = (nazwa, podtytuł, opis, [(klucz, wartość), ...])"""
    def t(x): return {"name": x[0], "sub": x[1], "desc": x[2], "facts": [list(f) for f in x[3]]}
    return {"pl": t(pl), "en": t(en)}

A = {}
# ------------------------------------------------------------------ bakterie, wirusy, drobne cząstki
A["ecoli"] = E(("Pałeczka okrężnicy", "Escherichia coli · bakteria Gram-ujemna",
    "Najlepiej poznany organizm na Ziemi. Pływa dzięki kilku rzęskom obracanym silnikami białkowymi: płynie prosto (bieg), a co ok. sekundę na chwilę koziołkuje i losuje nowy kierunek.",
    [("Rozmiar", "≈ 2 × 0,8 µm"), ("Prędkość", "20–30 µm/s"), ("Podział", "co ~20 min w bogatej pożywce"), ("Żyje", "jelito ssaków i ptaków, woda, gleba")]),
  ("Escherichia coli", "Escherichia coli · Gram-negative bacterium",
    "The best-studied organism on Earth. It swims with several flagella turned by protein motors: it runs straight, and about once a second it briefly tumbles and picks a new direction.",
    [("Size", "≈ 2 × 0.8 µm"), ("Speed", "20–30 µm/s"), ("Division", "every ~20 min in rich medium"), ("Lives in", "gut of mammals and birds, water, soil")]))
A["t4"] = E(("Bakteriofag T4", "Tequatrovirus · wirus bakterii",
    "Wirus atakujący E. coli. Włóknami ogonka „macha” po powierzchni bakterii, a po związaniu kurczy pochewkę jak strzykawkę i wstrzykuje DNA przez ścianę komórki.",
    [("Główka", "≈ 85 × 115 nm"), ("Genom", "≈ 169 tys. par zasad DNA"), ("Cykl", "potomstwo po ~25 min (37 °C)"), ("Potomków", "ok. 100–200 z jednej bakterii")]),
  ("Bacteriophage T4", "Tequatrovirus · bacterial virus",
    "A virus that attacks E. coli. It scans the bacterial surface with its tail fibres, and once bound it contracts its sheath like a syringe and injects its DNA through the cell wall.",
    [("Head", "≈ 85 × 115 nm"), ("Genome", "≈ 169 thousand DNA base pairs"), ("Cycle", "progeny after ~25 min (37 °C)"), ("Offspring", "about 100–200 per bacterium")]))
A["lambda"] = E(("Bakteriofag λ", "Lambdavirus lambda · fag łagodny",
    "Fag, który może wybrać: albo od razu namnożyć się i rozerwać bakterię (cykl lityczny), albo wbudować swoje DNA w chromosom gospodarza i czekać (lizogenia).",
    [("Główka", "≈ 60 nm"), ("Ogonek", "długi, giętki, ~150 nm"), ("Genom", "≈ 48,5 tys. par zasad"), ("Znaczenie", "klasyczny model przełącznika genetycznego")]),
  ("Bacteriophage λ", "Lambdavirus lambda · temperate phage",
    "A phage that can choose: either multiply at once and burst the bacterium (lytic cycle), or insert its DNA into the host chromosome and wait (lysogeny).",
    [("Head", "≈ 60 nm"), ("Tail", "long and flexible, ~150 nm"), ("Genome", "≈ 48.5 thousand base pairs"), ("Importance", "classic model of a genetic switch")]))
A["t7"] = E(("Bakteriofag T7", "Teseptimavirus · fag z krótkim ogonkiem",
    "Mały, ściśle lityczny wirus E. coli z bardzo krótkim ogonkiem. Konkuruje z T4 o te same bakterie.",
    [("Główka", "≈ 60 nm, dwudziestościan"), ("Genom", "≈ 40 tys. par zasad"), ("Cykl", "wyłącznie lityczny")]),
  ("Bacteriophage T7", "Teseptimavirus · short-tailed phage",
    "A small, strictly lytic E. coli virus with a very short tail. It competes with T4 for the same bacteria.",
    [("Head", "≈ 60 nm, icosahedral"), ("Genome", "≈ 40 thousand base pairs"), ("Cycle", "lytic only")]))
A["omv"] = E(("Pęcherzyk błony zewnętrznej", "OMV · outer membrane vesicle",
    "Bakterie Gram-ujemne odrywają małe kulki własnej błony zewnętrznej. Fagi i przeciwciała wiążą się z nimi zamiast z bakterią – działają jak wabik.",
    [("Średnica", "≈ 20–250 nm"), ("Rola", "wabik, sygnały, transport białek"), ("Źródło", "Manning i Kuehn 2011")]),
  ("Outer membrane vesicle", "OMV",
    "Gram-negative bacteria pinch off small spheres of their outer membrane. Phages and antibodies bind to them instead of the bacterium – they work as decoys.",
    [("Diameter", "≈ 20–250 nm"), ("Role", "decoy, signalling, protein transport"), ("Source", "Manning and Kuehn 2011")]))
A["flagfrag"] = E(("Zerwana rzęska", "filament rzęski bakteryjnej (flagelina)",
    "Fragment długiego, sztywnego helikalnego włókna z białka flageliny. Rzęska bakterii jest obracana u nasady jak śruba okrętowa.",
    [("Średnica", "≈ 20 nm"), ("Długość rzęski", "do ~10 µm"), ("Silnik", "do ~100 000 obr./min u niektórych bakterii")]),
  ("Broken flagellum", "bacterial flagellar filament (flagellin)",
    "A piece of the long, stiff helical filament made of the protein flagellin. A bacterial flagellum is rotated at its base like a ship's propeller.",
    [("Diameter", "≈ 20 nm"), ("Flagellum length", "up to ~10 µm"), ("Motor", "up to ~100,000 rpm in some bacteria")]))
A["bdello"] = E(("Bdellovibrio", "Bdellovibrio bacteriovorus · drapieżna bakteria",
    "Bakteria, która poluje na inne bakterie. Wwierca się do przestrzeni między błonami ofiary, zjada ją od środka, rośnie jako nić i dzieli się na nieparzystą lub parzystą liczbę potomków.",
    [("Rozmiar", "≈ 0,3–0,5 × 0,5–2,5 µm"), ("Prędkość", "do ~160 µm/s – ok. 100 długości ciała na sekundę"), ("Ofiary", "bakterie Gram-ujemne, np. E. coli"), ("Cykl", "kilka godzin w ofierze")]),
  ("Bdellovibrio", "Bdellovibrio bacteriovorus · predatory bacterium",
    "A bacterium that hunts other bacteria. It bores into the space between the prey's membranes, eats it from the inside, grows as a filament and splits into an odd or even number of progeny.",
    [("Size", "≈ 0.3–0.5 × 0.5–2.5 µm"), ("Speed", "up to ~160 µm/s – about 100 body lengths per second"), ("Prey", "Gram-negative bacteria, e.g. E. coli"), ("Cycle", "a few hours inside the prey")]))
A["acanth"] = E(("Acanthamoeba", "Acanthamoeba castellanii · ameba swobodnie żyjąca",
    "Ameba z cienkimi, kolczastymi wypustkami (akantopodiami), pospolita w wodzie i glebie. Zjada bakterie; w złych warunkach tworzy odporną cystę. Może wywołać zapalenie rogówki u osób noszących soczewki.",
    [("Rozmiar", "≈ 15–35 µm"), ("Pokarm", "bakterie, drożdże"), ("Przetrwalnik", "dwuścienna cysta")]),
  ("Acanthamoeba", "Acanthamoeba castellanii · free-living amoeba",
    "An amoeba with thin, spiky projections (acanthopodia), common in water and soil. It eats bacteria and forms a tough cyst in bad conditions. It can infect the cornea of contact-lens wearers.",
    [("Size", "≈ 15–35 µm"), ("Food", "bacteria, yeasts"), ("Resting form", "double-walled cyst")]))
A["grain"] = E(("Ziarno mineralne", "kwarc SiO₂ · osad",
    "Drobne ziarno piasku lub mułu. Dla bakterii to głaz, na którym osiada biofilm.",
    [("Muł", "2–63 µm"), ("Piasek", "63 µm – 2 mm"), ("Skład", "głównie kwarc (SiO₂)")]),
  ("Mineral grain", "quartz SiO₂ · sediment",
    "A tiny grain of sand or silt. For bacteria it is a boulder on which a biofilm settles.",
    [("Silt", "2–63 µm"), ("Sand", "63 µm – 2 mm"), ("Composition", "mostly quartz (SiO₂)")]))
A["bacillus"] = E(("Laseczka sienna", "Bacillus subtilis · bakteria Gram-dodatnia",
    "Glebowa bakteria tworząca łańcuszki. W trudnych warunkach zamyka kopię DNA w przetrwalniku – endosporze, która przetrwa wrzenie i wysuszenie przez lata.",
    [("Komórka", "≈ 2–3 × 0,8 µm"), ("Przetrwalnik", "odporny na gorąco, UV, suszę"), ("Żyje", "gleba, korzenie roślin")]),
  ("Hay bacillus", "Bacillus subtilis · Gram-positive bacterium",
    "A soil bacterium that forms chains. In hard times it seals a copy of its DNA into an endospore that can survive boiling and years of drought.",
    [("Cell", "≈ 2–3 × 0.8 µm"), ("Spore", "resists heat, UV and drying"), ("Lives in", "soil, plant roots")]))
A["pseudomonas"] = E(("Pałeczka ropy błękitnej", "Pseudomonas aeruginosa · bakteria Gram-ujemna",
    "Szybki pływak z jedną rzęską na biegunie, mistrz tworzenia biofilmów. Barwnik piocyjanina nadaje ropie niebiesko-zielony kolor; groźny patogen szpitalny.",
    [("Rozmiar", "≈ 1,5–3 × 0,5–0,8 µm"), ("Prędkość", "≈ 40–60 µm/s"), ("Żyje", "woda, gleba, szpitalne rury i rany")]),
  ("Pseudomonas", "Pseudomonas aeruginosa · Gram-negative bacterium",
    "A fast swimmer with a single polar flagellum and a master of biofilms. The pigment pyocyanin turns pus blue-green; a serious hospital pathogen.",
    [("Size", "≈ 1.5–3 × 0.5–0.8 µm"), ("Speed", "≈ 40–60 µm/s"), ("Lives in", "water, soil, hospital pipes and wounds")]))
A["caulobacter"] = E(("Caulobacter", "Caulobacter crescentus · bakteria wód ubogich",
    "Bakteria w kształcie przecinka, która dzieli się asymetrycznie: komórka z trzonkiem zostaje przyklejona, a pływająca córka odpływa. Klej na końcu trzonka to jeden z najmocniejszych klejów w przyrodzie.",
    [("Rozmiar", "≈ 1,5–2 × 0,5 µm"), ("Podział", "na komórkę osiadłą i pływającą"), ("Żyje", "wody słodkie, ubogie w pokarm")]),
  ("Caulobacter", "Caulobacter crescentus · oligotrophic bacterium",
    "A comma-shaped bacterium that divides asymmetrically: the stalked cell stays attached while the swimming daughter swims away. The glue at the tip of the stalk is one of the strongest adhesives in nature.",
    [("Size", "≈ 1.5–2 × 0.5 µm"), ("Division", "into a stalked and a swimming cell"), ("Lives in", "nutrient-poor fresh water")]))
A["spirillum"] = E(("Krętek olbrzymi", "Spirillum volutans · bakteria spiralna",
    "Jedna z największych bakterii: sztywna spirala z pękami rzęsek na obu końcach. Woli wodę z małą ilością tlenu (mikroaerofil).",
    [("Grubość", "≈ 1,4–1,7 µm"), ("Długość", "kilkadziesiąt µm"), ("Żyje", "wody stojące, muł")]),
  ("Giant spirillum", "Spirillum volutans · spiral bacterium",
    "One of the largest bacteria: a rigid spiral with tufts of flagella at both ends. It prefers water with little oxygen (microaerophile).",
    [("Width", "≈ 1.4–1.7 µm"), ("Length", "tens of µm"), ("Lives in", "stagnant water, mud")]))
A["yeast"] = E(("Drożdże", "grzyby jednokomórkowe · pączkowanie",
    "Jednokomórkowe grzyby, które rozmnażają się przez pączkowanie – na komórce wyrasta mała córka. Żyją w wodzie, na roślinach i w biofilmach.",
    [("Komórka", "≈ 3–6 µm"), ("Rozmnażanie", "pączkowanie"), ("Przykład", "Saccharomyces cerevisiae – drożdże piekarskie")]),
  ("Yeast", "unicellular fungi · budding",
    "Single-celled fungi that reproduce by budding – a small daughter grows out of the cell. They live in water, on plants and in biofilms.",
    [("Cell", "≈ 3–6 µm"), ("Reproduction", "budding"), ("Example", "Saccharomyces cerevisiae – baker's yeast")]))
A["staphylococcus"] = E(("Gronkowiec złocisty", "Staphylococcus aureus · ziarniak Gram-dodatni",
    "Kuliste bakterie dzielące się w trzech płaszczyznach – stąd skupiska jak winogrona. Wydzielają leukocydyny (np. PVL), które przebijają błony leukocytów.",
    [("Komórka", "≈ 1 µm"), ("Układ", "grona"), ("Nosicielstwo", "ok. 30% ludzi w nosie"), ("Uwaga", "szczepy MRSA oporne na metycylinę")]),
  ("Staphylococcus aureus", "Gram-positive coccus",
    "Round bacteria that divide in three planes – hence grape-like clusters. They release leukocidins (e.g. PVL) that punch holes in white blood cells.",
    [("Cell", "≈ 1 µm"), ("Arrangement", "clusters"), ("Carriage", "about 30% of people, in the nose"), ("Note", "MRSA strains resist methicillin")]))
A["streptococcus"] = E(("Paciorkowiec", "Streptococcus · ziarniak Gram-dodatni",
    "Ziarniaki dzielące się w jednej płaszczyźnie, dlatego tworzą łańcuszki. S. pyogenes wywołuje anginę i różę.",
    [("Komórka", "≈ 0,6–1 µm"), ("Układ", "łańcuszki"), ("Przykład", "S. pyogenes, S. pneumoniae")]),
  ("Streptococcus", "Gram-positive coccus",
    "Cocci that divide in a single plane, so they form chains. S. pyogenes causes strep throat and erysipelas.",
    [("Cell", "≈ 0.6–1 µm"), ("Arrangement", "chains"), ("Examples", "S. pyogenes, S. pneumoniae")]))
# ------------------------------------------------------------------ krew i tkanka
A["neutrophil"] = E(("Neutrofil", "granulocyt obojętnochłonny · krwinka biała",
    "Najliczniejsza krwinka biała i pierwsza linia obrony. Pełza przez tkankę do ogniska zakażenia, pożera bakterie, a w ostateczności wyrzuca sieć DNA (NET), która je więzi.",
    [("Średnica", "≈ 12 µm"), ("Jądro", "3–5 płatów"), ("Pełzanie", "≈ 10–20 µm/min"), ("Udział we krwi", "ok. 50–70% krwinek białych"), ("Życie", "godziny – 1–2 doby")]),
  ("Neutrophil", "neutrophilic granulocyte · white blood cell",
    "The most numerous white blood cell and the first line of defence. It crawls through tissue to the infection, eats bacteria and, as a last resort, casts a DNA net (NET) that traps them.",
    [("Diameter", "≈ 12 µm"), ("Nucleus", "3–5 lobes"), ("Crawling", "≈ 10–20 µm/min"), ("Share in blood", "about 50–70% of white cells"), ("Lifespan", "hours to 1–2 days")]))
A["rbc"] = E(("Erytrocyt", "krwinka czerwona",
    "Dwuwklęsły krążek bez jądra wypełniony hemoglobiną. Jest tak elastyczny, że przeciska się przez naczynia węższe od siebie.",
    [("Średnica", "≈ 7,5–8 µm"), ("Grubość", "≈ 2 µm"), ("Życie", "≈ 120 dni"), ("Liczba", "≈ 5 mln w 1 µl krwi")]),
  ("Red blood cell", "erythrocyte",
    "A biconcave disc without a nucleus, packed with haemoglobin. It is so flexible that it squeezes through vessels narrower than itself.",
    [("Diameter", "≈ 7.5–8 µm"), ("Thickness", "≈ 2 µm"), ("Lifespan", "≈ 120 days"), ("Count", "≈ 5 million per µl of blood")]))
A["platelet"] = E(("Płytka krwi", "trombocyt",
    "Fragment olbrzymiej komórki szpiku (megakariocytu). Przy uszkodzeniu naczynia przylega do rany i uruchamia krzepnięcie.",
    [("Średnica", "≈ 2–3 µm"), ("Liczba", "≈ 150–400 tys. w 1 µl"), ("Życie", "≈ 7–10 dni")]),
  ("Platelet", "thrombocyte",
    "A fragment of a giant bone-marrow cell (megakaryocyte). When a vessel is damaged it sticks to the wound and starts clotting.",
    [("Diameter", "≈ 2–3 µm"), ("Count", "≈ 150–400 thousand per µl"), ("Lifespan", "≈ 7–10 days")]))
A["lymphocyte"] = E(("Limfocyt", "krwinka biała · odporność swoista",
    "Mała komórka z dużym, okrągłym jądrem. Limfocyty B wytwarzają przeciwciała, T zabijają zakażone komórki i kierują odpowiedzią odpornościową.",
    [("Średnica", "≈ 7–8 µm (małe)"), ("Typy", "B, T, NK"), ("Pamięć", "latami „pamiętają” patogeny")]),
  ("Lymphocyte", "white blood cell · adaptive immunity",
    "A small cell with a large round nucleus. B cells make antibodies; T cells kill infected cells and direct the immune response.",
    [("Diameter", "≈ 7–8 µm (small)"), ("Types", "B, T, NK"), ("Memory", "they 'remember' pathogens for years")]))
A["macrophage"] = E(("Makrofag", "makrofag tkankowy · fagocyt",
    "Powolny, żarłoczny strażnik tkanki. Pożera drobnoustroje, martwe komórki i zużyte krwinki, a cytokinami wzywa neutrofile. Fagocytozę odkrył Ilja Miecznikow (Nobel 1908).",
    [("Średnica", "≈ 20–25 µm"), ("Pełzanie", "≈ 1–3 µm/min"), ("Zadania", "fagocytoza, eferocytoza, gojenie")]),
  ("Macrophage", "tissue macrophage · phagocyte",
    "A slow, voracious guardian of tissue. It eats microbes, dead cells and worn-out blood cells, and calls in neutrophils with cytokines. Phagocytosis was discovered by Ilya Mechnikov (Nobel 1908).",
    [("Diameter", "≈ 20–25 µm"), ("Crawling", "≈ 1–3 µm/min"), ("Jobs", "phagocytosis, efferocytosis, healing")]))
A["fibroblast"] = E(("Fibroblast", "komórka tkanki łącznej",
    "Wrzecionowata komórka, która wytwarza kolagen i całą macierz tkanki łącznej. Pełznie wzdłuż włókien, a gdy dotknie innego fibroblastu – zatrzymuje się i zawraca (hamowanie kontaktowe).",
    [("Długość", "≈ 50–100 µm z wypustkami"), ("Wytwarza", "kolagen, elastynę, proteoglikany"), ("Rola", "gojenie ran, blizny")]),
  ("Fibroblast", "connective-tissue cell",
    "A spindle-shaped cell that makes collagen and the whole connective-tissue matrix. It crawls along fibres and, when it touches another fibroblast, stops and turns away (contact inhibition).",
    [("Length", "≈ 50–100 µm with processes"), ("Makes", "collagen, elastin, proteoglycans"), ("Role", "wound healing, scars")]))
A["adipocyte"] = E(("Komórka tłuszczowa", "adipocyt",
    "Komórka prawie w całości wypełniona jedną kroplą tłuszczu; jądro zepchnięte na brzeg. To magazyn energii i gruczoł wydzielający hormony (np. leptynę).",
    [("Średnica", "≈ 50–150 µm"), ("Tłuszcz", "≈ 90% objętości"), ("Hormony", "leptyna, adiponektyna")]),
  ("Fat cell", "adipocyte",
    "A cell almost entirely filled with a single fat droplet, with the nucleus pushed to the edge. It stores energy and secretes hormones (e.g. leptin).",
    [("Diameter", "≈ 50–150 µm"), ("Fat", "≈ 90% of the volume"), ("Hormones", "leptin, adiponectin")]))
A["mastcell"] = E(("Komórka tuczna", "mastocyt",
    "Komórka nabita ziarnistościami z histaminą. Pobudzona wyrzuca je (degranulacja): naczynia się rozszerzają i napływają leukocyty. Odpowiada też za objawy alergii.",
    [("Średnica", "≈ 10–20 µm"), ("Ziarnistości", "histamina, heparyna, proteazy"), ("Alergia", "reaguje na przeciwciała IgE")]),
  ("Mast cell", "mastocyte",
    "A cell packed with histamine granules. When triggered it releases them (degranulation): vessels widen and white cells pour in. It also causes allergy symptoms.",
    [("Diameter", "≈ 10–20 µm"), ("Granules", "histamine, heparin, proteases"), ("Allergy", "responds to IgE antibodies")]))
A["candidayeast"] = E(("Candida – drożdżak", "Candida albicans · forma drożdżowa",
    "Grzyb żyjący w jamie ustnej, jelitach i na skórze większości ludzi. Owalne drożdżaki można sfagocytować; niebezpieczny staje się, gdy przechodzi w strzępki.",
    [("Komórka", "≈ 4–6 µm"), ("Nosicielstwo", "większość zdrowych ludzi"), ("Choroba", "kandydoza (pleśniawki, grzybica)")]),
  ("Candida – yeast", "Candida albicans · yeast form",
    "A fungus living in the mouth, gut and on the skin of most people. The oval yeast cells can be phagocytosed; it becomes dangerous when it switches to hyphae.",
    [("Cell", "≈ 4–6 µm"), ("Carriage", "most healthy people"), ("Disease", "candidiasis (thrush)")]))
A["candidahypha"] = E(("Candida – strzępka", "Candida albicans · forma nitkowata",
    "Długie nici grzyba, zbyt duże dla fagocytów. Toksyna kandydalizyna przebija błony; neutrofile atakują strzępki sieciami NET.",
    [("Grubość", "≈ 2–4 µm"), ("Długość", "dziesiątki–setki µm"), ("Toksyna", "kandydalizyna (2016)")]),
  ("Candida – hypha", "Candida albicans · filamentous form",
    "Long fungal threads, too big for phagocytes. The toxin candidalysin punctures membranes; neutrophils fight hyphae with NETs.",
    [("Width", "≈ 2–4 µm"), ("Length", "tens to hundreds of µm"), ("Toxin", "candidalysin (2016)")]))
A["collagen"] = E(("Włókno kolagenowe", "kolagen typu I · macierz zewnątrzkomórkowa",
    "Najliczniejsze białko ssaków. Potrójne helisy łączą się we włókienka, a te we włókna mocne jak stal na rozciąganie. Tworzą trójwymiarową sieć, przez którą przeciskają się komórki.",
    [("Włókienko", "≈ 50–200 nm grubości"), ("Włókno", "≈ 1–20 µm"), ("Udział", "ok. 25–30% białek ciała"), ("Budowa", "powtórzenia Gly-X-Y, potrójna helisa")]),
  ("Collagen fibre", "type I collagen · extracellular matrix",
    "The most abundant protein in mammals. Triple helices assemble into fibrils and those into fibres as strong in tension as steel. They form a 3D mesh that cells squeeze through.",
    [("Fibril", "≈ 50–200 nm thick"), ("Fibre", "≈ 1–20 µm"), ("Share", "about 25–30% of body protein"), ("Structure", "Gly-X-Y repeats, triple helix")]))
A["net"] = E(("Sieć NET", "zewnątrzkomórkowa pułapka neutrofila",
    "Neutrofil w akcie samobójczym wyrzuca swoją chromatynę (DNA z histonami) obsadzoną enzymami. Lepka sieć więzi i zabija bakterie oraz strzępki grzybów.",
    [("Skład", "DNA, histony, elastaza, mieloperoksydaza"), ("Odkrycie", "Brinkmann i in., Science 2004"), ("Koszt", "neutrofil ginie (NEToza)")]),
  ("NET", "neutrophil extracellular trap",
    "In a suicidal act a neutrophil casts out its chromatin (DNA with histones) studded with enzymes. The sticky net traps and kills bacteria and fungal hyphae.",
    [("Made of", "DNA, histones, elastase, myeloperoxidase"), ("Discovered", "Brinkmann et al., Science 2004"), ("Cost", "the neutrophil dies (NETosis)")]))
A["lysis"] = E(("Rozpadająca się komórka", "liza / martwica",
    "Błona pękła, a zawartość komórki wylewa się na zewnątrz. Szczątki i sygnały „zagrożenia” (DAMP) przyciągają fagocyty, które sprzątają.",
    [("Przyczyny", "toksyny, fagi, brak tlenu, uszkodzenie"), ("Skutek", "stan zapalny, sprzątanie przez makrofagi")]),
  ("Disintegrating cell", "lysis / necrosis",
    "The membrane has burst and the cell's contents spill out. The debris and 'danger' signals (DAMPs) attract phagocytes that clean up.",
    [("Causes", "toxins, phages, lack of oxygen, damage"), ("Result", "inflammation, clean-up by macrophages")]))
# ------------------------------------------------------------------ staw: orzęski, wiciowce, glony
A["paramecium"] = E(("Pantofelek", "Paramecium caudatum · orzęsek",
    "Jednokomórkowiec pokryty tysiącami rzęsek bijących falą. Zagarnia bakterie do rowka gębowego; wodniczki tętniące wypompowują wodę. Po uderzeniu w przeszkodę cofa się i zmienia kierunek (reakcja unikania).",
    [("Długość", "≈ 180–300 µm"), ("Prędkość", "≈ 1 mm/s"), ("Pokarm", "bakterie, drobne glony"), ("Obrona", "trichocysty – wystrzeliwane igły")]),
  ("Paramecium", "Paramecium caudatum · ciliate",
    "A single cell covered with thousands of cilia beating in waves. It sweeps bacteria into its oral groove; contractile vacuoles pump out water. After hitting an obstacle it backs up and turns (avoidance reaction).",
    [("Length", "≈ 180–300 µm"), ("Speed", "≈ 1 mm/s"), ("Food", "bacteria, small algae"), ("Defence", "trichocysts – fired needles")]))
A["paramecium-bursaria"] = E(("Pantofelek zielony", "Paramecium bursaria · orzęsek z glonami",
    "Pantofelek, w którego cytoplazmie żyje kilkaset glonów Chlorella. Glony dają mu cukry z fotosyntezy, a on im – schronienie i transport do światła.",
    [("Długość", "≈ 80–150 µm"), ("Symbionty", "≈ 700 komórek Chlorella"), ("Relacja", "symbioza (mutualizm)")]),
  ("Green paramecium", "Paramecium bursaria · ciliate with algae",
    "A paramecium whose cytoplasm hosts hundreds of Chlorella algae. The algae give it sugars from photosynthesis; it gives them shelter and a ride to the light.",
    [("Length", "≈ 80–150 µm"), ("Symbionts", "≈ 700 Chlorella cells"), ("Relationship", "symbiosis (mutualism)")]))
A["didinium"] = E(("Didinium", "Didinium nasutum · drapieżny orzęsek",
    "Beczułkowaty drapieżnik z ryjkiem. Wbija w pantofelka toksycysty, paraliżuje go i połyka w całości – nawet ofiarę większą od siebie.",
    [("Długość", "≈ 60–200 µm"), ("Rzęski", "dwa wieńce"), ("Ofiara", "głównie pantofelki"), ("Głód", "tworzy cystę i czeka")]),
  ("Didinium", "Didinium nasutum · predatory ciliate",
    "A barrel-shaped predator with a snout. It spears a paramecium with toxicysts, paralyses it and swallows it whole – even prey larger than itself.",
    [("Length", "≈ 60–200 µm"), ("Cilia", "two girdles"), ("Prey", "mainly paramecia"), ("Hunger", "forms a cyst and waits")]))
A["chlamy"] = E(("Chlamydomonas", "Chlamydomonas reinhardtii · zielenica",
    "Jednokomórkowy glon z dwiema wiciami, które biją jak ręce pływaka żabką. Czerwona plamka oczna pozwala mu płynąć do światła.",
    [("Średnica", "≈ 10 µm"), ("Prędkość", "≈ 100–120 µm/s"), ("Chloroplast", "jeden, kubkowaty"), ("Znaczenie", "organizm modelowy")]),
  ("Chlamydomonas", "Chlamydomonas reinhardtii · green alga",
    "A single-celled alga with two flagella that beat like a breaststroke swimmer's arms. A red eyespot lets it swim towards light.",
    [("Diameter", "≈ 10 µm"), ("Speed", "≈ 100–120 µm/s"), ("Chloroplast", "one, cup-shaped"), ("Importance", "model organism")]))
A["loxodes"] = E(("Loxodes", "Loxodes magnus · orzęsek osadów",
    "Duży, spłaszczony orzęsek żyjący w mule, gdzie jest mało tlenu. Ma pęcherzyki Müllera – narządy wyczuwające grawitację.",
    [("Długość", "do ~0,6 mm"), ("Żyje", "osady jezior i stawów"), ("Zmysł", "grawitacja (pęcherzyki Müllera)")]),
  ("Loxodes", "Loxodes magnus · sediment ciliate",
    "A large, flattened ciliate living in low-oxygen mud. It has Müller vesicles – organelles that sense gravity.",
    [("Length", "up to ~0.6 mm"), ("Lives in", "lake and pond sediments"), ("Sense", "gravity (Müller vesicles)")]))
A["blepharisma"] = E(("Blepharisma", "Blepharisma japonicum · różowy orzęsek",
    "Orzęsek zabarwiony na różowo przez blefaryzminę – barwnik trujący dla drapieżników i wrażliwy na światło. Przy braku pokarmu niektóre osobniki stają się kanibalami-olbrzymami.",
    [("Długość", "≈ 150–300 µm"), ("Barwnik", "blefaryzmina"), ("Ciekawostka", "kanibalizm i gigantyzm")]),
  ("Blepharisma", "Blepharisma japonicum · pink ciliate",
    "A ciliate coloured pink by blepharismin – a pigment toxic to predators and sensitive to light. When food runs out, some become giant cannibals.",
    [("Length", "≈ 150–300 µm"), ("Pigment", "blepharismin"), ("Curiosity", "cannibalism and gigantism")]))
A["euglena"] = E(("Klejnotka", "Euglena gracilis · wiciowiec fotosyntetyzujący",
    "Zielony wiciowiec: fotosyntetyzuje jak roślina, a w ciemności wchłania pokarm jak zwierzę. Plamka oczna i fotoreceptor kierują go do umiarkowanego światła; potrafi też pełzać, zmieniając kształt (metabolia).",
    [("Długość", "≈ 50–85 µm"), ("Prędkość", "≈ 50–100 µm/s"), ("Zapas", "paramylon"), ("Ruch", "jedna długa wić")]),
  ("Euglena", "Euglena gracilis · photosynthetic flagellate",
    "A green flagellate: it photosynthesises like a plant and in darkness absorbs food like an animal. An eyespot and photoreceptor steer it to moderate light; it can also crawl by changing shape (metaboly).",
    [("Length", "≈ 50–85 µm"), ("Speed", "≈ 50–100 µm/s"), ("Storage", "paramylon"), ("Motion", "one long flagellum")]))
A["spirostomum"] = E(("Spirostomum", "Spirostomum ambiguum · orzęsek robakowaty",
    "Olbrzymi, robakowaty orzęsek widoczny gołym okiem. W kilka milisekund kurczy się do ułamka długości – to jeden z najszybszych ruchów w biologii – i wysyła falę toksyn ostrzegającą sąsiadów.",
    [("Długość", "do kilku mm"), ("Skurcz", "≈ 5 ms"), ("Mechanizm", "sieć centryny, nie aktyna-miozyna")]),
  ("Spirostomum", "Spirostomum ambiguum · worm-like ciliate",
    "A giant, worm-like ciliate visible to the naked eye. In a few milliseconds it contracts to a fraction of its length – one of the fastest movements in biology – and releases toxins that warn its neighbours.",
    [("Length", "up to several mm"), ("Contraction", "≈ 5 ms"), ("Mechanism", "centrin network, not actin–myosin")]))
A["stentor"] = E(("Trębacz", "Stentor coeruleus · orzęsek osiadły",
    "Orzęsek w kształcie trąbki, niebieski od barwnika stentoryny. Wirem rzęsek wciąga pokarm, a dotknięty kurczy się – przy powtarzanych bodźcach „przyzwyczaja się” (habituacja). Odtwarza całe ciało nawet z małego kawałka.",
    [("Długość", "do ~1–2 mm"), ("Barwnik", "stentoryna"), ("Zdolność", "regeneracja, habituacja")]),
  ("Trumpet animalcule", "Stentor coeruleus · sessile ciliate",
    "A trumpet-shaped ciliate, blue from the pigment stentorin. It draws food in with a ciliary vortex, contracts when touched and, with repeated stimuli, 'gets used to it' (habituation). It regrows its whole body even from a small piece.",
    [("Length", "up to ~1–2 mm"), ("Pigment", "stentorin"), ("Abilities", "regeneration, habituation")]))
A["coleps"] = E(("Coleps", "Coleps hirtus · orzęsek w pancerzu",
    "Beczułka okryta płytkami z węglanu wapnia. Poluje stadami: toksycystami i „gryzieniem” rozrywa inne orzęski i zjada padlinę.",
    [("Długość", "≈ 40–65 µm"), ("Pancerz", "płytki wapienne"), ("Dieta", "orzęski, glony, padlina")]),
  ("Coleps", "Coleps hirtus · armoured ciliate",
    "A little barrel covered with calcium-carbonate plates. It hunts in packs: with toxicysts and 'biting' it tears apart other ciliates and scavenges carrion.",
    [("Length", "≈ 40–65 µm"), ("Armour", "calcareous plates"), ("Diet", "ciliates, algae, carrion")]))
A["euplotes"] = E(("Euplotes", "Euplotes · orzęsek chodzący",
    "Płaski orzęsek, który zamiast pływać chodzi po podłożu na cirrach – pęczkach zlepionych rzęsek, działających jak nóżki.",
    [("Długość", "≈ 80–200 µm"), ("Ruch", "chodzenie na cirrach"), ("Pokarm", "bakterie, glony")]),
  ("Euplotes", "Euplotes · walking ciliate",
    "A flat ciliate that, instead of swimming, walks on the surface on cirri – bundles of fused cilia that work like legs.",
    [("Length", "≈ 80–200 µm"), ("Motion", "walking on cirri"), ("Food", "bacteria, algae")]))
A["tetrahymena"] = E(("Tetrahymena", "Tetrahymena thermophila · orzęsek modelowy",
    "Mały orzęsek, który przyniósł dwie Nagrody Nobla: tu odkryto rybozymy (Cech, 1989) oraz telomerazę chroniącą końce chromosomów (Blackburn i Greider, 2009).",
    [("Długość", "≈ 50 µm"), ("Pokarm", "bakterie"), ("Nagrody Nobla", "1989 i 2009")]),
  ("Tetrahymena", "Tetrahymena thermophila · model ciliate",
    "A small ciliate that led to two Nobel Prizes: ribozymes were discovered in it (Cech, 1989), and telomerase, which protects chromosome ends (Blackburn and Greider, 2009).",
    [("Length", "≈ 50 µm"), ("Food", "bacteria"), ("Nobel Prizes", "1989 and 2009")]))
A["bodo"] = E(("Bodo", "Bodo saltans · wiciowiec",
    "Maleńki wiciowiec z dwiema wiciami, który podskakuje w wodzie i zjada bakterie. To krewny świdrowców – też ma kinetoplast, olbrzymie DNA mitochondrium.",
    [("Długość", "≈ 4–8 µm"), ("Pokarm", "bakterie"), ("Krewni", "Trypanosoma (kinetoplastydy)")]),
  ("Bodo", "Bodo saltans · flagellate",
    "A tiny flagellate with two flagella that hops through the water eating bacteria. It is a relative of trypanosomes – it also has a kinetoplast, a giant mass of mitochondrial DNA.",
    [("Length", "≈ 4–8 µm"), ("Food", "bacteria"), ("Relatives", "Trypanosoma (kinetoplastids)")]))
A["nematode"] = E(("Nicień", "Nematoda · obleniec",
    "Nitkowaty robak, który pływa, wyginając ciało falą (ok. 2 razy na sekundę). Nicień C. elegans to jeden z najważniejszych organizmów modelowych – dorosły ma dokładnie 959 komórek somatycznych.",
    [("Długość", "≈ 1 mm (C. elegans)"), ("Ruch", "fala wygięć ~2 Hz"), ("Pokarm", "bakterie")]),
  ("Nematode", "Nematoda · roundworm",
    "A threadlike worm that swims by bending its body in a wave (about twice a second). The nematode C. elegans is a key model organism – the adult has exactly 959 somatic cells.",
    [("Length", "≈ 1 mm (C. elegans)"), ("Motion", "bending wave ~2 Hz"), ("Food", "bacteria")]))
A["oscillatoria"] = E(("Drgalnia", "Oscillatoria · sinica nitkowata",
    "Nitka komórek sinicy, która sunie po podłożu i kołysze końcem. Sinice wytwarzają tlen w fotosyntezie – ich przodkowie utlenili atmosferę Ziemi.",
    [("Grubość nitki", "≈ 4–10 µm"), ("Ruch", "ślizg (gliding)"), ("Energia", "fotosynteza tlenowa")]),
  ("Oscillatoria", "filamentous cyanobacterium",
    "A filament of cyanobacterial cells that glides along surfaces and sways its tip. Cyanobacteria make oxygen by photosynthesis – their ancestors oxygenated Earth's atmosphere.",
    [("Filament width", "≈ 4–10 µm"), ("Motion", "gliding"), ("Energy", "oxygenic photosynthesis")]))
A["helio"] = E(("Słonecznica", "Actinosphaerium · promienionóżka",
    "Kulka z promieniście sterczącymi, sztywnymi nibynóżkami (aksopodiami) usztywnionymi mikrotubulami. Ofiara, która ich dotknie, przykleja się i zostaje wciągnięta.",
    [("Średnica", "≈ 0,2–1 mm"), ("Wypustki", "aksopodia z mikrotubulami"), ("Pokarm", "orzęski, wrotki, glony")]),
  ("Sun animalcule", "Actinosphaerium · heliozoan",
    "A ball with rigid, radiating projections (axopodia) stiffened by microtubules. Prey that touches them sticks and is drawn in.",
    [("Diameter", "≈ 0.2–1 mm"), ("Projections", "axopodia with microtubules"), ("Food", "ciliates, rotifers, algae")]))
A["lacrymaria"] = E(("Łza łabędzia", "Lacrymaria olor · orzęsek drapieżny",
    "Orzęsek z niezwykle rozciągliwą „szyją”, która wysuwa się wielokrotnie dalej niż długość ciała i przeszukuje wodę w poszukiwaniu ofiar.",
    [("Ciało", "≈ 100 µm"), ("Szyja", "do 7–30× długości ciała"), ("Pokarm", "inne orzęski")]),
  ("Tear of the swan", "Lacrymaria olor · predatory ciliate",
    "A ciliate with an extraordinarily extensible 'neck' that reaches many times its body length and sweeps the water for prey.",
    [("Body", "≈ 100 µm"), ("Neck", "up to 7–30× body length"), ("Food", "other ciliates")]))
A["daphnia"] = E(("Rozwielitka", "Daphnia · wioślarka",
    "Maleńki skorupiak z przezroczystym pancerzem – widać bijące serce i jelito. Filtruje glony, bakterie i orzęski; latem rozmnaża się bez samców (partenogeneza).",
    [("Długość", "≈ 1–5 mm"), ("Pokarm", "glony, bakterie, orzęski"), ("Rozmnażanie", "partenogeneza")]),
  ("Water flea", "Daphnia · cladoceran",
    "A tiny crustacean with a see-through shell – you can see its beating heart and gut. It filters algae, bacteria and ciliates; in summer it reproduces without males (parthenogenesis).",
    [("Length", "≈ 1–5 mm"), ("Food", "algae, bacteria, ciliates"), ("Reproduction", "parthenogenesis")]))
A["volvox"] = E(("Toczek", "Volvox · kolonia zielenic",
    "Kula z tysięcy komórek podobnych do Chlamydomonas, które wiciami toczą ją przez wodę. W środku rosną kolonie potomne. Jeden z pierwszych przykładów podziału na komórki rozrodcze i somatyczne.",
    [("Średnica", "≈ 0,1–1 mm"), ("Komórki", "setki – dziesiątki tysięcy"), ("Rozmnażanie", "kolonie potomne w środku")]),
  ("Volvox", "Volvox · green-alga colony",
    "A sphere of thousands of Chlamydomonas-like cells whose flagella roll it through the water. Daughter colonies grow inside. An early example of splitting into germ and body cells.",
    [("Diameter", "≈ 0.1–1 mm"), ("Cells", "hundreds to tens of thousands"), ("Reproduction", "daughter colonies inside")]))
A["detritus"] = E(("Detrytus", "szczątki organiczne",
    "Kłaczki rozkładającej się materii – obumarłych roślin, glonów i zwierząt – obrośnięte bakteriami. Podstawa sieci pokarmowej w stawie i kryjówka dla drobnych organizmów.",
    [("Skład", "martwa materia + bakterie i grzyby"), ("Rola", "pokarm, kryjówka")]),
  ("Detritus", "organic debris",
    "Flakes of decaying matter – dead plants, algae and animals – overgrown with bacteria. The base of the pond food web and a hiding place for small organisms.",
    [("Made of", "dead matter + bacteria and fungi"), ("Role", "food, shelter")]))
A["diatom"] = E(("Okrzemka", "Navicula · glon w szklanym pancerzyku",
    "Glon zamknięty w pancerzyku z krzemionki – dwóch pasujących do siebie połówkach jak pudełko. Sunie po podłożu, wydzielając śluz przez szczelinę (rafę).",
    [("Długość", "≈ 10–100 µm"), ("Pancerzyk", "krzemionka (szkło)"), ("Znaczenie", "ok. 20% fotosyntezy na Ziemi")]),
  ("Diatom", "Navicula · alga in a glass shell",
    "An alga enclosed in a silica shell – two matching halves like a box. It glides along surfaces by secreting mucilage through a slit (raphe).",
    [("Length", "≈ 10–100 µm"), ("Shell", "silica (glass)"), ("Importance", "about 20% of Earth's photosynthesis")]))
A["cosmarium"] = E(("Desmidia", "Cosmarium · zielenica",
    "Glon z dwóch symetrycznych półkomórek połączonych przewężeniem. Desmidie lubią czyste, lekko kwaśne wody torfowisk.",
    [("Rozmiar", "≈ 20–100 µm"), ("Budowa", "dwie półkomórki"), ("Żyje", "torfowiska, czyste stawy")]),
  ("Desmid", "Cosmarium · green alga",
    "An alga made of two symmetrical half-cells joined by a narrow isthmus. Desmids like clean, slightly acidic bog water.",
    [("Size", "≈ 20–100 µm"), ("Structure", "two half-cells"), ("Lives in", "bogs, clean ponds")]))
A["closterium"] = E(("Desmidia półksiężycowata", "Closterium · zielenica",
    "Glon w kształcie półksiężyca. W wakuolach na obu końcach drgają kryształki (m.in. siarczanu baru).",
    [("Długość", "≈ 100–500 µm"), ("Ciekawostka", "drgające kryształy w wakuolach")]),
  ("Crescent desmid", "Closterium · green alga",
    "A crescent-shaped alga. In vacuoles at both ends tiny crystals (including barium sulfate) jiggle.",
    [("Length", "≈ 100–500 µm"), ("Curiosity", "jiggling crystals in vacuoles")]))
A["floc"] = E(("Kłaczek bakterii", "agregat bakterii w śluzie",
    "Skupisko bakterii sklejonych wydzielanym przez nie śluzem (EPS). Dla orzęsków i ameb to gotowy posiłek.",
    [("Rozmiar", "kilka – kilkadziesiąt µm"), ("Spoiwo", "polisacharydy, białka, DNA")]),
  ("Bacterial floc", "bacterial aggregate in slime",
    "A clump of bacteria glued together by the slime they secrete (EPS). A ready meal for ciliates and amoebae.",
    [("Size", "a few to tens of µm"), ("Glue", "polysaccharides, proteins, DNA")]))
A["amoeba"] = E(("Ameba", "Amoeba proteus · pełzak",
    "Komórka bez stałego kształtu, która pełznie, wypuszczając nibynóżki. Otacza pokarm i zamyka go w wodniczce; wodniczka tętniąca usuwa nadmiar wody.",
    [("Rozmiar", "≈ 200–800 µm"), ("Ruch", "nibynóżki, kilka µm/s"), ("Pokarm", "orzęski, glony, bakterie"), ("Rozmnażanie", "podział na dwie")]),
  ("Amoeba", "Amoeba proteus",
    "A cell with no fixed shape that crawls by extending pseudopods. It surrounds food and closes it in a vacuole; a contractile vacuole removes excess water.",
    [("Size", "≈ 200–800 µm"), ("Motion", "pseudopods, a few µm/s"), ("Food", "ciliates, algae, bacteria"), ("Reproduction", "splits in two")]))
A["rotifer"] = E(("Wrotek", "Philodina · wrotek bdelloidalny",
    "Wielokomórkowe zwierzę mniejsze od niektórych jednokomórkowców. Dwa wieńce rzęsek wyglądają jak kręcące się kółka i wciągają pokarm. Wrotki bdelloidalne od dziesiątek milionów lat rozmnażają się bez samców i przeżywają całkowite wyschnięcie.",
    [("Długość", "≈ 300–450 µm"), ("Szczęki", "mastaks mieli pokarm"), ("Przetrwanie", "anhydrobioza"), ("Ruch", "pełzanie jak pijawka")]),
  ("Rotifer", "Philodina · bdelloid rotifer",
    "A multicellular animal smaller than some single-celled organisms. Two ciliary crowns look like spinning wheels and draw food in. Bdelloid rotifers have reproduced without males for tens of millions of years and survive complete drying.",
    [("Length", "≈ 300–450 µm"), ("Jaws", "the mastax grinds food"), ("Survival", "anhydrobiosis"), ("Motion", "inchworm crawling")]))
A["vorticella"] = E(("Wirczyk", "Vorticella · orzęsek na trzonku",
    "Dzwoneczek na sprężystym trzonku. Przy dotknięciu trzonek w milisekundy zwija się w sprężynkę – napędza go białkowa sprężyna (spazmonem) zasilana jonami wapnia, bez ATP.",
    [("Dzwonek", "≈ 50–100 µm"), ("Skurcz", "kilka milisekund"), ("Napęd", "spazmina + Ca²⁺")]),
  ("Vorticella", "Vorticella · stalked ciliate",
    "A little bell on an elastic stalk. When touched the stalk coils into a spring in milliseconds – powered by a protein spring (spasmoneme) driven by calcium ions, not ATP.",
    [("Bell", "≈ 50–100 µm"), ("Contraction", "a few milliseconds"), ("Drive", "spasmin + Ca²⁺")]))
A["spirocell"] = E(("Skrętnica", "Spirogyra · zielenica nitkowata",
    "Nitka walcowatych komórek, w których chloroplasty skręcają się w spiralne wstęgi. Tworzy zielone „waty” w stawach.",
    [("Grubość nitki", "≈ 10–100 µm"), ("Chloroplast", "spiralna wstęga"), ("Rozmnażanie", "koniugacja nitek")]),
  ("Spirogyra", "filamentous green alga",
    "A thread of cylindrical cells in which the chloroplasts wind into spiral ribbons. It forms green 'cotton' mats in ponds.",
    [("Filament width", "≈ 10–100 µm"), ("Chloroplast", "spiral ribbon"), ("Reproduction", "conjugation between threads")]))
# ------------------------------------------------------------------ mech: niesporczaki
A["tardigrade"] = E(("Niesporczak", "Hypsibius exemplaris · Tardigrada",
    "Ośmionogi „miś wodny” chodzący jak owad. Wysysa glony sztyletami. Gdy mech wysycha, zwija się w beczułkę i przechodzi w anhydrobiozę – niesporczaki przeżyły nawet ekspozycję na próżnię kosmiczną.",
    [("Długość", "≈ 250 µm"), ("Chód", "≈ 163 µm/s (pół długości ciała na s)"), ("Przetrwanie", "beczułka – lata bez wody"), ("Nogi", "4 pary z pazurkami")]),
  ("Tardigrade", "Hypsibius exemplaris · Tardigrada",
    "An eight-legged 'water bear' that walks like an insect. It sucks out algae with stylets. When the moss dries it curls into a tun and enters anhydrobiosis – tardigrades have even survived exposure to the vacuum of space.",
    [("Length", "≈ 250 µm"), ("Walking", "≈ 163 µm/s (half a body length per s)"), ("Survival", "tun – years without water"), ("Legs", "4 pairs with claws")]))
A["milnesium"] = E(("Niesporczak drapieżny", "Milnesium · Tardigrada",
    "Duży niesporczak z szerokim otworem gębowym. Poluje na wrotki, nicienie i mniejsze niesporczaki, przebijając ich oskórek sztyletami.",
    [("Długość", "do ~0,7 mm"), ("Ofiary", "wrotki, nicienie, niesporczaki")]),
  ("Predatory tardigrade", "Milnesium · Tardigrada",
    "A large tardigrade with a wide mouth. It hunts rotifers, nematodes and smaller tardigrades, piercing their cuticle with stylets.",
    [("Length", "up to ~0.7 mm"), ("Prey", "rotifers, nematodes, tardigrades")]))
A["mossleaf"] = E(("Listek mchu", "Bryophyta · mech",
    "Blaszka z jednej warstwy komórek z chloroplastami i żeberkiem pośrodku. Między listkami utrzymuje się cienka błonka wody – świat niesporczaków, wrotków i nicieni.",
    [("Grubość", "jedna warstwa komórek"), ("Rola", "zatrzymuje wodę"), ("Mieszkańcy", "niesporczaki, wrotki, nicienie")]),
  ("Moss leaf", "Bryophyta · moss",
    "A blade one cell layer thick, with chloroplasts and a midrib. A thin film of water stays between the leaves – the world of tardigrades, rotifers and nematodes.",
    [("Thickness", "one cell layer"), ("Role", "holds water"), ("Inhabitants", "tardigrades, rotifers, nematodes")]))
A["exuvia"] = E(("Wylinka z jajami", "zrzucony oskórek niesporczaka",
    "Niesporczak rośnie, linijąc. Samica Hypsibius składa jaja do starego oskórka, który zostaje jak kołyska; młode wykluwają się po kilku dniach.",
    [("Jaja", "zwykle kilka"), ("Rozmnażanie", "partenogeneza")]),
  ("Moult with eggs", "shed tardigrade cuticle",
    "A tardigrade grows by moulting. A Hypsibius female lays eggs inside her old cuticle, which stays behind like a cradle; the young hatch after a few days.",
    [("Eggs", "usually a few"), ("Reproduction", "parthenogenesis")]))
A["chlorella"] = E(("Chlorella", "Chlorella · zielenica jednokomórkowa",
    "Kulisty glon pełen chlorofilu, jeden z najpospolitszych na świecie. Pokarm niesporczaków i symbiont pantofelka zielonego.",
    [("Średnica", "≈ 2–10 µm"), ("Energia", "fotosynteza"), ("Rozmnażanie", "autospory")]),
  ("Chlorella", "Chlorella · unicellular green alga",
    "A spherical alga full of chlorophyll, one of the commonest in the world. Food for tardigrades and a symbiont of the green paramecium.",
    [("Diameter", "≈ 2–10 µm"), ("Energy", "photosynthesis"), ("Reproduction", "autospores")]))
# ------------------------------------------------------------------ Dictyostelium
A["dictycell"] = E(("Ameba społeczna", "Dictyostelium discoideum",
    "Ameba z leśnej ściółki. Dopóki są bakterie, żyje samotnie i dzieli się; w głodzie komórki wysyłają fale cAMP, zbiegają się w tysiące i tworzą wspólny organizm.",
    [("Średnica", "≈ 10 µm"), ("Sygnał", "cykliczny AMP"), ("Pełzanie", "≈ 8–16 µm/min")]),
  ("Social amoeba", "Dictyostelium discoideum",
    "An amoeba from forest leaf litter. While bacteria last it lives alone and divides; when starving the cells send out cAMP waves, stream together in thousands and form a joint organism.",
    [("Diameter", "≈ 10 µm"), ("Signal", "cyclic AMP"), ("Crawling", "≈ 8–16 µm/min")]))
A["mound"] = E(("Agregat", "kopczyk Dictyostelium",
    "Zbiorowisko do ~100 000 ameb, które zeszły się na sygnał cAMP. Z czubka kopczyka powstanie ślimak.",
    [("Komórki", "do ~100 000"), ("Następny etap", "ślimak")]),
  ("Aggregate", "Dictyostelium mound",
    "A gathering of up to ~100,000 amoebae that came together on a cAMP signal. A slug will form from the mound's tip.",
    [("Cells", "up to ~100,000"), ("Next stage", "the slug")]))
A["slug"] = E(("Ślimak (pseudoplazmodium)", "Dictyostelium discoideum",
    "Tysiące ameb pełznących razem jak jeden organizm, w pochwie ze śluzu. Kieruje się do światła i ciepła, szukając miejsca na owocnik.",
    [("Długość", "≈ 1–2 mm"), ("Kierunek", "światło, ciepło"), ("Przód", "przyszły trzonek")]),
  ("Slug (pseudoplasmodium)", "Dictyostelium discoideum",
    "Thousands of amoebae crawling together as one organism in a slime sheath. It heads towards light and warmth, looking for a place to fruit.",
    [("Length", "≈ 1–2 mm"), ("Direction", "light, warmth"), ("Front", "future stalk")]))
A["fruitbody"] = E(("Owocnik", "Dictyostelium · trzonek i zarodnie",
    "Ok. 20% komórek ginie, budując trzonek z celulozy, a reszta zostaje zarodnikami na szczycie. Altruizm na poziomie komórek – komórki trzonka pomagają spokrewnionym zarodnikom.",
    [("Wysokość", "≈ 1–2 mm"), ("Trzonek", "≈ 20% komórek (giną)"), ("Zarodniki", "przetrwają suszę")]),
  ("Fruiting body", "Dictyostelium · stalk and spore head",
    "About 20% of the cells die building a cellulose stalk, while the rest become spores on top. Altruism at the cell level – stalk cells help related spores.",
    [("Height", "≈ 1–2 mm"), ("Stalk", "≈ 20% of cells (they die)"), ("Spores", "survive drought")]))
# ------------------------------------------------------------------ rozród
A["sperm"] = E(("Plemnik", "Homo sapiens · gameta męska",
    "Komórka z głową pełną DNA ojca, wstawką z mitochondriami i długą witką bijącą ~15–25 razy na sekundę. Z ~200 mln plemników do komórki jajowej dociera najwyżej kilkaset.",
    [("Długość", "≈ 55 µm (główka 4,5 µm)"), ("Prędkość", "≈ 25–60 µm/s"), ("DNA", "haploidalny zestaw (23 chromosomy)"), ("Życie", "do ~5 dni w drogach rodnych")]),
  ("Sperm cell", "Homo sapiens · male gamete",
    "A cell with a head full of the father's DNA, a midpiece of mitochondria and a long tail beating ~15–25 times a second. Out of ~200 million sperm, at most a few hundred reach the egg.",
    [("Length", "≈ 55 µm (head 4.5 µm)"), ("Speed", "≈ 25–60 µm/s"), ("DNA", "haploid set (23 chromosomes)"), ("Lifespan", "up to ~5 days in the female tract")]))
A["oocyte"] = E(("Komórka jajowa", "oocyt II rzędu · Homo sapiens",
    "Największa komórka ludzkiego ciała, widoczna prawie gołym okiem. Otacza ją osłonka przejrzysta i wzgórek jajonośny. Po wniknięciu jednego plemnika osłonka twardnieje.",
    [("Średnica", "≈ 110–120 µm"), ("Osłonka", "≈ 13–15 µm (ZP1–ZP4)"), ("Czas", "zdolna do zapłodnienia ~12–24 h")]),
  ("Egg cell", "secondary oocyte · Homo sapiens",
    "The largest cell in the human body, almost visible to the naked eye. It is surrounded by the zona pellucida and the cumulus. Once one sperm enters, the zona hardens.",
    [("Diameter", "≈ 110–120 µm"), ("Zona", "≈ 13–15 µm (ZP1–ZP4)"), ("Window", "fertilisable for ~12–24 h")]))
# ------------------------------------------------------------------ nowotwór i odporność
A["tumorcell"] = E(("Komórka nowotworowa", "rak (carcinoma)",
    "Komórka nabłonka, która przez mutacje wymknęła się kontroli podziałów. Ma duże, nieregularne jądro z wyraźnymi jąderkami. Może przejść EMT, wędrować, wnikać do naczyń i tworzyć przerzuty.",
    [("Średnica", "≈ 15–20 µm"), ("Cykl", "≈ 1–2 doby"), ("Przerzuty", "zdecydowana większość krążących komórek ginie")]),
  ("Cancer cell", "carcinoma",
    "An epithelial cell that escaped the control of division through mutations. It has a large irregular nucleus with prominent nucleoli. It can undergo EMT, migrate, enter vessels and form metastases.",
    [("Diameter", "≈ 15–20 µm"), ("Cycle", "≈ 1–2 days"), ("Metastasis", "the vast majority of circulating cells die")]))
A["ctl"] = E(("Limfocyt T cytotoksyczny", "limfocyt T CD8⁺",
    "Zabójca komórek: rozpoznaje obcy fragment białka na MHC-I, tworzy synapsę i wstrzykuje perforynę z granzymami. Pojedyncze trafienie rzadko zabija – uszkodzenia się sumują.",
    [("Średnica", "≈ 7–10 µm"), ("Rozpoznaje", "antygen na MHC-I"), ("Hamulec", "PD-1 / PD-L1")]),
  ("Cytotoxic T cell", "CD8⁺ T lymphocyte",
    "A cell killer: it recognises a foreign protein fragment on MHC-I, forms a synapse and injects perforin with granzymes. A single hit rarely kills – the damage adds up.",
    [("Diameter", "≈ 7–10 µm"), ("Recognises", "antigen on MHC-I"), ("Brake", "PD-1 / PD-L1")]))
A["nk"] = E(("Komórka NK", "naturalny zabójca",
    "Limfocyt odporności wrodzonej. Zabija komórki, na których brakuje cząsteczek MHC-I („brak swojego”) – tak wyłapuje komórki zakażone wirusem i nowotworowe.",
    [("Średnica", "≈ 10–12 µm"), ("Zasada", "„brak swojego” (1990)"), ("We krwi", "ok. 5–15% limfocytów")]),
  ("NK cell", "natural killer",
    "An innate-immunity lymphocyte. It kills cells that lack MHC-I molecules ('missing self') – that is how it catches virus-infected and cancer cells.",
    [("Diameter", "≈ 10–12 µm"), ("Principle", "'missing self' (1990)"), ("In blood", "about 5–15% of lymphocytes")]))
A["trypanosome"] = E(("Świdrowiec", "Trypanosoma brucei · pasożyt krwi",
    "Wiciowiec wywołujący śpiączkę afrykańską, przenoszony przez muchę tse-tse. Płaszcz jednego białka VSG co pewien czas wymienia na inny, więc przeciwciała nie nadążają.",
    [("Długość", "≈ 20–30 µm"), ("Podział", "co ~6 h"), ("Płaszcz", "~10 mln cząsteczek VSG"), ("Geny VSG", "ok. 2000")]),
  ("Trypanosome", "Trypanosoma brucei · blood parasite",
    "A flagellate that causes African sleeping sickness, carried by the tsetse fly. From time to time it swaps its coat of a single VSG protein for another, so antibodies cannot keep up.",
    [("Length", "≈ 20–30 µm"), ("Division", "every ~6 h"), ("Coat", "~10 million VSG molecules"), ("VSG genes", "about 2000")]))
A["kupffer"] = E(("Komórka Kupffera", "makrofag osiadły wątroby",
    "Makrofag wyściełający naczynia zatokowe wątroby. Wyłapuje z krwi bakterie, zużyte krwinki i pasożyty oznaczone przeciwciałami lub dopełniaczem.",
    [("Miejsce", "zatoki wątroby"), ("Zadanie", "filtr krwi"), ("Udział", "największa pula makrofagów w ciele")]),
  ("Kupffer cell", "resident liver macrophage",
    "A macrophage lining the liver's sinusoids. It catches bacteria, worn-out blood cells and parasites tagged with antibodies or complement from the blood.",
    [("Location", "liver sinusoids"), ("Job", "blood filter"), ("Share", "the body's largest macrophage pool")]))
A["nanobot"] = E(("Nanobot", "maszyna – obiekt fikcyjny",
    "Tryb eksploracji. Takich robotów jeszcze nie ma – to wyobrażenie. Prawdziwe mikroroboty (np. napędzane polem magnetycznym) mają zwykle co najmniej kilka mikrometrów.",
    [("Status", "fikcja naukowa"), ("Laser", "tylko w grze")]),
  ("Nanobot", "machine – fictional object",
    "The exploration mode. Such robots do not exist yet – it is imagination. Real microrobots (e.g. driven by magnetic fields) are usually at least several micrometres in size.",
    [("Status", "science fiction"), ("Laser", "game only")]))
# ------------------------------------------------------------------ tło i środowisko
A["tem"] = E(("Obraz z mikroskopu elektronowego", "TEM · transmisyjny mikroskop elektronowy",
    "Zamiast światła – wiązka elektronów, dlatego widać obiekty tysiące razy mniejsze niż w mikroskopie świetlnym, np. pojedyncze wirusy. Preparat leży w próżni; barwy dodaje się komputerowo.",
    [("Rozdzielczość", "poniżej 1 nm"), ("Barwienie", "negatywne – sole metali ciężkich"), ("Kolory", "sztuczne (fałszywe barwy)")]),
  ("Electron microscope image", "TEM · transmission electron microscope",
    "An electron beam instead of light, so objects thousands of times smaller than in a light microscope become visible – single viruses, for example. The sample sits in a vacuum; colours are added by computer.",
    [("Resolution", "below 1 nm"), ("Staining", "negative – heavy-metal salts"), ("Colours", "artificial (false colour)")]))
A["biofilm"] = E(("Biofilm", "wspólnota bakterii w macierzy",
    "Bakterie przyklejone do powierzchni i zatopione w wydzielanym śluzie (EPS) z kanalikami na wodę. W biofilmie bakterie są wielokrotnie bardziej odporne na antybiotyki.",
    [("Macierz", "polisacharydy, białka, DNA"), ("Tolerancja", "do ~1000× więcej antybiotyku"), ("Przykłady", "płytka nazębna, kamień w rurach")]),
  ("Biofilm", "bacterial community in a matrix",
    "Bacteria stuck to a surface and embedded in secreted slime (EPS) with water channels. In a biofilm bacteria are many times more resistant to antibiotics.",
    [("Matrix", "polysaccharides, proteins, DNA"), ("Tolerance", "up to ~1000× more antibiotic"), ("Examples", "dental plaque, pipe scale")]))
A["venule"] = E(("Żyłka pozawłośniczkowa", "naczynie krwionośne",
    "Najmniejsze żyły zaraz za naczyniami włosowatymi. Tu leukocyty toczą się po ścianie, przylegają i przeciskają się do tkanki (diapedeza). Krew płynie ok. 0,5–2 mm/s.",
    [("Średnica", "≈ 10–50 µm"), ("Ściana", "śródbłonek + pericyty"), ("Zadanie", "wyjście leukocytów do tkanki")]),
  ("Post-capillary venule", "blood vessel",
    "The smallest veins right after the capillaries. Here white cells roll along the wall, stick and squeeze into the tissue (diapedesis). Blood flows at about 0.5–2 mm/s.",
    [("Diameter", "≈ 10–50 µm"), ("Wall", "endothelium + pericytes"), ("Job", "exit of white cells into tissue")]))
A["endothelium"] = E(("Śródbłonek", "ściana naczynia",
    "Pojedyncza warstwa płaskich komórek wyściełająca wszystkie naczynia. Decyduje, co przechodzi z krwi do tkanki; w zapaleniu wystawia cząsteczki, na których „hamują” leukocyty.",
    [("Grubość", "≈ 0,2–2 µm"), ("Powierzchnia", "setki m² w całym ciele"), ("Cząsteczki", "selektyny, integryny, VE-kadheryna")]),
  ("Endothelium", "vessel wall",
    "A single layer of flat cells lining every vessel. It decides what passes from blood into tissue; in inflammation it displays molecules on which white cells 'brake'.",
    [("Thickness", "≈ 0.2–2 µm"), ("Area", "hundreds of m² in the whole body"), ("Molecules", "selectins, integrins, VE-cadherin")]))
A["interstitium"] = E(("Tkanka łączna", "przestrzeń śródmiąższowa",
    "Przestrzeń między komórkami wypełniona żelem z kwasu hialuronowego i proteoglikanów, poprzecinanym włóknami kolagenu i elastyny. Płyn tkankowy przesącza się przez nią ~0,1–1 µm/s.",
    [("Włókna", "kolagen, elastyna"), ("Żel", "kwas hialuronowy, proteoglikany"), ("Mieszkańcy", "fibroblasty, makrofagi, mastocyty")]),
  ("Connective tissue", "interstitial space",
    "The space between cells filled with a gel of hyaluronan and proteoglycans, crossed by collagen and elastin fibres. Tissue fluid seeps through it at ~0.1–1 µm/s.",
    [("Fibres", "collagen, elastin"), ("Gel", "hyaluronan, proteoglycans"), ("Residents", "fibroblasts, macrophages, mast cells")]))
A["sprout"] = E(("Kiełek naczynia", "angiogeneza",
    "Nowe naczynie wyrastające z istniejącego. Prowadzi je komórka szczytowa z filopodiami, która podąża za VEGF z niedotlenionych komórek. Guzy wykorzystują to, by się odżywiać.",
    [("Sygnał", "VEGF (z niedotlenienia, HIF-1α)"), ("Tempo", "≈ 0,2 mm na dobę (rogówka królika)"), ("Odkrywca", "Judah Folkman, 1971")]),
  ("Vessel sprout", "angiogenesis",
    "A new vessel growing out of an existing one. It is led by a tip cell with filopodia that follows VEGF from oxygen-starved cells. Tumours exploit this to feed themselves.",
    [("Signal", "VEGF (from hypoxia, HIF-1α)"), ("Rate", "≈ 0.2 mm per day (rabbit cornea)"), ("Pioneer", "Judah Folkman, 1971")]))
A["capillary"] = E(("Naczynie włosowate", "kapilara · najmniejsze naczynie krwionośne",
    "Rurka z jednej warstwy komórek śródbłonka, tak wąska, że erytrocyty przeciskają się przez nią gęsiego, wygięte w „spadochron”. Tu krew oddaje tlen i odbiera dwutlenek węgla. Gęsta sieć kapilar sprawia, że żadna komórka zdrowej tkanki nie leży daleko od krwi; w litym guzie komórki ściskają kapilary i przepływ ustaje.",
    [("Średnica", "≈ 5–8 µm"), ("Ściana", "śródbłonek + pericyty"), ("Zasięg tlenu", "≈ 100–200 µm od naczynia"), ("Przepływ", "≈ 0,3–1 mm/s")]),
  ("Capillary", "the smallest blood vessel",
    "A tube made of a single layer of endothelial cells, so narrow that red blood cells squeeze through in single file, bent into a 'parachute'. Here blood gives up oxygen and takes up carbon dioxide. The dense capillary network keeps every cell of healthy tissue close to blood; in a solid tumour, cells squeeze capillaries shut and flow stops.",
    [("Diameter", "≈ 5–8 µm"), ("Wall", "endothelium + pericytes"), ("Oxygen reach", "≈ 100–200 µm from the vessel"), ("Flow", "≈ 0.3–1 mm/s")]))
A["caf"] = E(("Fibroblast związany z nowotworem (CAF)", "miofibroblast α-SMA⁺ · zrąb guza",
    "Fibroblast przestawiony przez komórki raka: większy, z grubymi włóknami naprężeniowymi. Otacza guz i odkłada gęsty, sztywny kolagen (desmoplazja), porządkuje jego włókna w „szyny”, po których wędrują komórki raka, i często sam prowadzi ich inwazję.",
    [("Znacznik", "α-SMA, FAP"), ("Robi", "kolagen, sztywny zrąb, czynniki wzrostu"), ("Skutek", "ułatwia inwazję, utrudnia wejście limfocytom T")]),
  ("Cancer-associated fibroblast (CAF)", "α-SMA⁺ myofibroblast · tumour stroma",
    "A fibroblast reprogrammed by cancer cells: larger, with thick stress fibres. It surrounds the tumour and lays down dense, stiff collagen (desmoplasia), aligns the fibres into 'rails' that cancer cells migrate along, and often leads their invasion itself.",
    [("Marker", "α-SMA, FAP"), ("Makes", "collagen, stiff stroma, growth factors"), ("Effect", "helps invasion, keeps T cells out")]))
A["necrosis"] = E(("Komórka martwicza", "martwica (necrosis)",
    "Komórka, która zginęła z braku tlenu. Najpierw pęcznieje, potem jej błona pęka, a jądro rozpada się na okruchy (kariorreksja) albo blednie (karioliza). Zostaje blady „cień” komórki. W większych guzach środek, najdalej od naczyń, zamienia się w pole martwicy.",
    [("Przyczyna", "anoksja, ≳ 150–200 µm od drożnego naczynia"), ("Wygląd", "blady cień, okruchy chromatyny"), ("Skutek", "zapalenie – w przeciwieństwie do apoptozy")]),
  ("Necrotic cell", "necrosis",
    "A cell that died from lack of oxygen. First it swells, then its membrane ruptures and the nucleus breaks into fragments (karyorrhexis) or fades away (karyolysis). A pale 'ghost' of the cell remains. In larger tumours the centre, farthest from vessels, turns into a field of necrosis.",
    [("Cause", "anoxia, ≳ 150–200 µm from an open vessel"), ("Look", "pale ghost, chromatin fragments"), ("Effect", "inflammation – unlike apoptosis")]))
A["apoptotic-body"] = E(("Ciałka apoptotyczne", "apoptoza · programowana śmierć komórki",
    "Tak kończy komórka, która uruchomiła program samobójczy (np. p53 w niedotlenieniu albo po ataku limfocytu T lub NK): obkurcza się, jej chromatyna zbija się w gęste grudki, a komórka rozpada na otoczone błoną kulki. Makrofagi zjadają je w ciągu godzin, bez zapalenia.",
    [("Wielkość", "≈ 1–5 µm"), ("Sygnał „zjedz mnie”", "fosfatydyloseryna na powierzchni"), ("Sprzątanie", "eferocytoza – makrofagi")]),
  ("Apoptotic bodies", "apoptosis · programmed cell death",
    "The end of a cell that switched on its suicide programme (e.g. p53 under hypoxia, or after an attack by a T or NK cell): it shrinks, its chromatin condenses into dense clumps and the cell breaks up into membrane-bound beads. Macrophages eat them within hours, without inflammation.",
    [("Size", "≈ 1–5 µm"), ("'Eat me' signal", "phosphatidylserine on the surface"), ("Clearance", "efferocytosis – macrophages")]))
A["squamous"] = E(("Komórka nabłonka pochwy", "złuszczona komórka nabłonka wielowarstwowego płaskiego",
    "Pochwę wyściela nabłonek wielowarstwowy płaski, którego górne warstwy stale się złuszczają. W płynie pochwy leżą więc duże, płaskie, wielokątne komórki z małym, zbitym jądrem. Są pełne glikogenu, z którego pałeczki kwasu mlekowego robią kwas mlekowy – stąd kwaśne pH pochwy.",
    [("Wielkość", "≈ 40–60 µm, płaska"), ("Jądro", "małe, zbite (pyknotyczne)"), ("Zawiera", "glikogen – pokarm pałeczek kwasu mlekowego")]),
  ("Vaginal epithelial cell", "shed cell of stratified squamous epithelium",
    "The vagina is lined by stratified squamous epithelium whose top layers are constantly shed. Its fluid therefore holds large, flat, polygonal cells with a small, dense nucleus. They are full of glycogen, which lactobacilli turn into lactic acid – hence the acidic vaginal pH.",
    [("Size", "≈ 40–60 µm, flat"), ("Nucleus", "small, dense (pyknotic)"), ("Contains", "glycogen – food for lactobacilli")]))
A["lactobacillus"] = E(("Pałeczki kwasu mlekowego", "Lactobacillus (np. L. crispatus, L. iners) · flora pochwy",
    "Nieruchome, Gram-dodatnie pałeczki, które dominują w zdrowej pochwie. Fermentują cukry z glikogenu w kwas mlekowy i utrzymują pH ≈ 4, a to chroni przed bakteriami chorobotwórczymi i grzybami. Po stosunku pH na kilka godzin rośnie, a potem pałeczki znów je obniżają.",
    [("Rozmiar", "≈ 2–4 × 0,8 µm"), ("Ruch", "brak – tylko ruchy Browna"), ("Robi", "kwas mlekowy, pH ≈ 3,8–4,5"), ("Znaczenie", "„dobra flora” pochwy")]),
  ("Lactobacilli", "Lactobacillus (e.g. L. crispatus, L. iners) · vaginal flora",
    "Non-motile Gram-positive rods that dominate the healthy vagina. They ferment sugars from glycogen into lactic acid and keep the pH near 4, which protects against pathogenic bacteria and fungi. After intercourse the pH rises for a few hours, then the lactobacilli lower it again.",
    [("Size", "≈ 2–4 × 0.8 µm"), ("Movement", "none – Brownian motion only"), ("Makes", "lactic acid, pH ≈ 3.8–4.5"), ("Role", "the 'good flora' of the vagina")]))
A["semen-coagulum"] = E(("Skrzep nasienia", "semenogeliny · upłynnienie przez PSA",
    "Tuż po wytrysku nasienie krzepnie w galaretę: białka z pęcherzyków nasiennych (semenogeliny) tworzą sieć, w której plemniki poruszają się z trudem. Enzym prostaty PSA tnie tę sieć i nasienie się upłynnia – zwykle w ciągu kilkunastu minut.",
    [("Krzepnie", "w ~1 min po wytrysku"), ("Upłynnia się", "zwykle w ~15 min (WHO: najpóźniej po 60 min)"), ("Enzym", "PSA – swoisty antygen sterczowy")]),
  ("Semen coagulum", "semenogelins · liquefaction by PSA",
    "Right after ejaculation semen clots into a gel: proteins from the seminal vesicles (semenogelins) form a network in which sperm can barely move. The prostate enzyme PSA cuts this network and the semen liquefies – usually within about fifteen minutes.",
    [("Clots", "~1 min after ejaculation"), ("Liquefies", "usually in ~15 min (WHO: within 60 min)"), ("Enzyme", "PSA – prostate-specific antigen")]))
A["pond-water"] = E(("Woda w stawie", "słodkowodne mikrośrodowisko",
    "Kropla wody ze stawu to cały ekosystem: bakterie, glony, pierwotniaki i drobne zwierzęta. W 1 ml żyje zwykle około miliona bakterii.",
    [("Bakterie", "≈ 10⁶ w 1 ml"), ("Lepkość", "≈ 1 mPa·s (20 °C)"), ("Dla mikrobów", "świat bez bezwładności (niska liczba Reynoldsa)")]),
  ("Pond water", "freshwater microhabitat",
    "A drop of pond water is a whole ecosystem: bacteria, algae, protists and tiny animals. One millilitre usually holds about a million bacteria.",
    [("Bacteria", "≈ 10⁶ per ml"), ("Viscosity", "≈ 1 mPa·s (20 °C)"), ("For microbes", "a world without inertia (low Reynolds number)")]))
A["bacteria-cloud"] = E(("Chmura bakterii", "zagęszczenie bakterii w wodzie",
    "Miejsce, gdzie bakterie namnożyły się przy rozkładającej się materii. Dla pantofelków i innych orzęsków to stołówka – zjadają tysiące bakterii na godzinę.",
    [("Źródło", "rozkład materii organicznej"), ("Zjadacze", "orzęski, wiciowce, wrotki")]),
  ("Bacterial cloud", "dense patch of bacteria in water",
    "A spot where bacteria multiplied around decaying matter. For paramecia and other ciliates it is a canteen – they eat thousands of bacteria per hour.",
    [("Source", "decomposing organic matter"), ("Eaters", "ciliates, flagellates, rotifers")]))
A["lemna"] = E(("Cień rzęsy wodnej", "Lemna · pływająca roślina",
    "Liście rzęsy wodnej unoszą się na powierzchni i rzucają cień. Dla glonów i klejnotek to miejsce ze słabszym światłem – czasem ratunek przed zbyt silnym słońcem.",
    [("Liść rzęsy", "≈ 2–5 mm"), ("Światło", "cień obniża natężenie wielokrotnie")]),
  ("Duckweed shade", "Lemna · floating plant",
    "Duckweed leaves float on the surface and cast shade. For algae and euglenas it is a spot with weaker light – sometimes a refuge from too strong sun.",
    [("Duckweed leaf", "≈ 2–5 mm"), ("Light", "shade cuts intensity many times")]))
A["moss-water"] = E(("Błonka wody w mchu", "mikrosiedlisko poduszki mchu",
    "Cienka warstwa wody między listkami mchu. Gdy wysycha, niesporczaki, wrotki i nicienie wpadają w anhydrobiozę i czekają na deszcz.",
    [("Grubość", "mikrometry – milimetry"), ("Zagrożenie", "wysychanie w ciągu godzin")]),
  ("Water film in moss", "moss-cushion microhabitat",
    "A thin layer of water between moss leaves. When it dries, tardigrades, rotifers and nematodes enter anhydrobiosis and wait for rain.",
    [("Thickness", "micrometres to millimetres"), ("Threat", "drying within hours")]))
A["agar"] = E(("Agar", "podłoże hodowlane",
    "Galareta z krasnorostów, na której w laboratorium hoduje się mikroby. Ameby społeczne pełzają po jej powierzchni i zjadają rosnące na niej bakterie.",
    [("Źródło", "krasnorosty (agaroza)"), ("Topnienie", "≈ 85 °C"), ("Zastygnięcie", "≈ 32–40 °C")]),
  ("Agar", "culture medium",
    "A jelly made from red seaweed on which microbes are grown in the lab. Social amoebae crawl on its surface and eat the bacteria growing on it.",
    [("Source", "red algae (agarose)"), ("Melts at", "≈ 85 °C"), ("Sets at", "≈ 32–40 °C")]))
A["klebsiella"] = E(("Murawa bakterii", "Klebsiella aerogenes · pokarm ameb",
    "Gęsty kożuch bakterii na agarze. W laboratoriach ameby Dictyostelium karmi się zwykle bakterią Klebsiella; tam, gdzie je zjadły, zostają jaśniejsze „łysinki”.",
    [("Bakteria", "pałeczka ≈ 1–2 µm"), ("Gęstość", "≈ 1 komórka na µm²")]),
  ("Bacterial lawn", "Klebsiella aerogenes · amoeba food",
    "A dense carpet of bacteria on agar. In labs Dictyostelium amoebae are usually fed on Klebsiella; where they have eaten them, clearer 'plaques' remain.",
    [("Bacterium", "rod ≈ 1–2 µm"), ("Density", "≈ 1 cell per µm²")]))
A["vagina"] = E(("Pochwa", "drogi rodne kobiety",
    "Pałeczki kwasu mlekowego (Lactobacillus) utrzymują tu kwaśne pH ok. 4, które chroni przed zakażeniami, ale szybko zabija plemniki. Nasienie buforuje kwas tylko przez kilka minut.",
    [("pH", "≈ 3,8–4,5"), ("Nabłonek", "wielowarstwowy płaski"), ("Plemniki", "większość ginie tutaj")]),
  ("Vagina", "female reproductive tract",
    "Lactic-acid bacteria (Lactobacillus) keep the pH here around 4, which protects against infection but quickly kills sperm. Semen buffers the acid only for a few minutes.",
    [("pH", "≈ 3.8–4.5"), ("Lining", "stratified squamous epithelium"), ("Sperm", "most die here")]))
A["cervix"] = E(("Szyjka macicy i śluz", "kanał szyjki",
    "W okresie owulacji śluz jest wodnisty i zasadowy, a nici mucyny układają się w kanaliki prowadzące plemniki. W bocznych kryptach plemniki mogą czekać godzinami.",
    [("Śluz", "mucyny, ~95–98% wody"), ("Krypty", "rezerwuar plemników"), ("Poza owulacją", "gęsty czop – bariera")]),
  ("Cervix and mucus", "cervical canal",
    "Around ovulation the mucus is watery and alkaline, and mucin strands form channels that guide sperm. In side crypts sperm can wait for hours.",
    [("Mucus", "mucins, ~95–98% water"), ("Crypts", "sperm reservoir"), ("Outside ovulation", "a thick plug – a barrier")]))
A["uterus"] = E(("Jama macicy", "macica",
    "Skurcze mięśniówki w okresie okołoowulacyjnym niosą plemniki falą ku jajowodom – szybciej, niż same by dopłynęły. Błona śluzowa to endometrium.",
    [("Długość macicy", "≈ 7–8 cm"), ("Transport", "perystaltyka w minutach"), ("Obrona", "neutrofile w świetle")]),
  ("Uterine cavity", "uterus",
    "Around ovulation, contractions of the muscle wall carry sperm in waves towards the tubes – faster than they could swim. The lining is the endometrium.",
    [("Uterus length", "≈ 7–8 cm"), ("Transport", "peristalsis within minutes"), ("Defence", "neutrophils in the lumen")]))
A["oviduct"] = E(("Jajowód", "trąbka Fallopiusza",
    "Kanał z nabłonkiem z rzęskami, które pędzą płyn ku macicy. W wąskiej cieśni plemniki czekają przyklejone do nabłonka, a w szerokiej bańce dochodzi do zapłodnienia.",
    [("Długość", "≈ 10–12 cm"), ("Odcinki", "cieśń, bańka, lejek"), ("Zapłodnienie", "w bańce")]),
  ("Oviduct", "Fallopian tube",
    "A canal lined with ciliated epithelium that drives fluid towards the uterus. In the narrow isthmus sperm wait bound to the lining; fertilisation happens in the wide ampulla.",
    [("Length", "≈ 10–12 cm"), ("Parts", "isthmus, ampulla, infundibulum"), ("Fertilisation", "in the ampulla")]))
A["cumulus"] = E(("Wzgórek jajonośny", "cumulus oophorus",
    "Tysiące komórek w galaretowatej macierzy kwasu hialuronowego otaczających komórkę jajową. Wydzielają progesteron, który przyspiesza plemniki (CatSper).",
    [("Macierz", "kwas hialuronowy"), ("Sygnał", "progesteron"), ("Przejście", "hialuronidaza plemnika")]),
  ("Cumulus", "cumulus oophorus",
    "Thousands of cells in a jelly-like hyaluronan matrix surrounding the egg. They release progesterone, which boosts sperm (CatSper).",
    [("Matrix", "hyaluronan"), ("Signal", "progesterone"), ("Passage", "sperm hyaluronidase")]))
A["blood-plasma"] = E(("Osocze krwi", "płynna część krwi",
    "Żółtawa ciecz, w której płyną krwinki. To ok. 55% objętości krwi: woda z białkami (albuminy, przeciwciała, fibrynogen), solami, cukrem i hormonami.",
    [("Udział", "≈ 55% krwi"), ("Woda", "≈ 92%"), ("Przeciwciała", "tu krążą IgM i IgG")]),
  ("Blood plasma", "liquid part of blood",
    "The yellowish fluid in which blood cells travel. About 55% of blood volume: water with proteins (albumin, antibodies, fibrinogen), salts, sugar and hormones.",
    [("Share", "≈ 55% of blood"), ("Water", "≈ 92%"), ("Antibodies", "IgM and IgG circulate here")]))
A["tsetse"] = E(("Mucha tse-tse", "Glossina · kłujka",
    "Afrykańska mucha żywiąca się krwią (obie płcie). Kłujką rozcina skórę i ssie krew z powstałej „kałuży”, przy okazji zabierając lub przekazując świdrowce.",
    [("Długość muchy", "≈ 6–14 mm"), ("Przenosi", "Trypanosoma brucei"), ("Rozród", "rodzi pojedyncze larwy")]),
  ("Tsetse fly", "Glossina · proboscis",
    "An African blood-feeding fly (both sexes). With its proboscis it cuts the skin and sucks blood from the resulting 'pool', picking up or passing on trypanosomes.",
    [("Fly length", "≈ 6–14 mm"), ("Transmits", "Trypanosoma brucei"), ("Breeding", "gives birth to single larvae")]))

# ------------------------------------------------------------------ kora mózgu zarodka myszy (tryb neuronu)
A["cortneuron"] = E(("Młody neuron piramidowy", "neuron wędrujący · Mus musculus",
    "Neuron kory w drodze na swoje miejsce. Najpierw jest wielobiegunowy: wolno błądzi i wysuwa oraz chowa cienkie neuryty. Potem przechodzi w dwubiegunowy – obejmuje włókno gleju promienistego, wysuwa ku oponie wypustkę wiodącą, a jądro skacze za nią. Z tyłu zostawia akson.",
    [("Ciało", "≈ 8–10 µm"), ("Faza wielobiegunowa", "średnio 4,4 µm/h (Tabata i Nakajima 2003)"), ("Lokomocja po glej", "średnio ~9–12 µm/h"), ("Droga z E15,5", "~3 doby do szczytu płytki (FlashTag – Yoshinaga i in. 2021)")]),
  ("Young pyramidal neuron", "migrating neuron · Mus musculus",
    "A cortical neuron on its way to its final position. It is multipolar at first: it wanders slowly and extends and retracts thin neurites. Then it becomes bipolar – it wraps around a radial glial fibre, extends a leading process towards the pia and its nucleus jumps after it. It leaves the axon behind.",
    [("Soma", "≈ 8–10 µm"), ("Multipolar phase", "4.4 µm/h on average (Tabata & Nakajima 2003)"), ("Locomotion on glia", "~9–12 µm/h on average"), ("Journey from E15.5", "~3 days to the top of the plate (FlashTag – Yoshinaga et al. 2021)")]))
A["ipc"] = E(("Progenitor pośredni (Tbr2⁺)", "komórka progenitorowa podstawna",
    "Córka komórki gleju promienistego, która odrywa się od powierzchni komory i w strefie podkomorowej zwykle dzieli się raz, symetrycznie – na dwa neurony. Z takich progenitorów pochodzi większość neuronów projekcyjnych kory.",
    [("Wielkość", "≈ 9–10 µm"), ("Znacznik", "czynnik transkrypcyjny Tbr2 (Eomes)"), ("Podział", "symetryczny, na dwa neurony (Noctor i in. 2004)"), ("Znaczenie", "większość neuronów wszystkich warstw (Kowalczyk i in. 2009)")]),
  ("Intermediate progenitor (Tbr2⁺)", "basal progenitor cell",
    "A daughter of a radial glial cell that detaches from the ventricular surface and, in the subventricular zone, usually divides once, symmetrically – into two neurons. Most cortical projection neurons come from such progenitors.",
    [("Size", "≈ 9–10 µm"), ("Marker", "transcription factor Tbr2 (Eomes)"), ("Division", "symmetric, into two neurons (Noctor et al. 2004)"), ("Importance", "most neurons of all layers (Kowalczyk et al. 2009)")]))
A["radial-glia"] = E(("Glej promienisty", "komórki macierzyste kory i rusztowanie wędrówki",
    "Ciało leży w strefie komorowej, a jedna długa wypustka sięga od komory aż do opony miękkiej. Te komórki są zarazem komórkami macierzystymi (dzielą się przy komorze) i szynami, po których neurony wędrują ku korze. Po urodzeniu przekształcają się w astrocyty.",
    [("Włókno", "od komory do opony – w E16 ~0,5 mm"), ("Cykl komórkowy", "18,4 h w E16 (Takahashi i in. 1995)"), ("Wędrówka po glej", "odkryta u małp (Rakic 1972)"), ("Los", "astrocyty (Schmechel i Rakic 1979)")]),
  ("Radial glia", "cortical stem cells and migration scaffold",
    "The cell body sits in the ventricular zone and a single long process reaches from the ventricle all the way to the pia. These cells are both stem cells (dividing at the ventricle) and rails along which neurons migrate to the cortex. After birth they turn into astrocytes.",
    [("Fibre", "from ventricle to pia – ~0.5 mm at E16"), ("Cell cycle", "18.4 h at E16 (Takahashi et al. 1995)"), ("Glia-guided migration", "discovered in monkeys (Rakic 1972)"), ("Fate", "astrocytes (Schmechel & Rakic 1979)")]))
A["cp-neuron"] = E(("Neuron płytki korowej", "młody neuron, który dotarł na miejsce",
    "Płytka korowa rośnie od środka na zewnątrz: najstarsze neurony (warstwy VI i V) leżą najgłębiej, a każdy nowy mija je i osiada tuż pod strefą brzeżną. Z płytki powstaną warstwy II–VI kory.",
    [("Ułożenie", "od środka na zewnątrz (Angevine i Sidman 1961)"), ("Kolumny", "neurony z jednego klonu leżą w kolumnie (Yu i in. 2009)"), ("Śmierć naturalna", "~12% neuronów pobudzających ginie w P2–P5 (Wong i in. 2018)")]),
  ("Cortical plate neuron", "a young neuron that has arrived",
    "The cortical plate grows inside-out: the oldest neurons (layers VI and V) lie deepest, and each new one passes them and settles just below the marginal zone. The plate will become cortical layers II–VI.",
    [("Arrangement", "inside-out (Angevine & Sidman 1961)"), ("Columns", "neurons of one clone lie in a column (Yu et al. 2009)"), ("Natural death", "~12% of excitatory neurons die at P2–P5 (Wong et al. 2018)")]))
A["subplate-neuron"] = E(("Neuron podpłytki", "jedne z najwcześniej urodzonych neuronów kory",
    "Tworzy przejściową warstwę pod płytką korową i przyjmuje pierwsze aksony ze wzgórza. Na komórkach wielobiegunowych tuż pod podpłytką tworzy przejściowe synapsy glutaminianergiczne – ich sygnał (receptory NMDA) przełącza je w wędrówkę po gleju.",
    [("Synapsy", "przejściowe, na komórkach wielobiegunowych (Ohtaka-Maruyama i in. 2018)"), ("Los", "wiele ginie po urodzeniu, część zostaje jako warstwa VIb")]),
  ("Subplate neuron", "among the earliest-born cortical neurons",
    "It forms a transient layer below the cortical plate and receives the first axons from the thalamus. On multipolar cells just below the subplate it forms transient glutamatergic synapses – their signal (NMDA receptors) switches them to glia-guided migration.",
    [("Synapses", "transient, on multipolar cells (Ohtaka-Maruyama et al. 2018)"), ("Fate", "many die after birth, some remain as layer VIb")]))
A["cajal-retzius"] = E(("Komórka Cajala–Retziusa", "neuron strefy brzeżnej",
    "Duży, poziomy neuron strefy brzeżnej z grubą wypustką biegnącą wzdłuż powierzchni kory. Wydziela relinę – sygnał, który każe wędrującym neuronom zakończyć drogę translokacją końcową i osiąść tuż pod strefą brzeżną.",
    [("Położenie", "strefa brzeżna – przyszła warstwa I"), ("Sygnał", "relina → Dab1 w neuronach (Sekine i in. 2011)"), ("Los", "w większości zanikają po urodzeniu")]),
  ("Cajal–Retzius cell", "marginal-zone neuron",
    "A large horizontal neuron of the marginal zone with a thick process running along the cortical surface. It secretes reelin – the signal that makes migrating neurons finish with terminal translocation and settle just below the marginal zone.",
    [("Location", "marginal zone – future layer I"), ("Signal", "reelin → Dab1 in neurons (Sekine et al. 2011)"), ("Fate", "mostly disappear after birth")]))
A["meninges"] = E(("Opony mózgowe", "opona miękka i pajęczynówka",
    "Błony okrywające mózg. Opona miękka przylega do kory: na jej błonie podstawnej kończą się stopki włókien gleju promienistego, a w niej biegną naczynia, z których w głąb kory wnikają naczynia promieniste. Między półkulami opony wypełniają szczelinę międzypółkulową.",
    [("Warstwy", "opona miękka, pajęczynówka, opona twarda"), ("Komórki", "płaskie fibroblasty opon"), ("Dla kory", "błona podstawna dla gleju i naczynia")]),
  ("Meninges", "pia mater and arachnoid",
    "The membranes covering the brain. The pia lies against the cortex: the endfeet of radial glial fibres end on its basement membrane, and its vessels send radial vessels deep into the cortex. Between the hemispheres the meninges fill the interhemispheric fissure.",
    [("Layers", "pia, arachnoid, dura"), ("Cells", "flat meningeal fibroblasts"), ("For the cortex", "basement membrane for glia, and vessels")]))
A["cortex-vessel"] = E(("Naczynie kory zarodka", "splot okołokomorowy i naczynia promieniste",
    "Rozwijająca się kora jest dobrze unaczyniona: na powierzchni leży splot opony miękkiej, nad strefą komorową – splot okołokomorowy, a łączą je naczynia promieniste. W skrawku splot widać jako odcinki naczyń z przerwami – sieć biegnie też w głąb.",
    [("Sploty", "opony miękkiej i okołokomorowy (Vasudevan i in. 2008)"), ("Znaczenie", "tlen i składniki odżywcze dla dzielących się progenitorów")]),
  ("Embryonic cortical vessel", "periventricular plexus and radial vessels",
    "The developing cortex is well vascularised: the pial plexus lies on the surface, the periventricular plexus above the ventricular zone, and radial vessels connect them. In a slice the plexus appears as vessel segments with gaps – the network also runs in depth.",
    [("Plexuses", "pial and periventricular (Vasudevan et al. 2008)"), ("Role", "oxygen and nutrients for dividing progenitors")]))
A["cortex-mz"] = E(("Strefa brzeżna", "przyszła warstwa I kory",
    "Najbardziej zewnętrzna, uboga w komórki warstwa kory. Leżą tu komórki Cajala–Retziusa i gęsta sieć włókien; tu kotwiczą wypustki wiodące neuronów przed translokacją końcową. Strefą brzeżną wędrują też stycznie interneurony.",
    [("Komórki", "Cajala–Retziusa, interneurony"), ("Sygnał", "relina"), ("Przyszłość", "warstwa I")]),
  ("Marginal zone", "future cortical layer I",
    "The outermost, cell-poor layer of the cortex. Cajal–Retzius cells and a dense fibre network lie here; leading processes of neurons anchor here before terminal translocation. Interneurons also migrate tangentially through the marginal zone.",
    [("Cells", "Cajal–Retzius, interneurons"), ("Signal", "reelin"), ("Future", "layer I")]))
A["cortex-cp"] = E(("Płytka korowa", "przyszła kora właściwa (warstwy II–VI)",
    "Gęsto upakowane młode neurony ułożone w promieniste kolumny. W najwyższej, niedojrzałej części (strefa PCZ, ~50 µm) neurony kończą wędrówkę translokacją końcową – dlatego każdy nowy neuron osiada nad starszymi.",
    [("Strefa PCZ", "≈ 50 µm (Sekine i in. 2011)"), ("Cała ściana kory w E16,5", "≈ 480 µm (Tonosaki i in. 2023)"), ("Ułożenie", "od środka na zewnątrz")]),
  ("Cortical plate", "future cortex proper (layers II–VI)",
    "Densely packed young neurons arranged in radial columns. In the uppermost, immature part (the PCZ, ~50 µm) neurons finish migrating by terminal translocation – which is why each new neuron settles above the older ones.",
    [("PCZ", "≈ 50 µm (Sekine et al. 2011)"), ("Whole cortical wall at E16.5", "≈ 480 µm (Tonosaki et al. 2023)"), ("Arrangement", "inside-out")]))
A["cortex-sp"] = E(("Podpłytka", "przejściowa warstwa pod płytką korową",
    "Cienka warstwa neuronów podpłytki i pęczków włókien. Pod nią komórki wielobiegunowe dostają synapsy od neuronów podpłytki i przechodzą w wędrówkę po gleju; w części kory neurony „czekają” tu dłużej przed wejściem do płytki (Yoshinaga i in. 2021).",
    [("Komórki", "neurony podpłytki"), ("Rola", "przełącznik wędrówki (Ohtaka-Maruyama i in. 2018)"), ("Po urodzeniu", "w dużej części zanika")]),
  ("Subplate", "transient layer below the cortical plate",
    "A thin layer of subplate neurons and fibre bundles. Below it multipolar cells receive synapses from subplate neurons and switch to glia-guided migration; in parts of the cortex neurons 'wait' here longer before entering the plate (Yoshinaga et al. 2021).",
    [("Cells", "subplate neurons"), ("Role", "migration switch (Ohtaka-Maruyama et al. 2018)"), ("After birth", "largely disappears")]))
A["cortex-iz"] = E(("Strefa pośrednia", "przyszła istota biała",
    "Pęczki aksonów biegnące stycznie – w dolnej części aksony pionierskie z TAG-1, wyżej aksony spoidłowe zmierzające ku linii środkowej. Między nimi wędrują neurony wielobiegunowe i dwubiegunowe, a w dolnej części płynie strumień interneuronów.",
    [("TAG-1", "kontakt z tymi aksonami tworzy akson neuronu (Namba i in. 2014)"), ("MAZ", "strefa gromadzenia komórek wielobiegunowych"), ("Przyszłość", "istota biała pod korą")]),
  ("Intermediate zone", "future white matter",
    "Tangential axon bundles – TAG-1 pioneer axons in the lower part, callosal axons heading for the midline higher up. Multipolar and bipolar neurons migrate between them, and a stream of interneurons flows in the lower part.",
    [("TAG-1", "contact with these axons makes a neuron's axon (Namba et al. 2014)"), ("MAZ", "multipolar cell accumulation zone"), ("Future", "white matter below the cortex")]))
A["cortex-svz"] = E(("Strefa podkomorowa", "strefa podziałów progenitorów pośrednich",
    "Warstwa nad strefą komorową, w której dzielą się progenitory pośrednie (Tbr2⁺) – każdy zwykle na dwa neurony. Biegnie przez nią splot naczyń okołokomorowych, a mikroglej pochłania tu część progenitorów.",
    [("Podziały", "symetryczne, na dwa neurony (Noctor i in. 2004)"), ("Mikroglej", "fagocytuje progenitory pod koniec neurogenezy (Cunningham i in. 2013)")]),
  ("Subventricular zone", "where intermediate progenitors divide",
    "A layer above the ventricular zone where intermediate progenitors (Tbr2⁺) divide – each usually into two neurons. The periventricular vascular plexus runs through it, and microglia engulf some of the progenitors here.",
    [("Divisions", "symmetric, into two neurons (Noctor et al. 2004)"), ("Microglia", "phagocytose progenitors near the end of neurogenesis (Cunningham et al. 2013)")]))
A["cortex-vz"] = E(("Strefa komorowa", "nabłonek wielorzędowy gleju promienistego",
    "Warstwa komórek gleju promienistego wyścielająca komorę. Ich jądra wędrują w rytmie cyklu komórkowego: faza S przy podstawnym brzegu strefy, mitoza zawsze przy powierzchni komory. Stąd pochodzą wszystkie neurony pobudzające kory.",
    [("Cykl komórkowy", "8,1 h w E11 → 18,4 h w E16 (Takahashi i in. 1995)"), ("Wędrówka jądra", "śródfazowa (INM)"), ("Mitozy", "przy powierzchni komory")]),
  ("Ventricular zone", "pseudostratified radial glial epithelium",
    "The layer of radial glial cells lining the ventricle. Their nuclei move with the cell cycle: S phase at the basal edge of the zone, mitosis always at the ventricular surface. All excitatory cortical neurons come from here.",
    [("Cell cycle", "8.1 h at E11 → 18.4 h at E16 (Takahashi et al. 1995)"), ("Nuclear movement", "interkinetic (INM)"), ("Mitoses", "at the ventricular surface")]))
A["ventricle"] = E(("Komora boczna", "płyn mózgowo-rdzeniowy",
    "Jama wypełniona płynem mózgowo-rdzeniowym. Przy jej powierzchni dzielą się komórki gleju promienistego – to początek każdej linii neuronów kory. Do komory wstrzykuje się barwnik FlashTag albo plazmid przy elektroporacji in utero.",
    [("Wypełnia", "płyn mózgowo-rdzeniowy"), ("FlashTag", "znakuje komórki dzielące się przy komorze (Govindan i in. 2018)")]),
  ("Lateral ventricle", "cerebrospinal fluid",
    "A cavity filled with cerebrospinal fluid. Radial glial cells divide at its surface – the start of every cortical neuron lineage. FlashTag dye or a plasmid for in utero electroporation is injected into the ventricle.",
    [("Contains", "cerebrospinal fluid"), ("FlashTag", "labels cells dividing at the ventricle (Govindan et al. 2018)")]))
A["lge"] = E(("Wyniosłość zwojowa boczna", "dno komory – zawiązek prążkowia",
    "Wypukłość dna komory bocznej. Razem z wyniosłością przyśrodkową i ogoniastą wytwarza neurony hamujące (GABA); interneurony kory pochodzą głównie z wyniosłości przyśrodkowej i ogoniastej i wędrują do kory stycznie.",
    [("Przyszłość", "prążkowie"), ("Neurony", "hamujące (GABA)")]),
  ("Lateral ganglionic eminence", "floor of the ventricle – striatal anlage",
    "A bulge on the floor of the lateral ventricle. Together with the medial and caudal eminences it produces inhibitory (GABA) neurons; cortical interneurons come mainly from the medial and caudal eminences and migrate tangentially into the cortex.",
    [("Future", "striatum"), ("Neurons", "inhibitory (GABA)")]))
A["indusium-griseum"] = E(("Szare pokrywające", "indusium griseum",
    "Cienka warstwa istoty szarej nad ciałem modzelowatym. Jej glej wydziela Slit2 i razem z klinem glejowym zamyka aksonom drogę poza ciałem modzelowatym (Shu i Richards 2001).",
    [("Sygnał", "Slit2 (odpychanie, receptory Robo)"), ("Położenie", "nad ciałem modzelowatym")]),
  ("Indusium griseum", "gray matter above the corpus callosum",
    "A thin layer of gray matter above the corpus callosum. Its glia secrete Slit2 and, together with the glial wedge, keep axons from straying outside the corpus callosum (Shu & Richards 2001).",
    [("Signal", "Slit2 (repulsion, Robo receptors)"), ("Location", "above the corpus callosum")]))
A["glial-wedge"] = E(("Klin glejowy", "glial wedge",
    "Klinowata grupa komórek gleju promienistego pod ciałem modzelowatym, przy grzbietowo-przyśrodkowym rogu komory. Wydziela Slit2 – aksony z receptorami Robo omijają ją i skręcają ku linii środkowej (Shu i Richards 2001).",
    [("Sygnał", "Slit2 → Robo1/2 na aksonach"), ("Skutek", "stożek wzrostu zapada się i cofa")]),
  ("Glial wedge", "midline guidepost glia",
    "A wedge-shaped group of radial glial cells below the corpus callosum, at the dorsomedial corner of the ventricle. It secretes Slit2 – axons with Robo receptors avoid it and turn towards the midline (Shu & Richards 2001).",
    [("Signal", "Slit2 → Robo1/2 on axons"), ("Effect", "the growth cone collapses and retracts")]))
A["corpus-callosum"] = E(("Ciało modzelowate", "największe spoidło mózgu",
    "Aksony neuronów kory (głównie warstw II/III i V) przechodzące do drugiej półkuli. Pierwsze przechodzą aksony pionierskie z kory obręczy (E15,5), a aksony kory nowej płyną wzdłuż nich. Drogę wyznaczają glej linii środkowej (Slit2) i netryna-1.",
    [("Pionierzy", "kora obręczy (Koester i O'Leary 1994; Rash i Richards 2001)"), ("Prowadzenie", "Slit2 z gleju, netryna-1/DCC (Fothergill i in. 2014)"), ("Aksony II/III u myszy", "przechodzą ok. P3 z kory czuciowej (Wang i in. 2007)")]),
  ("Corpus callosum", "the brain's largest commissure",
    "Axons of cortical neurons (mainly layers II/III and V) crossing to the other hemisphere. Pioneer axons from the cingulate cortex cross first (E15.5) and neocortical axons follow them. Midline glia (Slit2) and netrin-1 set the path.",
    [("Pioneers", "cingulate cortex (Koester & O'Leary 1994; Rash & Richards 2001)"), ("Guidance", "Slit2 from glia, netrin-1/DCC (Fothergill et al. 2014)"), ("Layer II/III axons in mice", "cross around P3 from somatosensory cortex (Wang et al. 2007)")]))
A["septum"] = E(("Przegroda", "okolica pod ciałem modzelowatym",
    "Obszar między komorami bocznymi, poniżej ciała modzelowatego – w zarodku gęsta tkanka nerwowa. Aksony spoidłowe przechodzą nad nią, w ciele modzelowatym.",
    [("Położenie", "pod ciałem modzelowatym, między komorami"), ("Dla aksonów", "nieprzekraczalna – droga biegnie wyżej")]),
  ("Septum", "region below the corpus callosum",
    "The region between the lateral ventricles, below the corpus callosum – dense neural tissue in the embryo. Callosal axons pass above it, through the corpus callosum.",
    [("Location", "below the corpus callosum, between the ventricles"), ("For axons", "impassable – the path runs higher")]))
A["interneuron"] = E(("Interneuron (GABA)", "neuron hamujący w wędrówce stycznej",
    "Neuron hamujący urodzony w wyniosłości zwojowej przyśrodkowej lub ogoniastej. Do kory idzie stycznie, strumieniami w strefie brzeżnej i dolnej strefie pośredniej. Ma rozgałęzioną wypustkę wiodącą – jądro wchodzi do jednej gałęzi, a pozostałe się cofają.",
    [("Prędkość w żywym zarodku", "12 ± 6 µm/h (Yanagida i in. 2012)"), ("Pochodzenie", "wyniosłości zwojowe MGE i CGE"), ("Śmierć naturalna", "~30% ginie w P5–P10 (Wong i in. 2018)")]),
  ("Interneuron (GABA)", "tangentially migrating inhibitory neuron",
    "An inhibitory neuron born in the medial or caudal ganglionic eminence. It reaches the cortex tangentially, in streams in the marginal zone and the lower intermediate zone. Its leading process is branched – the nucleus enters one branch and the others retract.",
    [("Speed in living embryos", "12 ± 6 µm/h (Yanagida et al. 2012)"), ("Origin", "MGE and CGE"), ("Natural death", "~30% die at P5–P10 (Wong et al. 2018)")]))
A["microglia"] = E(("Mikroglej", "makrofagi mózgu",
    "Komórki odpornościowe mózgu, pochodzące z woreczka żółtkowego. W korze zarodka są rozproszone w strefie komorowej, podkomorowej i pośredniej. Pochłaniają komórki apoptotyczne i część progenitorów, a ich rozmieszczenie wpływa na rozwój połączeń.",
    [("Rozmieszczenie", "VZ, SVZ i IZ (Hattori i Miyata 2018)"), ("Fagocytoza", "komórki apoptotyczne i progenitory (Cunningham i in. 2013)"), ("Połączenia", "wpływ na wzrost aksonów (Squarzoni i in. 2014)")]),
  ("Microglia", "the brain's macrophages",
    "The brain's immune cells, derived from the yolk sac. In the embryonic cortex they are scattered in the ventricular, subventricular and intermediate zones. They engulf apoptotic cells and some progenitors, and their distribution affects wiring.",
    [("Distribution", "VZ, SVZ and IZ (Hattori & Miyata 2018)"), ("Phagocytosis", "apoptotic cells and progenitors (Cunningham et al. 2013)"), ("Wiring", "influence axon outgrowth (Squarzoni et al. 2014)")]))
A["growthcone"] = E(("Stożek wzrostu aksonu", "ruchoma końcówka rosnącego aksonu",
    "Blaszka (lamellipodium) i palczaste filopodia badają otoczenie: stożek skręca ku sygnałom przyciągającym i zapada się przed odpychającymi (np. Slit2). W drodze przez istotę białą jest duży i ruchliwy, w korze docelowej – zwarty i wolniejszy.",
    [("Prędkość", "~37 µm/h przed linią środkową, ~55 µm/h za nią (Hutchins i in. 2011)"), ("W korze docelowej", "o połowę wolniej niż w drodze (Halloran i Kalil 1994)"), ("Zapadnięcie", "Slit2 → Robo")]),
  ("Axon growth cone", "the motile tip of a growing axon",
    "A veil (lamellipodium) and finger-like filopodia probe the surroundings: the cone turns towards attractive cues and collapses in front of repulsive ones (e.g. Slit2). In the white-matter tract it is large and motile; in the target cortex compact and slower.",
    [("Speed", "~37 µm/h before the midline, ~55 µm/h after (Hutchins et al. 2011)"), ("In target cortex", "half the tract speed (Halloran & Kalil 1994)"), ("Collapse", "Slit2 → Robo")]))
A["pyknotic-cell"] = E(("Neuron w apoptozie", "piknoza · naturalna śmierć neuronów",
    "Komórka, która uruchomiła program apoptozy: obkurcza się, jej chromatyna zbija się w ciemne grudki i rozpada. W korze myszy między P2 a P5 ginie tak ~12% neuronów pobudzających, a szczyt apoptozy przypada ok. P5–P6. Aktywność elektryczna chroni neurony.",
    [("Skala", "~12% neuronów pobudzających (Wong i in. 2018)"), ("Ochrona", "aktywność elektryczna (Blanquie i in. 2017)"), ("Sprzątanie", "mikroglej, w ciągu godzin")]),
  ("Apoptotic neuron", "pyknosis · natural neuronal death",
    "A cell that has switched on the apoptosis programme: it shrinks, its chromatin condenses into dark clumps and falls apart. In the mouse cortex ~12% of excitatory neurons die this way between P2 and P5, with apoptosis peaking around P5–P6. Electrical activity protects neurons.",
    [("Scale", "~12% of excitatory neurons (Wong et al. 2018)"), ("Protection", "electrical activity (Blanquie et al. 2017)"), ("Clearance", "microglia, within hours")]))

# zdjęcia: atlas/photos.json → {klucz: {"photo": plik, "credit": "autor · licencja · Wikimedia Commons", "url": strona pliku}}
ph = {}
pp = os.path.join(HERE, "photos.json")
if os.path.exists(pp): ph = json.load(open(pp, encoding="utf-8"))
for k, v in A.items():
    if k in ph: v.update(ph[k])
json.dump(A, open(os.path.join(HERE, "atlas.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(len(A), "entries,", sum(1 for k in A if "photo" in A[k]), "with photo")
