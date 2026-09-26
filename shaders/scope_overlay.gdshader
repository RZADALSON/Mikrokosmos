// Winieta pola widzenia, aberracja chromatyczna na brzegu i szum sensora (animowany)
shader_type canvas_item;

uniform float vig = 0.62;
uniform float vig_in = 0.5;
uniform float grain = 0.07;
uniform vec2 resolution = vec2(1600.0, 900.0);

// całkowitoliczbowy hash (bez wzorów moiré)
float hash(vec2 p) {
	uvec2 q = uvec2(ivec2(p) + ivec2(100000)) * uvec2(1597334673u, 3812015801u);
	uint n = (q.x ^ q.y) * 1597334673u;
	n ^= n >> 16u; n *= 2246822519u; n ^= n >> 13u;
	return float(n) * (1.0 / 4294967295.0);
}

void fragment() {
	vec2 px = UV * resolution;
	float R = length(resolution) * 0.5;
	float r = length(px - resolution * 0.5) / R;
	float v = smoothstep(vig_in, 1.02, r) * vig;
	float chroma = smoothstep(0.8, 1.0, r) * 0.10;
	// szum: losowy piksel czarny/biały z losową alfą
	vec2 cell = floor(px) + mod(floor(TIME * 60.0), 997.0) * vec2(137.0, 71.0);
	float h = hash(cell);
	float g = hash(cell + vec2(4096.0, 777.0)) * grain;
	vec3 gcol = h < 0.5 ? vec3(0.0) : vec3(1.0);
	// złożenie: najpierw winieta (czerń), potem odcień, potem szum
	vec3 col = mix(vec3(0.0), vec3(0.31, 0.16, 0.63), chroma / max(v + chroma, 1e-4));
	float a = clamp(v + chroma, 0.0, 1.0);
	col = mix(col, gcol, g / max(a + g, 1e-4));
	a = clamp(a + g * (1.0 - a), 0.0, 1.0);
	COLOR = vec4(col, a);
}
