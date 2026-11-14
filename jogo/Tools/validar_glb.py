#!/usr/bin/env python3
"""
Confere um modelo .glb contra Docs/ESPECIFICACAO_MODELOS_3D.md (só biblioteca padrão do Python 3).

Uso:
  python Tools/validar_glb.py Client/Assets/Models/guerreiro.glb --tipo guerreiro
  python Tools/validar_glb.py Client/Assets/Models/carrapato.glb  --tipo carrapato
  python Tools/validar_glb.py --autoteste          (constrói modelos sintéticos e prova que a conferência funciona)

Cada verificação sai como PASSA, AVISO ou FALHA. Código de saída 1 se houver FALHA.
"""
import json
import math
import re
import struct
import sys

# ------------------------------------------------------------------ regras por tipo
ANIMACOES = {
    "guerreiro": {
        "idle": (1.5, 4.0), "walk": (0.6, 1.4), "run": (0.4, 1.0), "attack_basic": (0.3, 0.9),
        "attack_golpe": (0.6, 0.8), "guard_start": (0.15, 0.4), "guard_loop": (0.8, 2.0),
        "guard_end": (0.15, 0.4), "hit": (0.2, 0.5), "death": (0.8, 2.0), "drink": (0.5, 1.2),
    },
    "carrapato": {
        "idle": (1.5, 4.0), "walk": (0.5, 1.2), "attack": (0.6, 1.2), "hit": (0.2, 0.5), "death": (0.8, 1.6),
    },
}
OPCIONAIS = {"guerreiro": {"cast", "pickup", "respawn"}, "carrapato": {"spawn"}}
TRIANGULOS = {"guerreiro": (8_000, 45_000), "carrapato": (5_000, 35_000)}
ALTURA = {"guerreiro": (1.75, 2.05, 1.5, 2.4), "carrapato": (1.0, 1.4, 0.8, 1.7)}   # ok_min, ok_max, falha_min, falha_max
LARGURA_MAX = {"guerreiro": (1.4, 2.0), "carrapato": (2.2, 3.0)}                      # ok, falha
OSSOS = {"guerreiro": (18, 80), "carrapato": (10, 60)}
PECAS = {"guerreiro": {"espada": True, "escudo": True, "elmo": False}, "carrapato": {}}

COMPONENTES = {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}
TIPOS = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT2": 4, "MAT3": 9, "MAT4": 16}


class Relatorio:
    def __init__(self):
        self.itens = []

    def add(self, nivel, nome, detalhe=""):
        self.itens.append((nivel, nome, detalhe))

    def passa(self, nome, detalhe=""): self.add("PASSA", nome, detalhe)
    def aviso(self, nome, detalhe=""): self.add("AVISO", nome, detalhe)
    def falha(self, nome, detalhe=""): self.add("FALHA", nome, detalhe)

    @property
    def falhas(self):
        return sum(1 for n, _, _ in self.itens if n == "FALHA")

    def imprimir(self):
        for nivel, nome, detalhe in self.itens:
            print(f"[{nivel:5}] {nome}" + (f": {detalhe}" if detalhe else ""))
        avisos = sum(1 for n, _, _ in self.itens if n == "AVISO")
        print(f"\nResumo: {len(self.itens) - self.falhas - avisos} passam, {avisos} avisos, {self.falhas} falhas.")


# ------------------------------------------------------------------ leitura do .glb
def ler_glb(caminho_ou_bytes):
    dados = caminho_ou_bytes if isinstance(caminho_ou_bytes, bytes) else open(caminho_ou_bytes, "rb").read()
    if len(dados) < 20 or dados[:4] != b"glTF":
        raise ValueError("não é um arquivo .glb (cabeçalho 'glTF' ausente)")
    versao, tamanho = struct.unpack_from("<II", dados, 4)
    if versao != 2:
        raise ValueError(f"versão {versao} do glTF; esperado 2")
    pos, js, binario = 12, None, b""
    while pos + 8 <= len(dados) and pos < tamanho:
        n, tipo = struct.unpack_from("<II", dados, pos)
        corpo = dados[pos + 8: pos + 8 + n]
        if tipo == 0x4E4F534A:
            js = json.loads(corpo.decode("utf-8"))
        elif tipo == 0x004E4942 and not binario:
            binario = corpo
        pos += 8 + n
    if js is None:
        raise ValueError("bloco JSON ausente")
    return js, binario


