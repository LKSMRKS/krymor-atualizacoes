// VFX das habilidades: desenho no chao num quadrado deitado (UV 0..1). Anel que se expande (onda de choque, anel de
// fogo com labaredas), circulo de runas girando (magia de area, cura, aura) e marca de mira. Aditivo.
shader_type spatial;
render_mode blend_add, unshaded, cull_disabled, depth_draw_never, shadows_disabled;
#include "res://Assets/Shaders/vfx_ruido.gdshaderinc"

uniform vec4 cor : source_color = vec4(1.0, 0.5, 0.15, 1.0);
uniform vec4 cor_quente : source_color = vec4(1.0, 0.95, 0.7, 1.0);
uniform float raio : hint_range(0.0, 1.0) = 0.8;     // posicao do anel (1 = borda do quadrado)
uniform float espessura = 0.08;
uniform float chamas = 0.0;      // 0..1: labaredas subindo do anel (ruido correndo para fora)
uniform float preenche = 0.0;    // 0..1: disco translucido dentro do anel
uniform float runas = 0.0;       // 0..1: circulos e marcas de runa girando
uniform float giro = 0.4;
uniform float alfa : hint_range(0.0, 1.0) = 1.0;

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	if (r > 1.0) discard;
	float a = atan(p.y, p.x);
	float n = vfx_fbm(vec2(a * 2.5 + 10.0, r * 5.0 - TIME * 3.0));
	float largura = espessura * (1.0 + chamas * (n * 2.2 - 0.4));
	float anel = smoothstep(largura, 0.0, abs(r - raio));
	// labaredas: lingua de fogo para fora do anel, recortada pelo ruido
	float lingua = chamas * smoothstep(raio + 0.25, raio, r) * step(raio, r) * smoothstep(0.45, 0.8, n);
	float disco = preenche * smoothstep(raio, raio - 0.05, r) * (0.25 + 0.35 * vfx_fbm(p * 4.0 + TIME * 0.4));
	float ar = a + TIME * giro;
	float circ = smoothstep(0.012, 0.0, abs(r - 0.92)) + smoothstep(0.01, 0.0, abs(r - 0.78)) + smoothstep(0.008, 0.0, abs(r - 0.4));
	float marcas = step(0.86, fract(ar * 12.0 / 6.2831)) * step(0.80, r) * step(r, 0.90);
	float h1 = abs(r * cos(mod(ar, 1.0472) - 0.5236) - 0.62);
	float h2 = abs(r * cos(mod(ar + 0.5236, 1.0472) - 0.5236) - 0.62);
	float estrela = (smoothstep(0.012, 0.0, h1) + smoothstep(0.012, 0.0, h2)) * step(r, 0.78);
	float runa = runas * (circ + marcas + estrela) * (0.7 + 0.3 * sin(TIME * 4.0));
	float k = anel + lingua + disco + runa;
	vec3 c = mix(cor.rgb, cor_quente.rgb, clamp(anel * (1.0 - chamas * n), 0.0, 1.0));
	ALBEDO = c * k * alfa * cor.a;
}
