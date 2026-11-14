#!/usr/bin/env python3
"""
12/11 (pendências do dono): rodar DEPOIS de gerar_ramificacoes.py. Idempotente.
• Crônica de KRYMOR (história principal): 8 capítulos com a Inês (Biblioteca do Cais, Porto), um por região, do 11 ao 60 —
  cada capítulo pede o chefe que "segura" a região; o último fecha a história. Os níveis 1–8 não mudam.
• Asas da Serpente Celeste (nível 50, 4º nível de asas: ferro 25 → classe 35 → neve 45 → serpente 50): prêmio do chefe do
  Caminho da Serpente.
• Chefe de mundo semanal: o Kraken do Triângulo (nível 60) num canto do Triângulo do Demônio, volta a cada 7 dias; o servidor
  avisa todo mundo 5 e 1 minuto antes (sistema de chefe de campo).
"""
import json, os
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
def L(n): return json.load(open(os.path.join(C, n), encoding="utf-8"))
def D(n, o): json.dump(o, open(os.path.join(C, n), "w", encoding="utf-8"), ensure_ascii=False, indent=2)

CAP = [
  (11, "monster_r1_capataz_da_pedreira", "vale_das_cinzas", "I · A Pedreira", "«Toda história de KRYMOR começa com cinza. A do Vale começa com um capataz que fez da pedreira um cemitério. Acabe com ele e volte para eu escrever o primeiro capítulo.»"),
  (20, "monster_mata_guardiao_retorcido", "mata_dos_enforcados", "II · A Mata que Enforca", "«Os enforcados da Mata não morreram sozinhos: alguém os guarda. O Guardião Retorcido. Ele é o segundo capítulo.»"),
  (33, "monster_canions_rei_dos_canions", "canions_suplicantes", "III · O Rei dos Suplicantes", "«Nos Cânions, os vivos imploram a um rei que nunca responde. Faça ele responder — com o seu golpe.»"),
  (38, "monster_brejo_abadessa_lodo", "mosteiro_afundado", "IV · A Abadessa", "«O Mosteiro afundou com uma abadessa rezando ao contrário. A reza ainda funciona. Interrompa.»"),
  (47, "monster_vespera_prefeito_sem_rosto", "vespera", "V · O Prefeito Sem Rosto", "«Véspera tem um prefeito que governa sem rosto, sem voz e sem fim de mandato. Termine o mandato.»"),
  (50, "monster_geada_rei_draugr", "coroa_da_geada", "VI · A Coroa de Gelo", "«Na Geada, um rei morto ainda usa a coroa. As cinzas que caem no Porto vêm do norte — e o norte é dele.»"),
  (56, "monster_escoria_colosso_escoria", "mar_de_escoria", "VII · O Mar que Queima", "«A Escória é o que sobrou do mundo antigo derretido. O Colosso é o que sobrou de quem derreteu. Penúltimo capítulo.»"),
  (60, "monster_acropole_tita_bronze", "acropole_das_cinzas", "VIII · As Cinzas da Maré", "«Na Acrópole está o Titã que acendeu o primeiro fogo. Derrube-o e as cinzas param de cair. Volte — eu escrevo o fim.»"),
]


# 12/11 (auditoria: "duas histórias principais pedindo os mesmos chefes"): a Crônica não pede chefe de novo — cada capítulo
# é REGISTRADO com a Inês depois que a missão de história daquela região acabou (a linha M e as cadeias continuam sendo a
# história jogada; a Crônica é o livro que amarra tudo). Só o capítulo VIII (o Titã, que nenhuma outra missão pede) é luta.
REGISTRO = {1: "q_capataz", 2: "q_m16", 3: "q_cad_canions_3", 4: "q_m32", 5: "q_cad_vespera_3", 6: "q_m44", 7: "q_m55"}


