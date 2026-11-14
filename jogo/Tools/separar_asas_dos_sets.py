#!/usr/bin/env python3
"""
14/11 (dono: "alguns sets estão com asas — se usar asa, fica com 2 asas. Tira as asas dos sets e cria asas próprias").
Os 6 conjuntos lendários do trabalhador que vieram com asas presas no peitoral (Cavaleiro Sagrado, Dragão de Fogo, Gelo
Eterno, Celestial, Demoníaco, Anjo Caído): separa as asas (peças rígidas na Spine2, atrás das costas ou para os lados,
acima da cintura — penas, dedos e membrana) num arquivo próprio arm_<set>_asas.glb e tira do peitoral do conjunto.
Só reescreve a lista de triângulos (os vértices ficam; o resto do arquivo é o mesmo). Idempotente: o original fica em
backups/sets_com_asas/ e é sempre dele que se parte.
"""
import json, os, shutil, struct, sys
import numpy as np

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ARM = os.path.join(RAIZ, "Client", "Assets", "Models", "armaduras")
BK = os.path.join(RAIZ, "backups", "sets_com_asas")
SETS = ["cavaleiro_sagrado", "dragao_fogo", "gelo_eterno", "celestial", "demoniaco", "anjo_caido"]
CT = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8}
NC = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def load(p):
    b = open(p, "rb").read(); l = struct.unpack("<I", b[12:16])[0]; j = json.loads(b[20:20 + l])
    off = 20 + l; bl = struct.unpack("<I", b[off:off + 4])[0]
    return j, bytearray(b[off + 8:off + 8 + bl])


def acc(j, bin_, i):
    a = j["accessors"][i]; bv = j["bufferViews"][a["bufferView"]]
    dt = CT[a["componentType"]]; n = NC[a["type"]]; start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    stride = bv.get("byteStride", 0)
    if stride and stride != np.dtype(dt).itemsize * n:
        raw = np.frombuffer(bytes(bin_), dtype=np.uint8, count=stride * a["count"], offset=start).reshape(a["count"], stride)
        return raw[:, :np.dtype(dt).itemsize * n].copy().view(dt).reshape(a["count"], n)
    arr = np.frombuffer(bytes(bin_), dtype=dt, count=a["count"] * n, offset=start)
    return arr.reshape(a["count"], n) if n > 1 else arr


def componentes(idx, nv):
    par = np.arange(nv)
    def f(x):
        while par[x] != x:
            par[x] = par[par[x]]; x = par[x]
        return x
    for a, b, c in idx.reshape(-1, 3):
        ra = f(a); par[f(b)] = ra; par[f(c)] = ra
    return np.array([f(i) for i in range(nv)])


def asas_do_peitoral(j, bin_):
    p = [m for m in j["meshes"] if m["name"] == "peitoral"][0]["primitives"][0]
    pos = acc(j, bin_, p["attributes"]["POSITION"]); idx = acc(j, bin_, p["indices"]).astype(np.int64)
    jo = acc(j, bin_, p["attributes"]["JOINTS_0"]).astype(int); we = acc(j, bin_, p["attributes"]["WEIGHTS_0"]).astype(float)
    if we.max() > 1.5: we = we / 255.0
    nomes = [j["nodes"][n]["name"] for n in j["skins"][0]["joints"]]
    sp2 = nomes.index("mixamorig:Spine2")
    rigido = (jo[np.arange(len(jo)), we.argmax(1)] == sp2) & (we.max(1) > 0.98)
    raiz = componentes(idx, len(pos)); tri = idx.reshape(-1, 3); tr = raiz[tri[:, 0]]
    sao = set()
    for r in np.unique(tr):
        vm = raiz == r; c = pos[vm].mean(0)
        if rigido[vm].all() and c[1] > 1.0 and (c[2] < -0.16 or abs(c[0]) > 0.45) and not (abs(c[0]) < 0.12 and c[1] > 1.6):
            sao.add(r)
    return p, tri, np.array([r in sao for r in tr])


def gravar(j, bin_, p, tri, saida):
    j = json.loads(json.dumps(j)); bin_ = bytearray(bin_)
    while len(bin_) % 4: bin_.append(0)
    dados = tri.astype(np.uint32).reshape(-1).tobytes()
    off = len(bin_); bin_ += dados
    j["bufferViews"].append({"buffer": 0, "byteOffset": off, "byteLength": len(dados), "target": 34963})
    j["accessors"].append({"bufferView": len(j["bufferViews"]) - 1, "componentType": 5125, "count": int(tri.size), "type": "SCALAR"})
    for m in j["meshes"]:
        if m["name"] == "peitoral": m["primitives"][0]["indices"] = len(j["accessors"]) - 1
    j["buffers"][0]["byteLength"] = len(bin_)
    js = json.dumps(j, separators=(",", ":")).encode()
    while len(js) % 4: js += b" "
    while len(bin_) % 4: bin_.append(0)
    total = 12 + 8 + len(js) + 8 + len(bin_)
    with open(saida, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, total))
        f.write(struct.pack("<I", len(js)) + b"JSON" + js)
        f.write(struct.pack("<I", len(bin_)) + b"BIN\x00" + bytes(bin_))


def main():
    os.makedirs(BK, exist_ok=True)
    for s in SETS:
        arq = os.path.join(ARM, f"arm_{s}.glb"); orig = os.path.join(BK, f"arm_{s}.glb")
        if not os.path.exists(orig): shutil.copy2(arq, orig)
        j, b = load(orig)
        p, tri, asa = asas_do_peitoral(j, b)
        gravar(j, b, p, tri[~asa], arq)
        gravar(j, b, p, tri[asa], os.path.join(ARM, f"arm_{s}_asas.glb"))
        print(f"{s}: {int(asa.sum())} triângulos de asa separados")


main()