def acessor(js, indice):
    return js["accessors"][indice]


def ler_escalares(js, binario, indice):
    """Lê um acessor SCALAR/VEC como lista (suficiente para tempos de animação)."""
    a = acessor(js, indice)
    vista = js["bufferViews"][a["bufferView"]]
    comps = TIPOS[a["type"]]
    tam = COMPONENTES[a["componentType"]]
    fmt = {5120: "b", 5121: "B", 5122: "h", 5123: "H", 5125: "I", 5126: "f"}[a["componentType"]]
    passo = vista.get("byteStride") or comps * tam
    base = vista.get("byteOffset", 0) + a.get("byteOffset", 0)
    saida = []
    for i in range(a["count"]):
        saida.append(struct.unpack_from("<" + fmt * comps, binario, base + i * passo))
    return saida


# ------------------------------------------------------------------ dimensões de imagem sem bibliotecas
def dimensoes_imagem(dados):
    if dados[:8] == b"\x89PNG\r\n\x1a\n":
        return struct.unpack(">II", dados[16:24])
    if dados[:2] == b"\xff\xd8":
        i = 2
        while i + 9 < len(dados):
            if dados[i] != 0xFF:
                i += 1
                continue
            marcador = dados[i + 1]
            if marcador in (0xC0, 0xC1, 0xC2):
                altura, largura = struct.unpack(">HH", dados[i + 5:i + 9])
                return largura, altura
            i += 2 + struct.unpack(">H", dados[i + 2:i + 4])[0]
    return None


