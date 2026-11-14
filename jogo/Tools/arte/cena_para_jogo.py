"""Converte uma cena do render_scene.py (camera olhando -Y do Blender) para o mapa do jogo (camera yaw 0 = olhando norte/-Z).
mapa_x = X0 - bx ; mapa_y = Y0 + by ; yaw_jogo = yaw + 180 ; escala Blender (sx,sy,sz) -> Godot (sx,sz,sy).
Uso: python3 cena_para_jogo.py cena.json X0 Y0 saida_dir"""
import sys, json, os, math, collections
cfg = json.load(open(sys.argv[1])); X0, Y0 = float(sys.argv[2]), float(sys.argv[3]); out = sys.argv[4]
pec = []; fundo = []; criaturas = []
for it in cfg["itens"]:
    nome = os.path.basename(it["glb"])[:-4]
    s = it.get("sxyz", [it.get("s", 1)] * 3)
    e = dict(id=nome, x=round(X0 - it["x"], 2), y=round(Y0 + it["y"], 2), yaw=round((it.get("yaw", 0) + 180) % 360, 1), esc=[s[0], s[2], s[1]])
    if "/criaturas/" in it["glb"] or "/humanoides/" in it["glb"]: criaturas.append(e)
    elif "penhasco" in nome or "horizonte" in nome: fundo.append(e)
    else: pec.append(e)
json.dump(dict(origem=[X0, Y0], pecas=pec, fundo_opcional=fundo, criaturas_referencia=criaturas), open(f"{out}/cena_print_costa.json", "w"), indent=1, ensure_ascii=False)
def cs(e, origem_rel=True):
    return f'        ("costa/{e["id"]}", {e["x"]-X0:.2f}f, {e["y"]-Y0:.2f}f, {e["yaw"]:.1f}f, {e["esc"][0]:.2f}f, {e["esc"][1]:.2f}f, {e["esc"][2]:.2f}f),'
C = f'''using Godot;
using CinzasDaMare.Client;

namespace CinzasDaMare.Client.Ambiente;

/// <summary>
/// Cena da prévia "costa_depois" (lago, rochas, capim, arbustos, flores, coqueiros/pinheiros), convertida para o mapa.
/// Coordenadas RELATIVAS à origem; origem padrão = ({X0}, {Y0}) no mapa da Costa, que põe o lago em cima do "lago" do map_costa (-20, 37).
/// Só visual: a colisão continua vindo dos obstáculos do mapa (ver FICHA, lista para o gerar_regioes.py).
/// Uso: CenaPrintCosta.Montar(mundo, new Vector2({X0}f, {Y0}f));   // mundo = Node3D raiz do mapa, P(x,y,h) = (x,h,y)
/// </summary>
public static class CenaPrintCosta
{{
    // (arquivo, dx, dy do mapa, yaw graus Godot, escala x, y, z Godot)
    private static readonly (string f, float dx, float dy, float yaw, float sx, float sy, float sz)[] Pecas =
    {{
{chr(10).join(cs(e) for e in pec)}
    }};

    // Paredão de penhasco do fundo do print: só use se não houver penhasco do mapa atrás (aqui cai em y = {Y0-30:.0f}).
    private static readonly (string f, float dx, float dy, float yaw, float sx, float sy, float sz)[] Fundo =
    {{
{chr(10).join(cs(e) for e in fundo if "penhasco" in e["id"])}
    }};

    public static Node3D Montar(Node3D mundo, Vector2 origem, bool comParedao = false)
    {{
        var raiz = new Node3D {{ Name = "CenaPrintCosta" }};
        mundo.AddChild(raiz);
        void Por((string f, float dx, float dy, float yaw, float sx, float sy, float sz) p)
        {{
            var n = new Node3D
            {{
                Position = new Vector3(origem.X + p.dx, 0f, origem.Y + p.dy),
                RotationDegrees = new Vector3(0f, p.yaw, 0f),
                Scale = new Vector3(p.sx, p.sy, p.sz),
            }};
            raiz.AddChild(n);
            if (!MapKit.Glb(n, p.f, Vector3.Zero, 0f, 1f)) n.QueueFree();
        }}
        foreach (var p in Pecas) Por(p);
        if (comParedao) foreach (var p in Fundo) Por(p);
        return raiz;
    }}
}}
'''
open(f"{out}/CenaPrintCosta.cs", "w").write(C)
cnt = collections.Counter(e["id"] for e in pec); print(cnt, len(pec), len(fundo), len(criaturas))
