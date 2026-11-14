// 12/11 noite (pedido do dono: "capa, véu e asa mexendo"): pano e asa das armaduras/formas balançam no próprio shader.
// A cor de vértice traz o quanto cada ponto é solto: alfa 0–0.47 = pano (capa, manto, casaco, véu), 0.53–1 = asa.
// O esqueleto já foi aplicado quando vertex() roda, então o balanço vai por cima da animação.
// Espaço do modelo do Godot: frente = +Z, cima = +Y.
shader_type spatial;
render_mode cull_disabled;

uniform sampler2D tex_cor : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D tex_orm : hint_default_white, filter_linear_mipmap, repeat_enable;   // G = rugosidade, B = metal (glTF)
uniform sampler2D tex_normal : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D tex_emissao : source_color, hint_default_black, filter_linear_mipmap, repeat_enable;
uniform bool tem_orm = false;
uniform bool tem_normal = false;
uniform bool tem_emissao = false;
uniform vec4 cor_base : source_color = vec4(1.0);
uniform float energia_emissao = 1.0;

uniform float velocidade = 0.0;   // m/s do personagem (o MannequinVisual atualiza)
uniform float giro = 0.0;         // curva (rad/s): a capa abre para o lado de fora
uniform float fase = 0.0;         // para dois personagens não balançarem iguais
uniform float forca = 1.0;

void vertex() {
    float a = COLOR.a;
    float asa = step(0.5, a);
    float w = asa > 0.5 ? clamp((a - 0.53) / 0.47, 0.0, 1.0) : clamp(a / 0.47, 0.0, 1.0);
    float t = TIME + fase;
    float corre = clamp(velocidade / 6.0, 0.0, 1.5);
    if (asa < 0.5) {
        // pano: fica para trás quando corre, sobe um pouco, ondula com o vento e abre na curva
        float onda = sin(t * (1.6 + corre * 3.0) + VERTEX.y * 4.0 + VERTEX.x * 2.5);
        float onda2 = cos(t * 1.1 + VERTEX.y * 2.7 - VERTEX.x * 3.1);
        vec3 d = vec3(0.0);
        d.z -= (0.03 + 0.22 * corre) * w * w;                // para trás
        d.y += 0.07 * corre * w * w;                         // levanta ao correr
        d.x += (0.012 + 0.03 * corre) * onda * w - giro * 0.06 * w * w;
        d.z += (0.01 + 0.035 * corre) * onda2 * w;
        VERTEX += d * forca;
    } else {
        // asa: bate devagar parada, mais rápido correndo; a ponta mexe mais que a raiz
        float bate = sin(t * (1.4 + corre * 2.6));
        float lado = sign(VERTEX.x);
        VERTEX.y += bate * 0.16 * w * w * forca;
        VERTEX.z -= (0.05 + 0.18 * corre) * w * w * forca;
        VERTEX.x -= lado * abs(bate) * 0.05 * w * w * forca;
    }
}

void fragment() {
    vec4 c = texture(tex_cor, UV) * cor_base;
    ALBEDO = c.rgb;
    if (tem_orm) {
        vec3 orm = texture(tex_orm, UV).rgb;
        ROUGHNESS = orm.g;
        METALLIC = orm.b;
    } else {
        ROUGHNESS = 0.75;
        METALLIC = 0.0;
    }
    if (tem_normal) NORMAL_MAP = texture(tex_normal, UV).rgb;
    if (tem_emissao) EMISSION = texture(tex_emissao, UV).rgb * energia_emissao;
}