# ------------------------------------------------------------------ conferência
def conferir(js, binario, tipo):
    r = Relatorio()
    nos = js.get("nodes", [])
    nomes_nos = [n.get("name", "") for n in nos]

    # --- triângulos e limites
    triangulos = 0
    minimos, maximos = [math.inf] * 3, [-math.inf] * 3
    meshes_com_pele = set()
    for n in nos:
        if "mesh" in n and "skin" in n:
            meshes_com_pele.add(n["mesh"])
    sem_juntas = []
    for indice_mesh, mesh in enumerate(js.get("meshes", [])):
        for prim in mesh.get("primitives", []):
            if prim.get("mode", 4) != 4:
                r.aviso("Primitiva que não é de triângulos", f"malha {mesh.get('name', indice_mesh)}")
                continue
            attrs = prim.get("attributes", {})
            if "indices" in prim:
                triangulos += acessor(js, prim["indices"])["count"] // 3
            elif "POSITION" in attrs:
                triangulos += acessor(js, attrs["POSITION"])["count"] // 3
            if "POSITION" in attrs:
                a = acessor(js, attrs["POSITION"])
                if "min" in a and "max" in a:
                    for k in range(3):
                        minimos[k] = min(minimos[k], a["min"][k])
                        maximos[k] = max(maximos[k], a["max"][k])
            if indice_mesh in meshes_com_pele and ("JOINTS_0" not in attrs or "WEIGHTS_0" not in attrs):
                sem_juntas.append(mesh.get("name", str(indice_mesh)))

    lo, hi = TRIANGULOS[tipo]
    if lo <= triangulos <= hi:
        r.passa("Triângulos", f"{triangulos}")
    else:
        r.aviso("Triângulos fora da faixa esperada", f"{triangulos} (esperado {lo} a {hi})")

    # --- tamanho e origem
    if minimos[0] is math.inf:
        r.falha("Medidas", "nenhum acessor POSITION com min/max")
    else:
        largura = max(maximos[0] - minimos[0], maximos[2] - minimos[2])
        altura = maximos[1] - minimos[1]
        ok_min, ok_max, f_min, f_max = ALTURA[tipo]
        detalhe = f"altura {altura:.2f} m, largura {largura:.2f} m"
        if ok_min <= altura <= ok_max:
            r.passa("Altura", detalhe)
        elif f_min <= altura <= f_max:
            r.aviso("Altura fora da faixa ideal", f"{detalhe} (ideal {ok_min} a {ok_max} m)")
        else:
            r.falha("Altura absurda (escala errada? o modelo deve estar em metros)", f"{detalhe}")
        ok_l, f_l = LARGURA_MAX[tipo]
        if largura <= ok_l:
            r.passa("Largura", f"{largura:.2f} m")
        elif largura <= f_l:
            r.aviso("Largura acima do ideal", f"{largura:.2f} m (ideal até {ok_l} m)")
        else:
            r.falha("Largura absurda", f"{largura:.2f} m")
        centro_x = (minimos[0] + maximos[0]) / 2
        centro_z = (minimos[2] + maximos[2]) / 2
        if abs(minimos[1]) <= 0.05 and abs(centro_x) <= 0.35 and abs(centro_z) <= 0.35:
            r.passa("Origem no chão, entre os pés")
        else:
            r.aviso("Origem fora do chão/centro", f"base em y={minimos[1]:.2f}, centro em x={centro_x:.2f}, z={centro_z:.2f}")

    # --- escala/rotação pendentes
    suspeitos = [n.get("name", "?") for n in nos if "scale" in n and any(abs(v - 1) > 1e-3 for v in n["scale"])]
    if suspeitos:
        r.aviso("Nós com escala diferente de 1 (aplique as transformações)", ", ".join(suspeitos[:5]))
    else:
        r.passa("Sem escala pendente nos nós")

    # --- esqueleto
    skins = js.get("skins", [])
    if not skins:
        r.falha("Esqueleto", "nenhuma skin (o modelo precisa de esqueleto e pesos)")
        juntas = []
    else:
        juntas = [nomes_nos[j] for j in skins[0].get("joints", [])]
        lo_o, hi_o = OSSOS[tipo]
        if lo_o <= len(juntas) <= hi_o:
            r.passa("Ossos", f"{len(juntas)}")
        else:
            r.aviso("Quantidade de ossos fora da faixa", f"{len(juntas)} (esperado {lo_o} a {hi_o})")
    if sem_juntas:
        r.falha("Malha com pele sem JOINTS_0/WEIGHTS_0", ", ".join(sem_juntas))
    elif skins:
        r.passa("Pesos de pele presentes")

    if tipo == "guerreiro" and juntas:
        minusculas = [j.lower() for j in juntas]

        def lado(nome, esquerda):
            padrao = r"left|(^|[^a-z])l([^a-z]|$)" if esquerda else r"right|(^|[^a-z])r([^a-z]|$)"
            return re.search(padrao, nome) is not None

        faltam = []
        for token in ("hip", "spine", "head"):
            if not any(token in n for n in minusculas):
                faltam.append(token)
        for parte in ("arm", "hand", "leg", "foot"):
            for esquerda, rotulo in ((True, "esquerdo"), (False, "direito")):
                if not any(parte in n and lado(n, esquerda) for n in minusculas):
                    faltam.append(f"{parte} {rotulo}")
        if faltam:
            r.falha("Ossos humanoides ausentes", ", ".join(faltam))
        else:
            r.passa("Ossos humanoides básicos presentes")

    # --- peças separadas
    minusculos = [n.lower() for n in nomes_nos]
    for peca, obrigatoria in PECAS[tipo].items():
        if any(peca in n for n in minusculos):
            r.passa(f"Peça '{peca}'")
        elif obrigatoria:
            r.falha(f"Peça '{peca}' ausente", "precisa ser um nó separado com esse nome")
        else:
            r.aviso(f"Peça '{peca}' ausente", "opcional nesta etapa")

    # --- animações
    anims = {}
    for a in js.get("animations", []):
        duracao = 0.0
        for amostrador in a.get("samplers", []):
            tempos = acessor(js, amostrador["input"])
            if "max" in tempos:
                duracao = max(duracao, tempos["max"][0])
            elif binario:
                duracao = max(duracao, max(v[0] for v in ler_escalares(js, binario, amostrador["input"])))
        anims[a.get("name", "")] = duracao
    for nome, (minimo, maximo) in ANIMACOES[tipo].items():
        if nome not in anims:
            r.falha(f"Animação '{nome}' ausente", "o nome precisa ser exatamente este")
        elif minimo <= anims[nome] <= maximo:
            r.passa(f"Animação '{nome}'", f"{anims[nome]:.2f} s")
        else:
            r.aviso(f"Animação '{nome}' com duração fora da faixa", f"{anims[nome]:.2f} s (esperado {minimo} a {maximo} s)")
    extras = sorted(set(anims) - set(ANIMACOES[tipo]) - OPCIONAIS[tipo])
    if extras:
        r.aviso("Animações extras (serão ignoradas)", ", ".join(extras))

    # --- materiais e texturas
    materiais = js.get("materials", [])
    if len(materiais) <= 3:
        r.passa("Materiais", f"{len(materiais)}")
    else:
        r.aviso("Mais de 3 materiais", f"{len(materiais)}")
    for i, imagem in enumerate(js.get("images", [])):
        if "bufferView" in imagem and binario:
            vista = js["bufferViews"][imagem["bufferView"]]
            inicio = vista.get("byteOffset", 0)
            dim = dimensoes_imagem(binario[inicio:inicio + min(vista["byteLength"], 200_000)])
            if dim is None:
                r.aviso(f"Textura {i}", "não consegui ler as dimensões")
            elif max(dim) > 4096:
                r.falha(f"Textura {i} grande demais", f"{dim[0]}x{dim[1]} (máximo 2048)")
            elif max(dim) > 2048:
                r.aviso(f"Textura {i} acima de 2048", f"{dim[0]}x{dim[1]}")
            else:
                r.passa(f"Textura {i}", f"{dim[0]}x{dim[1]}")
        else:
            r.aviso(f"Textura {i} não está embutida", "use .glb com texturas embutidas")
    return r


