// Tło pola widzenia mikroskopu.
// 1) Optyka (układ ekranu): gradient oświetlenia, nierównomierność i kurz na optyce (pierścienie – kurz poza ogniskiem).
// 2) Głębia preparatu (układ świata – przesuwa się razem ze szkiełkiem): materia leżąca nad i pod płaszczyzną ostrości,
//    widoczna tylko jako rozmyte plamy – jak na prawdziwych zdjęciach. Obiekty są losowe (siatka z przesunięciami,
//    hash z ziarna świata), a ich rodzaj i gęstość zależą od strefy: gęstości są w małej teksturze zone0/zone1
//    próbkowanej wokół kamery (plamy 1, plamy 2, pręty/nici, drobiny; odcień i zmętnienie), więc na granicy stref
//    tło zmienia się płynnie. Rozmiary w µm – przy oddalaniu obiekty maleją jak prawdziwe.
//    Rodziny kształtów: plama (nieregularny zarys, opcjonalnie ciemne jądro albo jaśniejszy środek, pierścień
//    rozogniskowania), pręt (kapsuła, lekko wygięta: nitka glonu z taśmą chloroplastu i poprzecznymi ścianami,
//    pęczek kolagenu, pałeczka, pasmo śluzu) i drobina (krążek z jasnym pierścieniem – „bokeh” drobnego ziarna).
shader_type canvas_item;

uniform vec4 bg_in : source_color = vec4(0.79, 0.8, 0.73, 1.0);
uniform vec4 bg_mid : source_color = vec4(0.69, 0.7, 0.64, 1.0);
uniform vec4 bg_out : source_color = vec4(0.42, 0.44, 0.38, 1.0);
uniform float uneven = 0.04;
uniform float dust = 1.0;
uniform float seed = 1.0;
uniform vec2 resolution = vec2(1600.0, 900.0);

uniform int kind = 0;              // 0 – bez głębi preparatu
uniform vec2 cam = vec2(0.0);      // środek kadru (µm)
uniform float px_um = 1.0;         // pikseli obrazu na µm
uniform float wseed = 0.0;         // ziarno świata (inny preparat – inne tło)
uniform sampler2D zone0 : filter_linear, repeat_disable;
uniform sampler2D zone1 : filter_linear, repeat_disable;
uniform vec2 zone_o = vec2(0.0);   // µm – róg siatki stref
uniform vec2 zone_span = vec2(1.0); // µm – boki siatki stref (x, y)
// plamy 1 i 2: col; p = (rozmiar µm, odstęp ×rozmiar, rozmycie ×promień, nieregularność);
//              q = (jądro: >0 ciemne, <0 jaśniejszy środek; pierścień; wydłużenie; ciemny brzeg)
uniform vec4 b1_col : source_color; uniform vec4 b1_p; uniform vec4 b1_q;
uniform vec4 b2_col : source_color; uniform vec4 b2_p; uniform vec4 b2_q;
// pręty: p = (długość µm, połowa szerokości µm, rozmycie ×szer., wygięcie); q = (prążki /µm, siła prążków, ściany co µm, odstęp ×dł.)
uniform vec4 r_col : source_color; uniform vec4 r_p; uniform vec4 r_q;
// część prętów to cienkie igły (np. okrzemki Synedra wśród nici skrętnicy): (udział, ×długość, ×szerokość, 0)
uniform vec4 r_n = vec4(0.0, 0.3, 0.3, 0.0);
uniform vec4 r_ncol : source_color = vec4(0.45, 0.4, 0.28, 0.4);
// ułożenie prętów: 0 – dowolne; 1 – pionowe (promieniste włókna gleju w korze), a w pasie y rod_band – poziome (pęczki aksonów)
uniform int rod_mode = 0;
uniform vec2 rod_band = vec2(0.0);
// drobiny: p = (promień µm, odstęp ×promień, rozmycie ×promień, jasny pierścień)
uniform vec4 d_col : source_color; uniform vec4 d_p;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7)) + seed * 17.13) * 43758.5453); }
float vnoise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}
// hash całkowitoliczbowy (stabilny daleko od początku układu – świat jest nieskończony)
vec3 h3(vec2 c, float salt) {
	uvec3 q = uvec3(ivec3(ivec2(c) + ivec2(1 << 20), int(salt * 97.0 + wseed)));
	q = q * uvec3(1597334673u, 3812015801u, 2798796415u);
	uint n = (q.x ^ q.y ^ q.z) * 1597334673u;
	uvec3 r = uvec3(n, n * 16807u, n * 48271u);
	r ^= r >> 16u; r *= 2246822519u; r ^= r >> 13u;
	return vec3(r) * (1.0 / 4294967295.0);
}
vec4 zoneA(vec2 p) { return texture(zone0, (p - zone_o) / zone_span); }

