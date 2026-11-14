#!/usr/bin/env python3
"""
12/11 (pedido do dono: "tem mais missão no começo e depois vai caindo — equilibra, sem ficar repetitivo, aproveitando os
lugares, mecânicas e explorações"): uma cadeia de história por região do 17 ao 50 (Mata, Cânions, Brejo, Véspera, Geada),
dada pelos moradores que já estão no acampamento: caça com motivo, exploração (3 achados espalhados pelos bolsões, F em
cada um) e o chefe de campo/elite da região. Escreve Content/quests_cadeias.json e põe os achados nos mapas.
Idempotente. Rodar depois de gerar_regioes.py e gerar_chefes_campo.py.
"""
import json, os, math

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")

REG = [
  dict(mapa="mata_dos_enforcados", nome="Mata dos Enforcados", npc="carpinteiro_jorge", npc2="agricultora_benta",
       caca=("monster_mata_raiz_andante", 8, 21, "As Raízes que Andam", "«As raízes andantes estão arrancando as cercas da plantação da Benta. Corta oito antes que cheguem ao poço.»"),
       achados=("Galho com marca de corda", "As Cordas da Mata",
                "«Ninguém sabe por que chamam isto aqui de Mata dos Enforcados. Eu sei: três galhos ainda têm a corda. Ache os três e me diga o que está escrito na casca.»", 22),
       chefe=("monster_mata_guardiao_retorcido", "O Guardião Retorcido", "«As cordas foram um aviso do Guardião Retorcido. Ele protege a mata de gente — inclusive de nós. Derrube-o e a mata respira.»", 23),
       caca2=("monster_mata_aranha_toca", 10, 24, "Teias no Poço", "«As aranhas da toca encheram o poço de teia. Dez delas e a água volta.»")),
  dict(mapa="canions_suplicantes", nome="Cânions dos Suplicantes", npc="zelador_ascanio", npc2="mineiro_bras2",
       caca=("monster_canions_esqueleto_mineiro", 8, 25, "Os Mineiros Que Não Pararam", "«Os mineiros morreram no desabamento e continuam cavando. Oito deles precisam descansar.»"),
       achados=("Lamparina do desabamento", "As Lamparinas Perdidas",
                "«Quando a mina desabou, três lamparinas ficaram acesas lá dentro. Dizem que enquanto queimarem, as almas não sobem. Apague as três.»", 27),
       chefe=("monster_canions_rei_dos_canions", "O Rei dos Cânions", "«Com as lamparinas apagadas, o Rei dos Cânions acordou. Ele é o último a precisar descansar.»", 33),
       caca2=("monster_canions_espectro", 10, 30, "Os Suplicantes", "«Os espectros pedem, choram, suplicam. Dez deles — com respeito, mas com firmeza.»")),
  dict(mapa="brejo_da_viuva", nome="Brejo da Viúva", npc="escrivao_tadeu", npc2="costureira_alba",
       caca=("monster_brejo_serpente", 8, 31, "Serpentes no Cartório", "«As serpentes do brejo invadiram o arquivo e comeram duas certidões de casamento. Oito delas, por favor.»"),
       achados=("Carta de amor afogada", "As Cartas da Viúva",
                "«A Viúva do brejo escrevia ao marido todo dia. Três cartas ainda boiam pelo pântano. Traga-as — talvez ela descanse.»", 34),
       chefe=("monster_brejo_viuva_negra", "A Viúva Negra", "«As cartas acordaram a Viúva Negra. Ela não quer descansar: quer companhia. Não seja a companhia.»", 39),
       caca2=("monster_brejo_tecela", 10, 37, "Seda de Tecelã", "«A seda das tecelãs é a melhor do continente — se você conseguir arrancar. Dez tecelãs.»")),
  dict(mapa="vespera", nome="Véspera", npc="carteiro_espectral", npc2="guia_aurelio",
       caca=("monster_vespera_carnical_vespera", 8, 38, "Correio Atrasado", "«Os carniçais comem as cartas antes que eu entregue. Oito a menos e o correio dos mortos volta a andar.»"),
       achados=("Sino de bronze rachado", "Os Três Sinos",
                "«Véspera tinha três sinos. Quando tocavam juntos, os mortos lembravam quem eram. Ache os três sinos rachados e toque cada um.»", 41),
       chefe=("monster_vespera_prefeito_sem_rosto", "O Prefeito Sem Rosto", "«Os sinos lembraram aos mortos quem os governa: o Prefeito Sem Rosto. Hora de uma eleição.»", 46),
       caca2=("monster_vespera_necromante", 8, 45, "Necromancia Ilegal", "«Necromantes reabrindo túmulos sem licença. Oito, e o cemitério volta a ter paz.»")),
  dict(mapa="coroa_da_geada", nome="Coroa da Geada", npc="batedora_runa", npc2="cozinheira_hild",
       caca=("monster_geada_draugr", 8, 45, "Os Que Não Esfriam", "«Os draugr marcham para a caravana todas as noites. Oito deles e a gente dorme.»"),
       achados=("Runa congelada", "As Runas do Juramento",
                "«Três runas do Primeiro Juramento estão presas no gelo. Quebre o gelo de cada uma (F) e leia o que elas prometem.»", 48),
       chefe=("monster_geada_rei_draugr", "O Rei Draugr", "«As runas falam do Rei Draugr, que quebrou o juramento. Faça ele cumprir — de joelhos.»", 51),
       caca2=("monster_geada_troll_gelo", 8, 50, "Carne de Troll", "«Carne de troll de gelo rende um ensopado que aquece até a alma. Oito, e eu cozinho para a caravana inteira.»")),
]


