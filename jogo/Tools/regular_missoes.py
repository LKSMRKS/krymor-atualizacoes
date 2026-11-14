#!/usr/bin/env python3
"""
13/11 (pedido do dono: "vamos regular as missões — pode ter 'mate' sim, mas não tanto quanto tem"): quase metade das
missões era "mate X". Este script troca metade das "mate X" de história (não as repetíveis, diárias nem do quadro) por:
  * DEFESA ("defend"): segure o ponto onde os bichos ficam — 3 ondas deles chegam (sair do ponto cancela a onda);
  * CAÇADA ("hunt"): ache o rastro e derrube o líder do bando (uma presa mais forte que aparece perto).
O ponto é o centro de onde aquele bicho nasce no mapa. Recompensas ficam iguais (a missão nova dá o mesmo ou mais
trabalho). Idempotente: o que já foi trocado leva "regulada"; rodar de novo não mexe mais.
Rodar DEPOIS de todos os geradores de missões (e antes dos testes).
"""
import json, os, glob, hashlib, math

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")

def main():
    mapas = {json.load(open(f, encoding="utf-8"))["id"]: json.load(open(f, encoding="utf-8")) for f in glob.glob(os.path.join(C, "map_*.json"))}
    onde = {}   # monstro -> {mapa: [(x, y)]}
    for mid, m in mapas.items():
        if m.get("instanced") or m.get("kind") == "dungeon": continue
        for s in m.get("spawns", []): onde.setdefault(s["monster"], {}).setdefault(mid, []).append((s["x"], s["y"]))
    total = kill = trocadas = 0
    for f in sorted(glob.glob(os.path.join(C, "quests*.json"))):
        qs = json.load(open(f, encoding="utf-8")); mudou = False
        for q in qs:
            total += 1
            if q.get("regulada"): trocadas += 1; continue
            if q["kind"] != "kill": continue
            kill += 1
            if q.get("repeatable") or q.get("daily") or q.get("board") or len(set(q.get("targets") or [])) != 1: continue   # vários bichos diferentes = já é variado
            # metade, escolhida pelo id (sempre a mesma)
            h = int(hashlib.md5(q["id"].encode()).hexdigest(), 16)
            if h % 2: continue
            alvo = q["targets"][0]
            mapa = q.get("map") or (next(iter(onde.get(alvo, {})), None) if len(onde.get(alvo, {})) == 1 else None)
            pts = onde.get(alvo, {}).get(mapa) if mapa else None
            if not pts: continue
            cx = round(sum(p[0] for p in pts) / len(pts), 1); cy = round(sum(p[1] for p in pts) / len(pts), 1)
            nome = q["name"]
            if (h >> 1) % 2 == 0:
                q["kind"] = "defend"; q["count"] = 3; q["radius"] = 6
                q["description"] = q["description"].rstrip() + " (Eles vêm em bando: fique no ponto e segure três ondas.)"
                q["hint"] = f"Vá até onde eles ficam e segure o ponto: 3 ondas. Sair para longe cancela a onda."
            else:
                q["kind"] = "hunt"; q["count"] = 1; q["targets"] = [alvo]; q["radius"] = 20
                q["description"] = q["description"].rstrip() + " (Não adianta matar um por um: ache o rastro do líder do bando e derrube ele.)"
                q["hint"] = f"Chegue perto de onde eles ficam: o rastro aparece e o líder do bando surge por perto."
            q["map"] = mapa; q["x"] = cx; q["y"] = cy
            q["regulada"] = "kill→" + q["kind"]
            trocadas += 1; kill -= 1; mudou = True
        if mudou: json.dump(qs, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print(f"missões: {total} · 'mate X' agora {kill} ({kill * 100 // max(1, total)}%) · trocadas por defesa/caçada: {trocadas}")

main()
