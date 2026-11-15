// VFX das habilidades: ar tremendo (calor / onda de choque). Le a tela atras e desloca pelas normais na borda da malha.
// So no grafico ALTO com renderizador moderno (Client/Efeitos/VfxKit.ComDistorcao).
shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never, shadows_disabled;
#include "res://Assets/Shaders/vfx_ruido.gdshaderinc"

uniform sampler2D tela : hint_screen_texture, filter_linear_mipmap;
uniform float forca = 0.03;
uniform float alfa : hint_range(0.0, 1.0) = 1.0;

void fragment() {
	float fr = pow(1.0 - clamp(abs(dot(NORMAL, VIEW)), 0.0, 1.0), 2.0);
	float n = vfx_ruido(UV * 12.0 + TIME * 2.0) - 0.5;
	vec2 desloca = (NORMAL.xy * fr + vec2(n)) * forca * alfa;
	ALBEDO = textureLod(tela, SCREEN_UV + desloca, 0.0).rgb;
	ALPHA = clamp(fr * 1.5, 0.0, 1.0) * alfa;
}