// nakładanie warstwy (kolor niepremnożony) na tło
vec3 over(vec3 dst, vec3 c, float a) { return mix(dst, c, clamp(a, 0.0, 1.0)); }

// skupiska: materia w preparacie nie leży równo – są gęstsze płaty i puste miejsca (szum o dużej skali)
float clump(vec2 c, float cell) { return smoothstep(0.18, 0.72, vnoise(c * cell / (cell * 5.3) + wseed * 0.37) * 0.7 + vnoise(c * cell / (cell * 1.9) - wseed) * 0.3) * 1.7; }

vec3 blobs(vec3 col, vec2 p, vec4 bc, vec4 P, vec4 Q, vec4 ch, float salt) {
	float size = P.x, cell = size * P.y;
	vec2 g = floor(p / cell);
	for (int j = -1; j <= 1; j++)
	for (int i = -1; i <= 1; i++) {
		vec2 c = g + vec2(float(i), float(j));
		vec3 h = h3(c, salt);
		vec2 ctr = (c + 0.15 + 0.7 * h.xy) * cell;
		float dens = dot(zoneA(ctr), ch) * clump(c, 1.0);
		if (h.z >= dens) continue;
		vec3 k = h3(c, salt + 1.0);
		float R = size * (0.4 + 1.1 * k.x * k.x);
		if (length(p - ctr) > R * 2.6) continue;
		float rot = k.y * 6.2832, cs = cos(rot), sn = sin(rot);
		float bl = R * P.z * (0.55 + 0.9 * k.z) + 0.6 / px_um;
		// grudka = kilka zlanych płatów o nieregularnym zarysie (a nie gwiazdka)
		float aU = 1.0, rr = 9.0;
		for (int l = 0; l < 3; l++) {
			vec3 m = h3(c + vec2(float(l) * 17.0, 3.0), salt + 2.0);
			float fl = float(l);
			vec2 off = l == 0 ? vec2(0.0) : (m.xy - 0.5) * R * (0.9 + 0.5 * P.w);
			float rl = R * (l == 0 ? 0.8 + 0.25 * m.z : 0.4 + 0.35 * m.z) * (1.0 - 0.35 * P.w * fl * 0.5);
			vec2 d = p - ctr - off; d = vec2(d.x * cs + d.y * sn, -d.x * sn + d.y * cs);
			d.y *= 1.0 + Q.z * k.z;
			float r = length(d), an = atan(d.y, d.x);
			float rn = rl * (1.0 + P.w * 0.35 * (0.6 * sin(2.0 * an + m.x * 40.0) + 0.4 * sin(3.0 * an + m.y * 40.0)));
			float al = 1.0 - smoothstep(rn - bl, rn + bl, r);
			aU *= 1.0 - al;
			if (l == 0) rr = r / max(rn, 1e-4);
		}
		float a = 1.0 - aU;
		if (a < 0.002) continue;
		float depth = 0.55 + 0.45 * fract(k.x * 7.31 + h.z * 3.1);   // bliżej/dalej od płaszczyzny ostrości
		vec3 c0 = bc.rgb;
		if (Q.x > 0.0) c0 *= 1.0 - Q.x * (1.0 - smoothstep(0.1, 0.45 + 0.3 * P.z, rr));        // ciemne jądro / zbita masa
		else if (Q.x < 0.0) c0 = mix(c0, vec3(1.0), -Q.x * (1.0 - smoothstep(0.0, 0.6, rr)));  // jaśniejszy środek (erytrocyt)
		c0 *= 1.0 - Q.w * smoothstep(0.5, 1.0, rr);
		col = over(col, c0, a * bc.a * depth);
		// pierścień rozogniskowania: jasna obwódka rozmytego obiektu
		col += Q.y * 0.1 * depth * a * (1.0 - a) * 4.0 * smoothstep(0.4, 0.9, rr);
	}
	return col;
}