# ------------------------------------------------------------------ autoteste com modelos sintéticos
def montar_glb(js, binario):
    dados_json = json.dumps(js, separators=(",", ":")).encode("utf-8")
    dados_json += b" " * (-len(dados_json) % 4)
    binario += b"\x00" * (-len(binario) % 4)
    total = 12 + 8 + len(dados_json) + 8 + len(binario)
    return (struct.pack("<4sII", b"glTF", 2, total) + struct.pack("<II", len(dados_json), 0x4E4F534A) + dados_json
            + struct.pack("<II", len(binario), 0x004E4942) + binario)


def modelo_sintetico(tipo, quebrado=False):
    nomes_ossos = {
        "guerreiro": ["Hips", "Spine", "Chest", "Neck", "Head", "UpperArm_L", "LowerArm_L", "Hand_L", "UpperArm_R",
                      "LowerArm_R", "Hand_R", "UpperLeg_L", "LowerLeg_L", "Foot_L", "UpperLeg_R", "LowerLeg_R", "Foot_R",
                      "Shoulder_L"],
        "carrapato": [f"bone_{i}" for i in range(12)],
    }[tipo]
    largura, altura = (0.6, 1.9) if tipo == "guerreiro" else (1.9, 1.2)
    if quebrado:
        altura = 19.0          # escala errada (centímetros)
    b = bytearray()
    vistas, acessores = [], []

    def vista(bytes_):
        deslocamento = len(b)
        b.extend(bytes_)
        b.extend(b"\x00" * (-len(b) % 4))
        vistas.append({"buffer": 0, "byteOffset": deslocamento, "byteLength": len(bytes_)})
        return len(vistas) - 1

    # caixa (8 vértices, 12 triângulos), muitos triângulos simulados repetindo índices não é necessário
    x, z = largura / 2, largura / 4
    pos = [(-x, 0, -z), (x, 0, -z), (x, 0, z), (-x, 0, z), (-x, altura, -z), (x, altura, -z), (x, altura, z), (-x, altura, z)]
    posicoes = b"".join(struct.pack("<3f", *p) for p in pos)
    acessores.append({"bufferView": vista(posicoes), "componentType": 5126, "count": 8, "type": "VEC3",
                      "min": [-x, 0, -z], "max": [x, altura, z]})
    indices = [0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0]
    acessores.append({"bufferView": vista(struct.pack("<36H", *indices)), "componentType": 5123, "count": 36, "type": "SCALAR"})
    atributos = {"POSITION": 0}
    if not quebrado:
        juntas = b"".join(struct.pack("<4B", 0, 0, 0, 0) for _ in range(8))
        pesos = b"".join(struct.pack("<4f", 1, 0, 0, 0) for _ in range(8))
        acessores.append({"bufferView": vista(juntas), "componentType": 5121, "count": 8, "type": "VEC4"})
        acessores.append({"bufferView": vista(pesos), "componentType": 5126, "count": 8, "type": "VEC4"})
        atributos.update({"JOINTS_0": 2, "WEIGHTS_0": 3})

    nos = [{"name": n} for n in nomes_ossos]
    corpo = {"name": "Guerreiro_Corpo" if tipo == "guerreiro" else "Carrapato_Corpo", "mesh": 0}
    if not quebrado:
        corpo["skin"] = 0
    nos.append(corpo)
    indice_corpo = len(nos) - 1
    extras = []
    if tipo == "guerreiro":
        for peca in (["Espada", "Escudo", "Elmo"] if not quebrado else ["Elmo"]):
            nos.append({"name": peca})
            extras.append(len(nos) - 1)

    animacoes = []
    durs = {n: (a + b_) / 2 for n, (a, b_) in ANIMACOES[tipo].items()}
    if quebrado:
        durs.pop("run", None) if tipo == "guerreiro" else durs.pop("walk", None)
        durs["idle"] = 9.0
    for nome, dur in durs.items():
        entrada = struct.pack("<2f", 0.0, dur)
        saida = struct.pack("<6f", 0, 0, 0, 0, 0, 0)
        i_in = len(acessores)
        acessores.append({"bufferView": vista(entrada), "componentType": 5126, "count": 2, "type": "SCALAR", "min": [0.0], "max": [dur]})
        acessores.append({"bufferView": vista(saida), "componentType": 5126, "count": 2, "type": "VEC3"})
        animacoes.append({"name": nome, "samplers": [{"input": i_in, "output": i_in + 1}],
                          "channels": [{"sampler": 0, "target": {"node": 0, "path": "translation"}}]})

    js = {
        "asset": {"version": "2.0", "generator": "autoteste"},
        "scene": 0, "scenes": [{"nodes": [indice_corpo] + extras}],
        "nodes": nos, "buffers": [{"byteLength": len(b)}], "bufferViews": vistas, "accessors": acessores,
        "meshes": [{"name": "corpo", "primitives": [{"attributes": atributos, "indices": 1}]}],
        "materials": [{"name": "m0"}], "animations": animacoes,
    }
    if not quebrado:
        js["skins"] = [{"joints": list(range(len(nomes_ossos)))}]
    return montar_glb(js, bytes(b))


