// VFX das habilidades: parede d'agua da Serpente do Mar (malha com V de baixo para cima). Azul fundo embaixo, crista
// clara com espuma de ruido correndo, transparente nas pontas.
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_never, shadows_disabled;
#include "res://Assets/Shaders/vfx_ruido.gdshaderinc"

uniform vec4 fundo : source_color = vec4(0.03, 0.22, 0.42, 0.85);
uniform vec4 crista : source_color = vec4(0.55, 0.85, 1.0, 0.95);
uniform float alfa : hint_range(0.0, 1.0) = 1.0;

void fragment() {
	float n = vfx_fbm(vec2(UV.x * 6.0 - TIME * 0.6, UV.y * 3.0 - TIME * 2.2));
	float espuma = smoothstep(0.62, 0.8, n + UV.y * 0.45);
	vec4 c = mix(fundo, crista, smoothstep(0.35, 1.0, UV.y));
	c.rgb = mix(c.rgb, vec3(0.95, 0.98, 1.0), espuma);
	float borda = smoothstep(0.0, 0.15, UV.x) * smoothstep(1.0, 0.85, UV.x) * smoothstep(1.0, 0.92, UV.y + n * 0.1);
	ALBEDO = c.rgb;
	EMISSION = c.rgb * 0.35 + vec3(espuma) * 0.4;
	ROUGHNESS = 0.15;
	ALPHA = c.a * borda * alfa;
}
