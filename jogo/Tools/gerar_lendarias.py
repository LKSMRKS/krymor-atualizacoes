#!/usr/bin/env python3
"""
13/11 (análise do jogo — itens: "lendária com efeito próprio por arma, mas de um jeito barato"): toda arma "lendaria_*"
ganha o efeito do TIPO dela (o servidor já dispara proc_skill a cada proc_every golpes básicos que acertam):
espada = Corte Fantasma · machado = Rachar o Chão (atordoa) · maça/martelo = Terremoto · lança = Perfurar ·
cajado = Eco Arcano · arco = Chuva de Flechas · talismã = Maldição · faca = Sangria. Rodar depois do gerar_arsenal.py.
"""
import json, os

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
EF = {  # subtipo: (skill, nome, raio, coeficiente, a cada, efeitos, texto)
    "sword": ("skill_lend_corte_fantasma", "Corte Fantasma", 2.6, 1.1, 5, {}, "a cada 5 golpes, um corte fantasma acerta tudo em volta do alvo"),
    "axe": ("skill_lend_rachar", "Rachar o Chão", 3.0, 0.9, 6, {"stagger": 1}, "a cada 6 golpes, racha o chão e quebra a preparação de quem estiver perto"),
    "hammer": ("skill_lend_terremoto", "Terremoto", 3.6, 1.2, 7, {"stagger": 1}, "a cada 7 golpes, um terremoto em volta do alvo"),
    "club": ("skill_lend_terremoto", "Terremoto", 3.6, 1.2, 7, {"stagger": 1}, "a cada 7 golpes, um terremoto em volta do alvo"),
    "spear": ("skill_lend_perfurar", "Perfurar", 1.8, 1.7, 4, {}, "a cada 4 golpes, uma estocada que atravessa (muito dano)"),
    "staff": ("skill_lend_eco", "Eco Arcano", 3.0, 1.0, 5, {}, "a cada 5 golpes, a magia ecoa em volta do alvo"),
    "bow": ("skill_lend_chuva", "Chuva de Flechas", 3.2, 0.8, 5, {}, "a cada 5 flechas, chove flecha em volta do alvo"),
    "talisman": ("skill_lend_maldicao", "Maldição", 3.0, 0.9, 5, {"chill_ms": 1500}, "a cada 5 golpes, uma maldição que deixa os inimigos lentos"),
    "dagger": ("skill_lend_sangria", "Sangria", 2.2, 1.3, 4, {}, "a cada 4 golpes, uma sangria em volta do alvo"),
}
CLASSE = {"sword": "guerreiro", "axe": "guerreiro", "hammer": "guerreiro", "club": "guerreiro", "spear": "guerreiro", "dagger": "guerreiro",
          "staff": "mago", "bow": "arqueiro", "talisman": "bruxa"}
skills, vistos = [], set()
for sub, (sid, nome, raio, coef, cada, efeitos, texto) in EF.items():
    if sid in vistos: continue
    vistos.add(sid)
    skills.append({"id": sid, "class_id": CLASSE[sub], "display_name": nome, "unlock_level": 1, "target_mode": "nova", "range": 0, "radius": raio,
                   "cooldown_ms": 0, "resource_cost": 0, "resource_generated": 0, "coefficient": coef, "effects": efeitos, "cast_ms": 0,
                   "recovery_ms": 0, "description": "Efeito lendário: " + texto + "."})
json.dump(skills, open(os.path.join(C, "skills_lendarias.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
f = os.path.join(C, "items_arsenal.json")
itens = json.load(open(f, encoding="utf-8"))
n = 0
for i in itens:
    if not i["id"].startswith("lendaria_") or i.get("subtype") not in EF: continue
    sid, nome, raio, coef, cada, efeitos, texto = EF[i["subtype"]]
    i["proc_skill"] = sid
    i["base_stats"]["proc_every"] = cada
    marca = " · Lendária: "
    i["summary"] = i.get("summary", "").split(marca)[0] + marca + nome + " (" + texto + ")."
    n += 1
json.dump(itens, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
print("lendárias com efeito:", n, "· efeitos:", len(skills))

# ---------------- 13/11 (reforjar): Joia da Forja — em cima de uma peça de conjunto, sorteia a variação de novo
forja = {"id": "joia_forja", "display_name": "Joia da Forja", "category": "material", "subtype": "joia", "min_level": 1, "allowed_classes": [],
         "handedness": 0, "base_stats": {}, "affix_pool": "none", "temperable": False, "trade_rule": "free", "stack_limit": 50,
         "icon_asset_id": "ico_joia_brasa", "model_asset_id": "item_joia_brasa",
         "summary": "REFORJAR: arraste em cima de uma peça de conjunto (V1–V14) e a variação é sorteada de novo — de 2 abaixo a 3 acima. Custa moedas.",
         "price": 6000, "source": "Máquina do Caos (2 Joias da Brasa + 3 da Maré), Caixas das Cinzas +3 a +5 e chefes."}
json.dump([forja], open(os.path.join(C, "items_forja.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
json.dump([{"id": "caos_joia_forja", "name": "Joia da Forja (70%)", "station": "caos", "inputs": [{"item": "joia_brasa", "qty": 2}, {"item": "joia_mare", "qty": 3}],
            "output": "joia_forja", "output_qty": 1, "min_level": 15, "chance": 0.7}],
          open(os.path.join(C, "recipes_forja.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
print("joia da forja + receita no Caos")
