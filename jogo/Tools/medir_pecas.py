#!/usr/bin/env python3
"""
13/11 22:26 (regra do dono: "nada atravessa, a não ser fantasma ou água — quadro de recados, bancada, várias coisas sem
colisão"): mede o "pé" (contorno no chão) de TODOS os modelos .glb e gera Shared/World/PecasMedidas.cs, que o servidor usa
para dar colisão do tamanho certo a cada peça de cenário (gira e escala com ela). Usa o *_colisao.glb quando existe.

Também lê o cliente para saber qual modelo desenha cada tipo de peça (case "x" when G(node, "arquivo") e "x" => "arquivo").
Rodar quando entrarem modelos novos:  python3 Tools/medir_pecas.py
"""
import glob, json, os, re, sys
import numpy as np
import trimesh

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MOD = os.path.join(RAIZ, "Client", "Assets", "Models")
OUT = os.path.join(RAIZ, "Shared", "World", "PecasMedidas.cs")


def pe(caminho):
    s = trimesh.load(caminho, force="scene")
    pts = []
    for nome in s.graph.nodes_geometry:
        T, g = s.graph[nome]
        geo = s.geometry[g]
        if not hasattr(geo, "vertices") or len(geo.vertices) == 0:
            continue
        v = trimesh.transformations.transform_points(geo.vertices, T)
        pts.append(v)
    if not pts:
        return None
    v = np.vstack(pts)
    v = v[(v[:, 1] > 0.08) & (v[:, 1] < 2.2)]   # o que bate na altura do corpo (tira chão e copa)
    if len(v) < 4:
        return None
    x0, x1 = np.percentile(v[:, 0], [1, 99]); z0, z1 = np.percentile(v[:, 2], [1, 99])
    return [round(float(x0), 2), round(float(x1), 2), round(float(z0), 2), round(float(z1), 2)]


medidas = {}
for f in glob.glob(os.path.join(MOD, "**", "*.glb"), recursive=True):
    rel = os.path.relpath(f, MOD)[:-4].replace(os.sep, "/")
    if rel.endswith("_colisao"):
        continue
    col = os.path.join(MOD, rel + "_colisao.glb")
    try:
        m = pe(col) if os.path.exists(col) else None
        m = m or pe(f)
    except Exception as e:
        print("não li", rel, e, file=sys.stderr); m = None
    if m:
        medidas[rel] = m

# tipo da peça -> modelo (o que o cliente desenha)
codigo = ""
for f in glob.glob(os.path.join(RAIZ, "Client", "**", "*.cs"), recursive=True):
    codigo += open(f, encoding="utf-8").read()
tipo = {}
for kinds, arq in re.findall(r'case ((?:"[a-z0-9_]+"(?: or )?)+) when G\(node, "([a-z0-9_/]+)"', codigo):
    for k in re.findall(r'"([a-z0-9_]+)"', kinds):
        tipo.setdefault(k, arq)
for k, arq in re.findall(r'"([a-z0-9_]+)" => "([a-z0-9_/]+)"', codigo):
    if arq in medidas:
        tipo.setdefault(k, arq)
por_base = {}
for rel in medidas:
    por_base.setdefault(rel.split("/")[-1], rel)
kinds = set()
for f in glob.glob(os.path.join(RAIZ, "Content", "map_*.json")):
    m = json.load(open(f, encoding="utf-8"))
    kinds |= {p["kind"] for p in m.get("props", [])} | {o["kind"] for o in m.get("objects", [])}
kinds |= {"bancada", "forja", "caldeirao", "panela", "bau_armazem"}
saida = {"glb:" + rel: m for rel, m in medidas.items()}   # qualquer modelo posto pelo editor ("glb:pasta/nome")
for k in sorted(kinds):
    if k.startswith("glb:"):
        rel = k[4:]
        if rel in medidas:
            saida[k] = medidas[rel]
        continue
    rel = tipo.get(k)
    if rel not in medidas:
        rel = next((por_base[c] for c in (k, "cenario_" + k, "edificio_" + k, "marco_" + k) if c in por_base), None)
    if rel:
        saida[k] = medidas[rel]

linhas = [f'        ["{k}"] = ({a}, {b}, {c}, {d}),' for k, (a, b, c, d) in sorted(saida.items())]
open(OUT, "w", encoding="utf-8").write('''// GERADO por Tools/medir_pecas.py — não edite à mão (rode o script de novo quando entrarem modelos novos).
using System.Collections.Generic;

namespace CinzasDaMare.Shared.World;

/// <summary>13/11: pé (contorno no chão, modelo sem giro, em metros) de cada tipo de peça de cenário: X0..X1 na largura, Z0..Z1
/// na profundidade (Z do modelo = Y do mapa). Medido dos .glb na altura do corpo (0,08–2,2 m).</summary>
public static class PecasMedidas
{
    public static readonly Dictionary<string, (double X0, double X1, double Z0, double Z1)> Pe = new()
    {
''' + "\n".join(linhas) + '''
    };
}
''')
print(len(medidas), "modelos medidos;", len(saida), "tipos de peça com medida →", os.path.relpath(OUT, RAIZ))
