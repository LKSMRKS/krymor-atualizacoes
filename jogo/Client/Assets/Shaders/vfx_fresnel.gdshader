// VFX das habilidades: casca de energia (escudos, esferas, lancas, colunas de luz). Brilho na borda (fresnel), favo de
// mel opcional (escudo), ruido correndo e dissolve com borda acesa. Aditivo: nao escurece nada atras.
shader_type spatial;
render_mode blend_add, unshaded, cull_disabled, depth_draw_never, shadows_disabled;
#include "res://Assets/Shaders/vfx_ruido.gdshaderinc"

uniform vec4 cor : source_color = vec4(0.4, 0.7, 1.0, 1.0);
uniform vec4 cor_borda : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float intensidade = 1.6;
uniform float potencia = 2.5;
uniform float nucleo = 0.08;        // brilho no meio (0 = so a borda)
uniform float favo = 0.0;           // 0..1: desenho de favo (escudo)
uniform float faixas = 0.0;         // 0..1: faixas correndo ao longo de V (lanca, coluna)
uniform float rolagem = 1.0;
uniform float dissolve : hint_range(0.0, 1.0) = 0.0;
uniform float alfa : hint_range(0.0, 1.0) = 1.0;

float favo_linhas(vec2 uv) {
	vec2 p = uv * vec2(14.0, 8.0);
	p.x += mod(floor(p.y), 2.0) * 0.5;
	vec2 f = abs(fract(p) - 0.5);
	return smoothstep(0.40, 0.48, max(f.x, f.y));
}

void fragment() {
	float fr = pow(1.0 - clamp(abs(dot(NORMAL, VIEW)), 0.0, 1.0), potencia);
	float n = vfx_fbm(UV * vec2(6.0, 3.0) + vec2(0.0, -TIME * rolagem));
	float corpo = fr + nucleo + favo * favo_linhas(UV) * (0.35 + 0.65 * n);
	corpo += faixas * smoothstep(0.55, 0.9, vfx_ruido(vec2(UV.x * 10.0, UV.y * 3.0 - TIME * rolagem * 4.0)));
	if (n < dissolve) discard;
	float borda = smoothstep(dissolve + 0.08, dissolve, n) * step(0.001, dissolve);
	vec3 c = cor.rgb * corpo * intensidade * (0.75 + 0.5 * n) + cor_borda.rgb * borda * 2.0;
	ALBEDO = c * alfa * cor.a;
}
