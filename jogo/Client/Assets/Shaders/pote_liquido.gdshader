// Líquido do pote da barra da gárgula/dragão: superfície com onda, balanço quando o valor muda, redemoinho, bolhas e brilho.
// Desenhado num ColorRect do MESMO tamanho da barra; a máscara (interior do pote) recorta.
shader_type canvas_item;

uniform sampler2D mascara : filter_linear;
uniform float nivel : hint_range(0.0, 1.0) = 1.0;     // fração de vida/mana
uniform float topo_uv = 0.4018;                        // y do topo do interior (uv da barra)
uniform float fundo_uv = 0.8268;                       // y do fundo do interior
uniform float centro_x_uv = 0.1636;                    // x do centro do pote
uniform float meia_largura_uv = 0.0566;                // meia largura do interior
uniform vec4 cor_clara : source_color = vec4(1.0, 0.12, 0.08, 1.0);
uniform vec4 cor_funda : source_color = vec4(0.22, 0.0, 0.02, 1.0);
uniform float balanco = 0.0;                           // 0..1, sobe quando o valor muda e decai
uniform float inclinacao = 0.0;                        // -1..1, inclina a superfície no balanço
uniform float bolhas = 1.0;
uniform float respingo = 1.0;                         // 0 desliga os respingos de pote cheio

float hash(float n) { return fract(sin(n * 127.1) * 43758.5453); }

void fragment() {
    float m = texture(mascara, UV).a;
    float t0 = TIME;
    // respingos: com o pote cheio (e mais ainda balançando), gotas pulam da boca e caem para fora
    float cheio = smoothstep(0.9, 1.0, nivel) * respingo;
    if (m < 0.02) {
        if (cheio <= 0.0) discard;
        float gota = 0.0;
        for (int i = 0; i < 8; i++) {
            float fi = float(i);
            float per = 1.4 + hash(fi + 2.0) * 1.2;
            float f = fract(t0 / per + hash(fi + 5.0));
            float lado = hash(fi + 11.0) < 0.5 ? -1.0 : 1.0;
            float x0 = centro_x_uv + lado * meia_largura_uv * (0.55 + 0.4 * hash(fi + 3.0));
            float px = x0 + lado * meia_largura_uv * 0.9 * f;
            float py = topo_uv - (0.09 + 0.05 * hash(fi + 7.0)) * (4.0 * f * (1.0 - f)) + 0.12 * f * f;
            float r = 0.0045 * (1.0 - 0.5 * f);
            vec2 dd = vec2((UV.x - px) * 4.5, UV.y - py);
            gota = max(gota, smoothstep(r, r * 0.4, length(dd)) * (1.0 - f) * (0.4 + balanco));
        }
        gota *= cheio;
        if (gota < 0.02) discard;
        COLOR = vec4(cor_clara.rgb * 1.3, gota);
    } else {   // (Godot 4.7 não aceita return no fragment: o resto vai no else)
    float xr = (UV.x - centro_x_uv) / meia_largura_uv;          // -1..1 dentro do pote
    float t = TIME;
    float h = mix(fundo_uv, topo_uv, nivel);
    float onda = 0.0035 * sin(xr * 3.1 + t * 2.0) + 0.0025 * sin(xr * 7.3 - t * 3.3)
               + balanco * (0.03 * sin(xr * 2.4 + t * 8.5) + 0.012 * sin(xr * 6.0 - t * 11.0))
               + inclinacao * 0.04 * xr;
    float sup = h + onda;
    if (UV.y < sup) discard;
    float alt = fundo_uv - topo_uv;
    float prof = clamp((UV.y - sup) / alt, 0.0, 1.0);
    // redemoinho lento
    float yy = UV.y / alt;
    float n = sin(xr * 3.0 + t * 0.7 + sin(yy * 9.0 - t * 0.5) * 1.8) * 0.5 + 0.5;
    n = mix(n, sin(xr * 6.5 - t * 1.1 + yy * 14.0) * 0.5 + 0.5, 0.35);
    vec3 c = mix(cor_clara.rgb, cor_funda.rgb, smoothstep(0.0, 1.0, prof) * 0.85);
    c *= 0.75 + 0.5 * n;
    // escurece nas bordas do vidro (volume) e clareia no meio
    c *= 1.15 - 0.45 * xr * xr;
    // linha brilhante da superfície e espuma no balanço
    float faixa = UV.y - sup;
    c += cor_clara.rgb * smoothstep(0.012, 0.0, faixa) * (0.8 + balanco);
    c += vec3(1.0) * smoothstep(0.004, 0.0, faixa) * 0.25 * balanco;
    // bolhas subindo
    for (int i = 0; i < 7; i++) {
        float fi = float(i);
        float bx = (hash(fi + 1.3) * 1.6 - 0.8);
        float vel = 0.08 + 0.1 * hash(fi + 7.1);
        float by = fundo_uv - fract(t * vel + hash(fi + 3.7)) * (fundo_uv - sup);
        float r = (0.006 + 0.006 * hash(fi + 9.2));
        vec2 d = vec2((xr - bx - 0.08 * sin(t * 2.0 + fi)) * meia_largura_uv, UV.y - by);
        float b = smoothstep(r, r * 0.55, length(d)) - smoothstep(r * 0.55, r * 0.2, length(d)) * 0.6;
        c += cor_clara.rgb * b * 0.8 * bolhas;
    }
    COLOR = vec4(c, m);
    }
}
