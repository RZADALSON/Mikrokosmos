// Tło pola widzenia mikroskopu: gradient oświetlenia, nierównomierność i kurz na optyce
shader_type canvas_item;

uniform vec4 bg_in : source_color = vec4(0.79, 0.8, 0.73, 1.0);
uniform vec4 bg_mid : source_color = vec4(0.69, 0.7, 0.64, 1.0);
uniform vec4 bg_out : source_color = vec4(0.42, 0.44, 0.38, 1.0);
uniform float uneven = 0.04;
uniform float dust = 1.0;
uniform float seed = 1.0;
uniform vec2 resolution = vec2(1600.0, 900.0);

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7)) + seed * 17.13) * 43758.5453); }
float vnoise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
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
