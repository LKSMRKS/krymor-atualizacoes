// Liquido do orbe da HUD (estilo Diablo/MU): esfera cheia ate 'nivel', redemoinho animado, superficie ondulando com
// menisco claro, bolhas subindo e bordas escuras (volume). Usado por OrbeHud.cs num ColorRect quadrado.
shader_type canvas_item;

uniform float nivel : hint_range(0.0, 1.0) = 1.0;
uniform vec4 cor : source_color = vec4(0.80, 0.07, 0.06, 1.0);
uniform vec4 cor_funda : source_color = vec4(0.16, 0.0, 0.02, 1.0);
uniform float clarao = 0.0;      // pisca ao tomar dano (0..1)
uniform float inclina = 0.0;     // 13/11: o liquido balanca quando a camera gira / voce anda (superficie inclinada)
uniform float agita = 0.0;       // 13/11: quanto a superficie ondula a mais enquanto balanca

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p); vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}
float fbm(vec2 p) { float v = 0.0; float a = 0.5; for (int i = 0; i < 4; i++) { v += a * vnoise(p); p = p * 2.03 + 7.1; a *= 0.5; } return v; }

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;          // -1..1, y para baixo
	float r = length(uv);
	if (r > 1.0) discard;
	float onda = (0.025 + 0.05 * agita) * sin(uv.x * 6.0 + TIME * (2.1 + 4.0 * agita)) + (0.012 + 0.03 * agita) * sin(uv.x * 13.0 - TIME * 3.3);
	float topo = 1.0 - 2.0 * nivel + onda + inclina * uv.x;   // linha da superficie (em uv.y), inclinada pelo balanco
	float dentro = smoothstep(topo - 0.012, topo + 0.012, uv.y);
	// redemoinho: ruido deformado por ruido, girando devagar
	vec2 q = uv * 2.3;
	float ang = TIME * 0.12;
	q = mat2(vec2(cos(ang), -sin(ang)), vec2(sin(ang), cos(ang))) * q;
	float n = fbm(q + vec2(fbm(q * 1.4 + TIME * 0.18), fbm(q * 1.4 - TIME * 0.13)) * 1.6);
	vec3 c = mix(cor_funda.rgb, cor.rgb, 0.35 + 0.75 * n);
	// volume da esfera: centro mais claro, bordas escuras; fundo do liquido mais escuro
	c *= mix(1.25, 0.45, smoothstep(0.15, 1.0, r));
	c *= mix(1.0, 0.7, smoothstep(-0.2, 1.0, uv.y));
	// menisco claro e bolhas
	float men = smoothstep(0.045, 0.0, abs(uv.y - topo));
	c += cor.rgb * men * 0.9;
	for (int i = 0; i < 6; i++) {
		float fi = float(i);
		vec2 b = vec2(sin(fi * 2.7 + TIME * 0.6) * 0.55, 1.0 - fract(TIME * (0.12 + fi * 0.025) + fi * 0.37) * 2.0);
		if (b.y > topo) c += vec3(1.0) * smoothstep(0.035, 0.0, length(uv - b)) * 0.35;
	}
	vec3 vazio = vec3(0.025, 0.022, 0.026) * (1.2 - r * 0.6);
	vec3 col = mix(vazio, c, dentro);
	col = mix(col, vec3(1.0, 0.85, 0.8), clarao * 0.5);
	COLOR = vec4(col, 1.0);
}
