// Tryby mikroskopu jako post-process obrazu świata:
// 1 = ciemne pole (odwrócenie + obrót barwy), 2 = jasne pole (mniej kontrastu),
// 3 = TEM w skali szarości, 4 = barwienie negatywne
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform int mode = 0;

vec3 hue_rotate(vec3 c, float a) {
	const mat3 toYIQ = mat3(vec3(0.299, 0.596, 0.211), vec3(0.587, -0.274, -0.523), vec3(0.114, -0.322, 0.312));
	const mat3 toRGB = mat3(vec3(1.0, 1.0, 1.0), vec3(0.956, -0.272, -1.106), vec3(0.621, -0.647, 1.703));
	vec3 yiq = toYIQ * c;
	float h = atan(yiq.z, yiq.y) + a;
	float ch = length(yiq.yz);
	return toRGB * vec3(yiq.x, ch * cos(h), ch * sin(h));
}

void fragment() {
	vec3 c = texture(screen_tex, SCREEN_UV).rgb;
	if (mode == 1) {
		c = hue_rotate(1.0 - c, 3.14159265);
		c = (c - 0.5) * 1.25 + 0.5;
		c *= 0.9;
	} else if (mode == 2) {
		c = (c - 0.5) * 0.7 + 0.5;
		c *= 1.12;
	} else if (mode == 3) {
		float g = dot(c, vec3(0.299, 0.587, 0.114));
		c = vec3((g - 0.5) * 1.2 + 0.5);
	} else if (mode == 4) {
		float g = 1.0 - dot(c, vec3(0.299, 0.587, 0.114));
		c = vec3((g - 0.5) * 1.3 + 0.5);
	}
	COLOR = vec4(clamp(c, 0.0, 1.0), 1.0);
}