def livre(m, x, y, r=1.2):
    if abs(x) > m["half_size"] - 3 or abs(y) > m["half_size"] - 3:
        return False
    for o in m["obstacles"]:
        if o.get("shape") == "circle":
            if math.hypot(x - o["x"], y - o["y"]) < o["r"] + r:
                return False
        elif abs(x - o["x"]) < o["w"] / 2 + r and abs(y - o["y"]) < o["h"] / 2 + r:
            return False
    for p in m["props"]:
        if math.hypot(x - p["x"], y - p["y"]) < 1.5 + r:
            return False
    for o in m["objects"]:
        if math.hypot(x - o["x"], y - o["y"]) < 3:
            return False
    return True


def perto(m, tx, ty):
    for raio in range(0, 30):
        for k in range(0, 360, 20):
            x, y = round(tx + raio * math.cos(math.radians(k)), 1), round(ty + raio * math.sin(math.radians(k)), 1)
            if livre(m, x, y):
                return x, y
    raise SystemExit("sem lugar")


def main():
    quests = []
    for r in REG:
        p = os.path.join(C, f"map_{r['mapa']}.json")
        m = json.load(open(p, encoding="utf-8"))
        rid = r["mapa"].split("_")[0]
        nomeA, tituloA, descA, lvA = r["achados"]
        alvos = [(-42, 34), (42, 34), (42, -10)]   # três bolsões diferentes
        ids = []
        for i, (tx, ty) in enumerate(alvos):
            oid = f"achado_{rid}_{i + 1}"
            ids.append(oid)
            if not any(o["id"] == oid for o in m["objects"]):
                x, y = perto(m, tx + 6, ty + 6)
                m["objects"].append({"id": oid, "kind": "reliquia", "x": x, "y": y, "label": nomeA})
        json.dump(m, open(p, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
        mon, n, lv, titulo, desc = r["caca"]
        q1, q2, q3, q4 = f"q_cad_{rid}_1", f"q_cad_{rid}_2", f"q_cad_{rid}_3", f"q_cad_{rid}_4"
        xp = lambda L: int(L * L * 70)
        quests.append({"id": q1, "name": titulo, "giver": r["npc"], "kind": "kill", "targets": [mon], "count": n, "map": r["mapa"], "min_level": lv,
                       "description": desc, "hint": f"Na {r['nome']}. Depois volte a quem pediu, no acampamento.", "reward_xp": xp(lv), "reward_coins": lv * 25, "category": "historia"})
        quests.append({"id": q2, "name": tituloA, "giver": r["npc"], "kind": "interact", "targets": ids, "count": 3, "map": r["mapa"], "min_level": lvA,
                       "requires": q1, "description": descA, "hint": f"Três achados espalhados pela {r['nome']} (oeste, leste e sudeste). Aperte F em cada um.",
                       "reward_xp": xp(lvA), "reward_coins": lvA * 30, "category": "historia"})
        cm, cn, cd, clv = r["chefe"]
        quests.append({"id": q3, "name": cn, "giver": r["npc"], "kind": "boss", "targets": [cm], "count": 1, "map": r["mapa"], "min_level": clv,
                       "requires": q2, "description": cd, "hint": f"{cn} na {r['nome']}. Vá preparado (tônicos e, se puder, um grupo).",
                       "reward_xp": xp(clv) * 2, "reward_coins": clv * 60, "reward_items": ["consumable_c02_tonico_medio*5"], "category": "historia"})
        m2, n2, l2, t2, d2 = r["caca2"]
        quests.append({"id": q4, "name": t2, "giver": r["npc2"], "kind": "kill", "targets": [m2], "count": n2, "map": r["mapa"], "min_level": l2,
                       "description": d2, "hint": f"Na {r['nome']}. Volte a quem pediu, no acampamento.", "reward_xp": xp(l2), "reward_coins": l2 * 25, "category": "opcional"})
    json.dump(quests, open(os.path.join(C, "quests_cadeias.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print(len(quests), "missões")


if __name__ == "__main__":
    main()
