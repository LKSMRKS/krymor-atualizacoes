// Contorno do NPC / objeto (casca invertida), usado como MaterialOverlay de cada malha.
// 11/11 noite (pedido: "só o contorno de fora, não nas juntas; um risquinho sutil"): casca fina e EMPURRADA PARA TRÁS
// (longe da câmera) — por cima do próprio corpo ela fica escondida (braço na frente do tronco, juntas), e só aparece
// na borda de fora, contra o fundo.
shader_type spatial;
render_mode unshaded, cull_front, depth_draw_never, blend_mix, skip_vertex_transform;

uniform vec4 cor : source_color = vec4(1.0, 1.0, 1.0, 0.35);
uniform float espessura = 0.009;      // metros
uniform float recuo = 0.35;           // metros para trás (esconde as linhas por dentro do corpo)
uniform float pulso = 0.2;            // 0 = parado
uniform float velocidade = 2.4;

void vertex() {
    vec3 n = normalize((MODELVIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
    VERTEX = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
    float dist = max(1.0, -VERTEX.z * 0.12);                 // mesma grossura na tela de perto e de longe
    VERTEX += vec3(n.xy, 0.0) * espessura * dist;            // cresce só para os lados da tela
    float z0 = VERTEX.z;
    VERTEX.z -= recuo;
    VERTEX.xy *= VERTEX.z / min(z0, -0.05);                // mesma posição na tela depois de recuar
    NORMAL = n;
}

void fragment() {
    float p = 1.0 - pulso * (0.5 + 0.5 * sin(TIME * velocidade));
    ALBEDO = cor.rgb;
    ALPHA = cor.a * p;
}
