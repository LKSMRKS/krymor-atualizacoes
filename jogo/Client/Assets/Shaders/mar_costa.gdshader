// Mar da Costa do Cal: uma faixa so faz areia molhada -> arrebentacao com espuma -> mar com ondas e profundidade.
// Malha: plano subdividido com o eixo Z LOCAL saindo da praia para o mar (z < 0 = areia, z = 0 = linha d'agua).
// Montado por MarDaCosta.cs. Texturas: ter_areia_molhada(.png/_n.png), fx_agua_n.png, fx_espuma.png.
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_opaque, specular_schlick_ggx;

uniform vec3 cor_raso : source_color = vec3(0.16, 0.42, 0.42);
uniform vec3 cor_fundo : source_color = vec3(0.04, 0.14, 0.22);
uniform vec3 cor_espuma : source_color = vec3(0.93, 0.95, 0.92);
uniform sampler2D areia_molhada : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D areia_n : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D agua_n : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D espuma : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform float largura_areia = 14.0;   // faixa de areia molhada antes da agua (m)
uniform float tile_areia = 6.0;
uniform float onda_altura = 0.45;
uniform float vaivem = 2.6;           // quanto a agua sobe e desce na areia (m)

varying float dist;      // distancia da linha d'agua (local z)
varying vec3 wp;

void vertex() {
	dist = VERTEX.z;
	wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float longe = smoothstep(6.0, 60.0, VERTEX.z);
	float o = sin(wp.x * 0.11 + TIME * 0.9) * 0.6 + sin(wp.x * 0.23 - VERTEX.z * 0.17 + TIME * 1.4) * 0.4
	        + sin(VERTEX.z * 0.35 - TIME * 1.2) * 0.5;
	VERTEX.y += longe * onda_altura * o;
}

void fragment() {
	float swash = vaivem * (0.6 * sin(TIME * 0.45) + 0.4 * sin(TIME * 0.83 + 1.7));
	float s = dist - swash;                        // < 0: areia (molhada perto da agua) ; > 0: agua
	vec2 uvA = wp.xz / tile_areia;
	if (s < 0.0) {
		float molhado = smoothstep(-largura_areia, -0.5, s);
		// marca da ultima onda: risco de espuma fina que fica na areia
		float marca = smoothstep(0.35, 0.0, abs(s + vaivem * 0.9 + 0.3)) * texture(espuma, wp.xz * 0.12).r;
		vec3 areia = texture(areia_molhada, uvA).rgb;
		ALBEDO = mix(areia, cor_espuma, marca * 0.6);
		NORMAL_MAP = texture(areia_n, uvA).rgb;
		ROUGHNESS = mix(0.85, 0.18, molhado);
		ALPHA = molhado * 0.92;
	} else {
		float fundo = smoothstep(0.0, 70.0, s);
		vec3 c = mix(cor_raso, cor_fundo, fundo);
		vec2 f1 = wp.xz * 0.06 + vec2(TIME * 0.02, TIME * 0.013);
		vec2 f2 = wp.xz * 0.11 - vec2(TIME * 0.017, -TIME * 0.021);
		NORMAL_MAP = normalize(texture(agua_n, f1).rgb + texture(agua_n, f2).rgb - 1.0) * 0.5 + 0.5;
		// arrebentacao: borda branca na linha d'agua + linhas de espuma que chegam do mar
		float borda = smoothstep(2.2, 0.0, s);
		float linhas = smoothstep(0.75, 1.0, sin(s * 0.9 + TIME * 1.6)) * smoothstep(18.0, 3.0, s);
		float tex = texture(espuma, wp.xz * 0.09 + vec2(0.0, TIME * 0.03)).r;
		float esp = clamp(borda * (0.55 + 0.6 * tex) + linhas * tex * 1.2, 0.0, 1.0);
		ALBEDO = mix(c, cor_espuma, esp);
		ROUGHNESS = mix(0.04, 0.6, esp);
		SPECULAR = 0.6;
		ALPHA = mix(mix(0.55, 0.96, smoothstep(0.0, 8.0, s)), 1.0, esp * 0.8);
	}
}