def autoteste():
    ok = True
    for tipo in ("guerreiro", "carrapato"):
        bom = conferir(*ler_glb(modelo_sintetico(tipo)), tipo)
        ruim = conferir(*ler_glb(modelo_sintetico(tipo, quebrado=True)), tipo)
        # o sintético só tem 12 triângulos: esse aviso é esperado; não pode haver FALHA no bom
        esperado_bom = bom.falhas == 0
        esperado_ruim = ruim.falhas >= 3
        print(f"[{tipo}] modelo correto: {bom.falhas} falhas (esperado 0) -> {'ok' if esperado_bom else 'ERRO'}")
        print(f"[{tipo}] modelo quebrado: {ruim.falhas} falhas (esperado >= 3) -> {'ok' if esperado_ruim else 'ERRO'}")
        ok = ok and esperado_bom and esperado_ruim
    try:
        ler_glb(b"isto nao e um glb")
        ok = False
        print("ERRO: arquivo inválido deveria ser recusado")
    except ValueError as e:
        print(f"arquivo inválido recusado corretamente: {e}")
    print("AUTOTESTE:", "PASSOU" if ok else "FALHOU")
    return 0 if ok else 1


def main(argv):
    if "--autoteste" in argv:
        return autoteste()
    if len(argv) < 2 or "--tipo" not in argv:
        print(__doc__)
        return 2
    tipo = argv[argv.index("--tipo") + 1]
    if tipo not in ANIMACOES:
        print("--tipo deve ser 'guerreiro' ou 'carrapato'")
        return 2
    try:
        js, binario = ler_glb(argv[1])
    except (OSError, ValueError) as e:
        print(f"[FALHA] Não consegui ler o arquivo: {e}")
        return 1
    relatorio = conferir(js, binario, tipo)
    relatorio.imprimir()
    return 1 if relatorio.falhas else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
