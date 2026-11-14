// Contorno escuro dos inimigos (casco invertido): o bicho se separa do chao claro da Costa a 8 m de camera.
// Uso: material de cada superficie do monstro -> NextPass = ShaderMaterial com este shader.
// Funciona com malha com esqueleto (o VERTEX ja vem deformado). largura em metros do modelo.
shader_type spatial;
render_mode unshaded, cull_front, depth_draw_opaque, shadows_disabled, fog_disabled;
uniform vec4 cor : source_color = vec4(0.10, 0.06, 0.05, 1.0);
uniform float largura = 0.014;
uniform float alcance_camera = 18.0; // contorno some aos poucos alem desta distancia (nao vira risco grosso longe)
void vertex() {
	VERTEX += NORMAL * largura;
}
void fragment() {
	float d = length(VERTEX);
	ALBEDO = cor.rgb;
	if (d > alcance_camera) { discard; }
}
