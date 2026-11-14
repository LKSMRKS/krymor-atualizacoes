#!/usr/bin/env python3
"""
14/11 tarde (pedido do dono: "confere um por um, precisa estar tudo perfeitamente alinhado — nada desbalanceado, tudo
alinhado com a progressão do jogo matematicamente"; "os sets que eu fiz pelo Meshy: os últimos são os últimos mesmo;
coloca os pica que você e o trabalhador fizeram junto ou abaixo").

Uma régua de FORÇA para cada família, contra a curva de 100% do nível (a mesma do Shared/Content/PisoDeNivel.cs):
  arma      (2,6·L + 8) × intervalo ÷ projéteis   (cajado/talismã: 2,2·L + 8)
  armadura  pesada 14,3·L+30 · média 9,7·L+40 · leve 6,7·L+30, × fração da peça (peito 1 · calça 0,5 · bota 0,4 ·
            elmo 0,35 · luva 0,33)
  escudo    0,6 × (14,3·L + 30)
Família → força:
  loja / região / história comum ............... 1,00
  arsenal: enferrujada 0,92 · comum 1,00 · refinada 1,06 · excelente 1,12
  conjunto do Meshy (peças com "set") ......... 1,15  + as linhas da variação V1–V15 por cima (V10+ = Excelente)
  lendário (trabalhador, Caos, histórias) ..... 1,15  + 1 linha (×1,2). Fica JUNTO dos conjuntos do Meshy; como o Meshy
                                                 ganha até 8 linhas na variação, o Meshy V7+ passa o lendário.
  Lendários do trabalhador saem de 45–60 para 42–50: o fim do jogo continua sendo o dos conjuntos do dono (nível 50).
Acessórios (mesma ideia, curva própria):
  asas antigas (wings_*): poder 0,46%·L, velocidade 0,4%·L + 1%, armadura 1,7·L, vida 5,4·L (Serpente, crônica: ×1,15)
  capas: armadura 1,7·L + 6, vida 3,1·L + 10 × (básica 0,8 · normal 1,0 · rara de chefe 1,1)
  skins (bracelete com armadura/vida): armadura 0,46·L + 3, vida 3,4·L + 20 × (de chefe 1,1)
As 25 asas da coleção já saem na régua pelo gerar_colecao_dono.py (comum 0,75 · incomum 0,85 · rara 1 · épica 1,1 ·
lendária 1,2). Itens abaixo do nível 3 (os iniciais) ficam como estão. Idempotente. Rodar antes do regular_economia.
"""
import json, os, glob

C = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Content")
FRACAO = {"armor": 1.0, "helmet": 0.35, "pants": 0.5, "gloves": 0.33, "boots": 0.4}
ARSENAL = {"enferrujada": 0.92, "comum": 1.0, "refinada": 1.06, "excelente": 1.12}
NIVEL_LENDARIO = {   # trabalhador: 45/50/55/60 → 42/45/48/50 (junto dos últimos conjuntos do dono, nível 50)
    "set_arm_runico_ancestral": 42, "set_arm_pedra": 42, "set_arm_floresta": 42,
    "set_arm_cavaleiro_sagrado": 45, "set_arm_gelo_eterno": 45, "set_arm_necromante": 45, "set_arm_abissal_profundo": 45,
    "set_arm_dragao_fogo": 48, "set_arm_abismo": 48, "set_arm_faraonico": 48,
    "set_arm_celestial": 50, "set_arm_demoniaco": 50, "set_arm_tempo": 50, "set_arm_vazio": 50, "set_arm_anjo_caido": 50,
}
NIVEL_ARMA_LEND = {"sagrado": 42, "gelo": 45, "fogo": 48, "vazio": 50, "celestial": 50}
CAPA_BASICA = {"capa_viajante", "capa_rubra"}
CAPA_RARA = {"capa_amaldicoada", "capa_lava", "capa_dragao", "capa_celestial", "capa_morte", "capa_luz", "capa_tempo",
             "capa_brasa_viva", "capa_abismo", "capa_dourada"}
SKIN_DE_CHEFE = {"bracelete_pedra", "bracelete_samurai", "bracelete_sem_cabeca", "bracelete_cristal", "bracelete_ouro",
                 "bracelete_tubarao", "bracelete_lava"}


def lendario(d):
    return d.get("quality") == "lendario" or d["id"].startswith(("lendaria_", "arma_lend_", "legend_"))


def forca_arma(d):
    if lendario(d): return 1.15
    if d["id"].startswith("arsenal_"):
        for k, v in ARSENAL.items():
            if d["id"].endswith("_" + k): return v
    return 1.0


def peso(d):
    cls = d.get("allowed_classes") or []
    if d.get("subtype") == "plate" or cls == ["guerreiro"]: return 14.3, 30
    if d.get("subtype") in ("robe", "cloth") or (cls and all(c in ("mago", "bruxa") for c in cls)): return 6.7, 30
    return 9.7, 40


