#!/usr/bin/env python3
"""
13/11 ("tudo que é adicionado no jogo deve ir pra paleta do editor automaticamente"): lê os desenhos de peça do cliente
(MapView.AddProp, MapKit.TryBuild, KitTapa.Prop, PecasDaVila.Build) e escreve Client/PecasConhecidas.cs com todos os
tipos que o jogo sabe desenhar. Roda a cada compilação (build); o editor junta esta lista com as peças usadas nos mapas.
"""
import os, re
R = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(R, "Client")

def metodo(txt, assinatura):
    i = txt.find(assinatura)
    if i < 0: return ""
    j = txt.find("\n    }\n", i)
    return txt[i:j if j > 0 else len(txt)]

fontes = []
mv = open(os.path.join(C, "MapView.cs"), encoding="utf-8").read()
fontes.append(metodo(mv, "private void AddProp(MapProp p)"))
fontes.append(metodo(mv, "private void LotTwoBuilding("))
fontes.append(metodo(open(os.path.join(C, "MapKit.cs"), encoding="utf-8").read(), "public static bool TryBuild("))
for f in ("KitTapa.cs", "PecasDaVila.cs"):
    p = os.path.join(C, "Ambiente", f)
    if os.path.exists(p): fontes.append(open(p, encoding="utf-8").read())
tipos = set()
for t in fontes:
    for linha in t.splitlines():
        if "case " not in linha and not re.search(r'^\s*or "', linha) and "=>" not in linha: continue
        for k in re.findall(r'(?:case|or|\||,|\()\s*"([a-z][a-z0-9_]{2,})"', linha):
            tipos.add(k)
ruins = {"cidade", "praia", "neve", "lava", "fantasma", "ceu", "mata", "brejo", "lua", "vale", "canion", "gruta", "abismo", "aguia"}
tipos = sorted(t for t in tipos if t not in ruins and not t.endswith("_") and not t.startswith(("cenario_", "edificio_", "costa_", "arma_")))
corpo = ",\n        ".join(", ".join(f'"{t}"' for t in tipos[i:i + 8]) for i in range(0, len(tipos), 8))
open(os.path.join(C, "PecasConhecidas.cs"), "w", encoding="utf-8").write(f"""// GERADO por Tools/listar_pecas.py a cada compilação — não editar à mão.
namespace CinzasDaMare.Client;

/// <summary>Todos os tipos de peça de cenário que o cliente sabe desenhar (vão para a paleta do editor).</summary>
public static class PecasConhecidas
{{
    public static readonly string[] Todas =
    {{
        {corpo}
    }};
}}
""")
print(len(tipos), "tipos de peça")
