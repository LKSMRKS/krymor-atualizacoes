// VFX das habilidades: rastro de lamina num arco (malha com U ao longo do arco e V de dentro para fora). A "cabeca"
// do corte anda com 'progresso' e deixa uma cauda que some; ponta clara, fios de ruido no sentido do golpe.
shader_type spatial;
render_mode blend_add, unshaded, cull_disabled, depth_draw_never, shadows_disabled;
#include "res://Assets/Shaders/vfx_ruido.gdshaderinc"

uniform vec4 cor : source_color = vec4(0.7, 0.85, 1.0, 1.0);
uniform vec4 cor_ponta : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float progresso = 0.5;   // 0..1.4 (passa do fim para a cauda sumir)
uniform float cauda = 0.55;
uniform float intensidade = 1.6;
uniform float alfa : hint_range(0.0, 1.0) = 1.0;

void fragment() {
	float u = UV.x; float v = UV.y;
	float atras = progresso - u;
	if (atras < 0.0) discard;
	float k = 1.0 - clamp(atras / cauda, 0.0, 1.0);
	k = k * k;
	float fios = 0.55 + 0.45 * vfx_ruido(vec2(u * 3.0 - progresso * 2.0, v * 18.0));
	float perfil = smoothstep(0.0, 0.35, v) * (0.35 + 0.65 * smoothstep(0.55, 1.0, v));   // mais forte na borda de fora
	float cabeca = smoothstep(0.12, 0.0, atras);
	vec3 c = mix(cor.rgb, cor_ponta.rgb, clamp(cabeca + smoothstep(0.85, 1.0, v) * 0.6, 0.0, 1.0));
	ALBEDO = c * k * fios * perfil * intensidade * alfa * cor.a;
}