def main():
    q = []
    todas = {}
    for f in os.listdir(C):
        if f.startswith("quests") and f.endswith(".json") and f != "quests_cronica.json":
            for x in json.load(open(os.path.join(C, f), encoding="utf-8")):
                todas[x["id"]] = x
    for i, (lv, boss, mapa, nome, desc) in enumerate(CAP, 1):
        base = {"id": f"q_cronica_{i}", "name": "Crônica " + nome, "giver": "bibliotecaria_ines", "category": "historia_principal",
                "reward_flags": [f"cronica:{i}"]}
        if i in REGISTRO:
            reg = todas[REGISTRO[i]]
            base.update(kind="talk", targets=["bibliotecaria_ines"], count=1, min_level=max(reg.get("min_level", lv), 11),
                        requires=REGISTRO[i],
                        description=f"«Você terminou '{reg['name']}'. Conte-me como foi — cada região vencida é um capítulo da Crônica de KRYMOR.»",
                        hint=f"Capítulo {i} de 8. Termine a missão '{reg['name']}' e volte à Inês (Biblioteca do Cais, Porto).",
                        reward_xp=int(lv * lv * 60), reward_coins=lv * 40)
        else:
            base.update(kind="boss", targets=[boss], count=1, map=mapa, min_level=lv, description=desc,
                        hint=f"Capítulo final. Vença o Titã de Bronze na Acrópole e volte à Inês.",
                        reward_xp=int(lv * lv * 160), reward_coins=lv * 80, reward_items=["consumable_c05_tonico_forte*20"])
        if i > 1:
            base["requires_flag"] = f"cronica:{i - 1}"
        q.append(base)
    D("quests_cronica.json", q)

    # asas do 50
    itens = [{"id": "wings_serpente", "display_name": "Asas da Serpente Celeste", "category": "wings", "subtype": "cristal", "min_level": 50,
              "allowed_classes": ["guerreiro", "mago", "bruxa", "arqueiro"], "handedness": 0,
              "base_stats": {"power_pct": 0.26, "move_speed_pct": 0.22, "armor": 120, "max_health": 380, "resource_regen_pct": 0.3},
              "affix_pool": "", "temperable": False, "trade_rule": "free", "stack_limit": 1, "icon_asset_id": "ico_wings_cristal", "model_asset_id": "wings_cristal",
              "summary": "Escamas douradas da Serpente Celeste em ossos de nuvem. 4º nível de asas: ataque +26%, velocidade +22%, armadura +120, vida +380, regeneração +30%. G = voar.",
              "price": 0, "source": "Caminho da Serpente: missão do chefe (Celina, a Guardiã da Ponte)."}]
    D("items_cronica.json", itens)
    qr = L("quests_ramificacoes.json")
    for x in qr:
        if x["id"] == "q_caminho_serpente_3":
            x["reward_items"] = ["wings_serpente", "consumable_c02_tonico_medio*5"]
            x["description"] = "«A Serpente Celeste dorme no fim da ponte. Quem a vence leva as escamas dela nas costas — as melhores asas de KRYMOR.»"
    D("quests_ramificacoes.json", qr)

    # chefe de mundo semanal
    base = {m["id"]: m for m in L("monsters_ramificacoes.json")}["monster_fundo_leviata"]
    kr = {**base, "id": "monster_mundo_kraken", "species_name": "Kraken do Triângulo (chefe semanal)", "level": 60, "health_mult": round(base["health_mult"] * 3, 2),
          "damage_mult": round(base["damage_mult"] * 1.2, 2), "xp_mult": 40.0, "radius": max(base["radius"], 2.0), "coins_min": 6000, "coins_max": 12000}
    D("monsters_mundo.json", [kr])
    m = L("map_triangulo_demonio.json")
    m["spawns"] = [s for s in m["spawns"] if s["id"] != "chefe_semanal_kraken"] + [
        {"id": "chefe_semanal_kraken", "monster": "monster_mundo_kraken", "x": 40.0, "y": -40.0, "level": 60, "group": "chefe_semanal", "respawn_seconds": 604800}]
    m["zones"] = [z for z in m["zones"] if z["id"] != "kraken"] + [{"id": "kraken", "name": "Toca do Kraken (chefe semanal)", "kind": "chefe", "x": 40, "y": -40, "w": 18, "h": 18}]
    m["obstacles"] = [o for o in m["obstacles"] if (o["x"] - 40) ** 2 + (o["y"] + 40) ** 2 > 7 ** 2]
    D("map_triangulo_demonio.json", m)
    print("crônica: 8 capítulos · asas do 50 · Kraken semanal")


if __name__ == "__main__":
    main()