vec3 rods(vec3 col, vec2 p, vec4 rc, vec4 P, vec4 Q, float salt) {
	float L = P.x, cell = L * Q.w;
	vec2 g = floor(p / cell);
	for (int j = -1; j <= 1; j++)
	for (int i = -1; i <= 1; i++) {
		vec2 c = g + vec2(float(i), float(j));
		vec3 h = h3(c, salt);
		vec2 ctr = (c + 0.2 + 0.6 * h.xy) * cell;
		float dens = zoneA(ctr).b * clump(c, 1.0);
		if (h.z >= dens) continue;
		vec3 k = h3(c, salt + 1.0);
		bool needle = fract(h.z * 13.7 + k.z * 3.3) < r_n.x;
		float hl = 0.5 * L * (0.55 + 0.9 * k.x) * (needle ? r_n.y : 1.0), W = P.y * (0.7 + 0.6 * k.y) * (needle ? r_n.z : 1.0);
		float rot = k.z * 3.1416 + h.x;
		if (rod_mode == 1) rot = ((ctr.y > rod_band.x && ctr.y < rod_band.y) ? 0.0 : 1.5708) + (k.z - 0.5) * 0.22;
		float cs = cos(rot), sn = sin(rot);
		vec2 d = p - ctr; float u = d.x * cs + d.y * sn, v = -d.x * sn + d.y * cs;
		if (abs(u) > hl + W * 4.0) continue;
		float t = u / hl;
		v -= P.w * hl * (t * t - 0.33) * (k.x - 0.5) * 2.0;           // wygięcie
		float uu = clamp(u, -hl, hl), dist = length(vec2(u - uu, v));
		float bl = W * P.z * (0.5 + k.y) + 0.6 / px_um;
		float a = 1.0 - smoothstep(W - bl, W + bl, dist);
		if (a <= 0.001) continue;
		float depth = 0.55 + 0.45 * fract(k.y * 5.17 + h.z * 2.3);
		vec3 c0 = needle ? r_ncol.rgb : rc.rgb;
		float ra = needle ? r_ncol.a : rc.a;
		// taśma chloroplastu (skrętnica) / prążki kolagenu – rozmywają się razem z nitką
		float band = Q.y * (0.5 + 0.5 * sin(u * Q.x * 6.2832 + v / max(W, 1e-4) * 2.2 + h.x * 20.0));
		band *= (needle ? 0.0 : 1.0) * (1.0 - smoothstep(0.0, 1.0, bl / max(W, 1e-4) - 0.6));
		c0 *= 1.0 - band;
		// poprzeczne ściany komórek w nitce
		if (Q.z > 0.0 && !needle) { float sp = abs(fract(u / Q.z + h.y) - 0.5) * Q.z; c0 = mix(c0, vec3(1.0), 0.35 * (1.0 - smoothstep(0.0, W * 0.25 + bl * 0.3, sp))); }
		col = over(col, c0, a * ra * depth);
	}
	return col;
}

