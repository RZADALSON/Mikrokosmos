// Optyka mikroskopu jako post-process obrazu świata.
// 5 = kontrast fazowy: jasne halo wokół ciemniejszych obiektów i ciemny rąbek po ich wewnętrznej stronie
//     (światło rozproszone przechodzi przez pierścień fazowy – artefakt „halo” i „shade-off”; Zernike 1942,
//      Otaki 2000). Szerokość halo jest cechą optyki (pierścienia i obiektywu), więc liczona jest w pikselach obrazu.
// 1 = ciemne pole: do obiektywu trafia tylko światło rozproszone – świecą krawędzie, ziarnistości i drobiny, tło jest czarne.
// 2 = jasne pole bez kontrastu: niebarwione, przezroczyste komórki są blade i ledwo widoczne.
// 3 = TEM w skali szarości, 4 = barwienie negatywne.
// 6 = kontrast interferencyjny (DIC, Nomarski; w skrawkach – IR-DIC): obraz różnicy dróg optycznych dwóch wiązek przesuniętych
//     o ułamek mikrometra wzdłuż kierunku ścinania – krawędzie wyglądają jak oświetlone z jednej strony (pozorny relief),
//     tło jest szare, a barwa słaba. Nie ma halo jak w kontraście fazowym, dlatego DIC nadaje się do grubych skrawków.
// 7 = fluorescencja (GFP): świecą tylko wyznakowane komórki na czarnym tle; światło rozproszone w skrawku daje poświatę.
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform int mode = 0;
uniform float halo_px = 6.0;
uniform float halo_k = 0.85;
uniform float dic_px = 1.6;
uniform float dic_k = 2.1;

float lum(vec3 c) { return dot(c, vec3(0.299, 0.587, 0.114)); }

// średnia z dysku o promieniu r (px): środek + dwa pierścienie po 12 próbek
vec3 disk(vec2 uv, vec2 px, float r) {
	vec3 acc = texture(screen_tex, uv).rgb;
	for (int i = 0; i < 12; i++) {
		float a = float(i) * 0.5235988;
		vec2 d = vec2(cos(a), sin(a)) * px;
		acc += texture(screen_tex, uv + d * r).rgb;
		acc += texture(screen_tex, uv + vec2(d.y, -d.x) * r * 0.5).rgb;
	}
	return acc / 25.0;
}

void fragment() {
	vec2 uv = SCREEN_UV, px = SCREEN_PIXEL_SIZE;
	vec3 c = texture(screen_tex, uv).rgb;
	if (mode == 5) {
		// lekkie zmiękczenie krawędzi (granica dyfrakcyjna obiektywu) + halo fazowe
		vec3 s = (texture(screen_tex, uv + vec2(px.x, 0.0)).rgb + texture(screen_tex, uv - vec2(px.x, 0.0)).rgb
		        + texture(screen_tex, uv + vec2(0.0, px.y)).rgb + texture(screen_tex, uv - vec2(0.0, px.y)).rgb) * 0.25;
		c = mix(c, s, 0.35);
		vec3 b = disk(uv, px, halo_px);
		vec3 d = c - b;
		// halo jest białawe (nie ma barwy obiektu), więc wzmacniamy głównie jasność
		float dl = lum(d);
		c += vec3(dl) * halo_k + (d - vec3(dl)) * 0.25;
	} else if (mode == 1) {
		// ciemne pole: jasność ∝ lokalnym różnicom (gradient współczynnika załamania rozprasza światło)
		vec3 b1 = disk(uv, px, 2.5);
		vec3 b2 = disk(uv, px, 9.0);
		float e = abs(lum(c) - lum(b1)) * 3.4 + abs(lum(b1) - lum(b2)) * 1.6;
		// gęste obiekty (ciemne w świetle przechodzącym) też trochę rozpraszają
		e += max(0.0, lum(b2) - lum(c)) * 0.35;
		// lampa halogenowa bez filtra dziennego: rozproszone światło ciepłe, złotawe (jak na zdjęciach w ciemnym polu)
		vec3 tint = vec3(1.0, 0.93, 0.78) + (c - vec3(lum(c))) * 1.6;
		c = tint * e;
		c = pow(clamp(c, 0.0, 1.0), vec3(0.85));
	} else if (mode == 2) {
		vec3 bl = disk(uv, px, 10.0);
		c = bl + (c - bl) * 0.42;
		c = c * 1.08 + 0.03;
		// jasne pole z halogenem i niebieskim filtrem dziennym: typowy dla zdjęć lekko fioletowo-lawendowy odcień tła
		c *= vec3(0.95, 0.93, 1.04);
	} else if (mode == 6) {
		vec3 s = (texture(screen_tex, uv + vec2(px.x, 0.0)).rgb + texture(screen_tex, uv - vec2(px.x, 0.0)).rgb
		        + texture(screen_tex, uv + vec2(0.0, px.y)).rgb + texture(screen_tex, uv - vec2(0.0, px.y)).rgb) * 0.25;
		c = mix(c, s, 0.3);
		vec2 sh = vec2(0.7071, -0.7071) * px * dic_px;
		float a = lum(texture(screen_tex, uv + sh).rgb), b = lum(texture(screen_tex, uv - sh).rgb);
		float a2 = lum(texture(screen_tex, uv + sh * 2.5).rgb), b2 = lum(texture(screen_tex, uv - sh * 2.5).rgb);
		float rel = (a - b) + 0.35 * (a2 - b2);
		// IR-DIC: kamera czuła na podczerwień – obraz jednobarwny
		c = vec3(lum(c) * 0.62 + 0.27 + rel * dic_k);
	} else if (mode == 7) {
		vec3 g1 = disk(uv, px, 3.0), g2 = disk(uv, px, 10.0);
		c = c + g1 * 0.45 + g2 * 0.3;
		c = pow(clamp(c, 0.0, 1.0), vec3(0.92));
	} else if (mode == 3) {
		float g = lum(c);
		c = vec3((g - 0.5) * 1.2 + 0.5);
	} else if (mode == 4) {
		float g = 1.0 - lum(c);
		c = vec3((g - 0.5) * 1.3 + 0.5);
	}
	COLOR = vec4(clamp(c, 0.0, 1.0), 1.0);
}