def alinhar(d):
    """Devolve a lista de mudanças (texto) feitas no item."""
    s = d.get("base_stats") or {}
    cat = d.get("category"); i = d["id"]; mud = []
    if d.get("set") in NIVEL_LENDARIO and d["min_level"] != NIVEL_LENDARIO[d["set"]]:
        mud.append(f"nível {d['min_level']}→{NIVEL_LENDARIO[d['set']]}"); d["min_level"] = NIVEL_LENDARIO[d["set"]]
    if i.startswith("arma_lend_"):
        el = i.rsplit("_", 1)[-1]
        if el in NIVEL_ARMA_LEND and d["min_level"] != NIVEL_ARMA_LEND[el]:
            mud.append(f"nível {d['min_level']}→{NIVEL_ARMA_LEND[el]}"); d["min_level"] = NIVEL_ARMA_LEND[el]
    if i.startswith("arma_lend_manopla_") and d.get("allowed_classes") != ["invocador"]:
        # 14/11 (dono): a manopla é a arma do INVOCADOR — classe que o dono ainda vai criar (ninguém equipa até lá)
        mud.append("classe → invocador"); d["allowed_classes"] = ["invocador"]
        d["summary"] = d.get("summary", "").replace("Só para Guerreiro", "Só para Invocador")
    L = d.get("min_level", 1)
    if L < 3 or "transform" in (d.get("subtype") or ""): return mud

    def por(chave, novo):
        novo = round(novo) if chave in ("armor", "max_health", "weapon_power") else round(novo, 3)
        if s.get(chave) != novo:
            mud.append(f"{chave} {s.get(chave)}→{novo}"); s[chave] = novo

    if cat == "weapon" and "weapon_power" in s:
        iv = s.get("attack_interval_ms", 1000) or 1000
        conj = d.get("subtype") in ("staff", "talisman", "wand", "orb")
        alvo = ((2.2 if conj else 2.6) * L + 8) * iv / 1000 / max(1, s.get("projectile_count", 1))
        por("weapon_power", alvo * forca_arma(d))
    elif cat in FRACAO and "armor" in s:
        k, b = peso(d)
        f = 1.15 if (lendario(d) or d.get("set")) else 1.0
        por("armor", (k * L + b) * FRACAO[cat] * f)
    elif cat == "offhand" and "armor" in s:
        por("armor", 0.6 * (14.3 * L + 30) * (1.15 if lendario(d) else 1.0))
    elif cat == "wings" and i.startswith("wings_"):
        t = 1.15 if i == "wings_serpente" else 1.0
        por("power_pct", min(0.30, 0.0046 * L * t)); por("move_speed_pct", min(0.22, (0.004 * L + 0.01) * t))
        por("armor", 1.7 * L * t); por("max_health", 5.4 * L * t)
    elif cat == "cape" and "armor" in s:
        t = 0.8 if i in CAPA_BASICA else 1.1 if i in CAPA_RARA else 1.0
        por("armor", (1.7 * L + 6) * t); por("max_health", (3.1 * L + 10) * t)
    elif cat == "bracelet" and "armor" in s and "max_health" in s:
        t = 1.1 if i in SKIN_DE_CHEFE else 1.0
        por("armor", (0.46 * L + 3) * t); por("max_health", (3.4 * L + 20) * t)
    d["base_stats"] = s
    return mud


def main():
    total = 0
    linhas = []
    for f in sorted(glob.glob(os.path.join(C, "items*.json"))):
        its = json.load(open(f, encoding="utf-8"))
        if not isinstance(its, list): continue
        mudou = False
        for d in its:
            m = alinhar(d)
            if m:
                mudou = True; total += 1
                linhas.append(f"{d['id']}: " + ", ".join(m))
        if mudou:
            json.dump(its, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    # o Titã solta o topo dos lendários (nível 48–50 depois do realinhamento)
    lend = json.load(open(os.path.join(C, "items_lendarios.json"), encoding="utf-8"))
    lt = os.path.join(C, "loot_tita.json")
    if os.path.exists(lt):
        t = json.load(open(lt, encoding="utf-8"))
        t[0]["entries"][0]["pick"] = sorted(i["id"] for i in lend if i["category"] == "weapon" and i["min_level"] >= 48)
        t[0]["entries"][1]["pick"] = sorted(i["id"] for i in lend if i["category"] != "weapon" and i["min_level"] >= 48)
        json.dump(t, open(lt, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print(f"alinhados: {total} itens")
    for l in linhas[:12]: print("  ", l)
    if linhas: open(os.path.join(os.path.dirname(C), "Docs", "alinhamento_poder.txt"), "w", encoding="utf-8").write("\n".join(linhas) + "\n")


main()