vec3 dots(vec3 col, vec2 p, vec4 dc, vec4 P, float salt) {
	float cell = P.x * P.y;
	vec2 g = floor(p / cell);
	for (int j = -1; j <= 1; j++)
	for (int i = -1; i <= 1; i++) {
		vec2 c = g + vec2(float(i), float(j));
		vec3 h = h3(c, salt);
		vec2 ctr = (c + 0.1 + 0.8 * h.xy) * cell;
		float dens = zoneA(ctr).a * clump(c, 1.0);
		if (h.z >= dens) continue;
		vec3 k = h3(c, salt + 1.0);
		float R = P.x * (0.25 + 1.9 * k.x * k.x * k.x), r = length(p - ctr);
		float bl = R * P.z * (0.4 + 1.2 * k.y) + 0.7 / px_um;
		if (r > R + bl * 2.0) continue;
		float a = 1.0 - smoothstep(R - bl, R + bl, r);
		bool bright = k.z < 0.35;
		vec3 c0 = bright ? mix(dc.rgb, vec3(1.0), 0.75) : dc.rgb;
		col = over(col, c0, a * dc.a * (0.5 + 0.5 * k.y));
		col += P.w * 0.1 * exp(-pow((r - R) / (bl * 0.6 + 1e-4), 2.0)) * (bright ? 0.4 : 1.0);
	}
	return col;
}

void fragment() {
	vec2 px = UV * resolution;
	vec2 c = resolution * vec2(0.47, 0.45);
	float R = length(resolution) * 0.5;
	float r = length(px - c) / R;
	vec3 col = r < 0.55 ? mix(bg_in.rgb, bg_mid.rgb, r / 0.55) : mix(bg_mid.rgb, bg_out.rgb, clamp((r - 0.55) / 0.45, 0.0, 1.0));
	// nierównomierne oświetlenie
	float n = vnoise(px / R * 2.2) - 0.5;
	col += n * uneven * 2.0;
	if (kind > 0) {
		// punkt preparatu (µm) pod pikselem
		vec2 p = cam + (px - resolution * 0.5) / px_um;
		vec4 z = zoneA(p);
		vec4 t = texture(zone1, (p - zone_o) / zone_span);
		// odcień strefy (0,5 = obojętny) i zmętnienie wody / tkanki (plamista mgiełka w dużej skali)
		vec3 tint = t.rgb * 2.0;
		col *= mix(vec3(1.0), tint, 0.6);
		float hz = vnoise(p / (b1_p.x * 9.0) + wseed) * 0.65 + vnoise(p / (b1_p.x * 3.1) - wseed) * 0.35;
		col = mix(col, col * tint * 0.82, t.a * smoothstep(0.25, 0.85, hz));
		// warstwy od najdalszej: duże plamy, nici/pręty, mniejsze plamy, drobiny
		if (z.r > 0.002) col = blobs(col, p, b1_col, b1_p, b1_q, vec4(1.0, 0.0, 0.0, 0.0), 11.0);
		if (z.b > 0.002) col = rods(col, p, r_col, r_p, r_q, 23.0);
		if (z.g > 0.002) col = blobs(col, p, b2_col, b2_p, b2_q, vec4(0.0, 1.0, 0.0, 0.0), 37.0);
		if (z.a > 0.002) col = dots(col, p, d_col, d_p, 53.0);
	}
	// kurz na optyce (poza ogniskiem → pierścienie)
	if (dust > 0.5) {
		for (int i = 0; i < 16; i++) {
			vec2 p = vec2(hash(vec2(float(i), 1.0)), hash(vec2(float(i), 2.0))) * resolution;
			float rr = mix(3.0, 14.0, hash(vec2(float(i), 3.0))) * resolution.x / 900.0;
			float d = length(px - p);
			float ring = exp(-pow((d - rr) / (rr * 0.22 + 1.5), 2.0));
			float core = exp(-pow(d / (rr * 0.55), 2.0));
			col -= ring * mix(0.03, 0.07, hash(vec2(float(i), 4.0)));
			col += core * mix(0.02, 0.05, hash(vec2(float(i), 5.0)));
		}
	}
	COLOR = vec4(col, 1.0);
}
