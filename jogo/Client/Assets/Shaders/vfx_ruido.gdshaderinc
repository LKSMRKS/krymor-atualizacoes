// VFX das habilidades (Client/Efeitos): ruido de valor e fbm sem textura (nao depende de NoiseTexture2D, que gera em thread).
float vfx_hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vfx_ruido(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p); vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(vfx_hash(i), vfx_hash(i + vec2(1.0, 0.0)), u.x), mix(vfx_hash(i + vec2(0.0, 1.0)), vfx_hash(i + vec2(1.0, 1.0)), u.x), u.y);
}
float vfx_fbm(vec2 p) { float v = 0.0; float a = 0.5; for (int i = 0; i < 4; i++) { v += a * vfx_ruido(p); p = p * 2.03 + 7.1; a *= 0.5; } return v; }
