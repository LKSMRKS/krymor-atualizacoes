// VFX das habilidades: material "solido" que nasce e some se desfazendo (cristais de gelo, pedras em brasa, raizes,
// espinhos, tronco, espadas espectrais). Recebe luz; brilho proprio por veios de ruido; borda acesa no dissolve.
shader_type spatial;
render_mode cull_back, shadows_disabled;
#include "res://Assets/Shaders/vfx_ruido.gdshaderinc"

uniform vec4 albedo : source_color = vec4(0.6, 0.85, 1.0, 1.0);
uniform vec4 emissao : source_color = vec4(0.2, 0.6, 1.0, 1.0);
uniform float forca_emissao = 1.0;
uniform float veios = 0.0;          // 0..1: rachaduras/veios brilhantes (pedra em brasa, raiz viva)
uniform float escala_ruido = 5.0;
uniform float gelo = 0.0;           // 0..1: borda fresnel clara e superficie lisa (cristal)
uniform float rugosidade = 0.7;
uniform vec4 cor_borda : source_color = vec4(1.0, 0.8, 0.4, 1.0);
uniform float dissolve : hint_range(0.0, 1.0) = 0.0;
uniform float musgo = 0.0;          // 0..1: manchas de musgo no albedo (raízes) — sem brilho
uniform vec4 cor_musgo : source_color = vec4(0.22, 0.32, 0.12, 1.0);

void fragment() {
	float n = vfx_fbm(UV * escala_ruido);
	if (n < dissolve) discard;
	float v = 1.0 - smoothstep(0.0, 0.022, abs(vfx_fbm(UV * escala_ruido * 1.7 + 3.0) - 0.5));
	float fr = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	ALBEDO = albedo.rgb * mix(0.7 + 0.3 * n, 1.0, gelo);
	ALBEDO = mix(ALBEDO, cor_musgo.rgb * (0.8 + 0.4 * n), musgo * smoothstep(0.5, 0.68, vfx_fbm(UV * vec2(3.0, 9.0) + 11.0)));
	ROUGHNESS = mix(rugosidade, 0.08, gelo);
	SPECULAR = mix(0.5, 0.9, gelo);
	float borda = smoothstep(dissolve + 0.07, dissolve, n) * step(0.001, dissolve);
	EMISSION = emissao.rgb * forca_emissao * (0.25 + veios * v * 2.5 + gelo * fr * 1.8) + cor_borda.rgb * borda * 3.0;
}
