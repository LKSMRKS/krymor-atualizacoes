"""Itens dos 15 conjuntos lendários e das 50 armas lendárias (14/11) — atributos pelas FÓRMULAS do Docs/GUIA_BALANCEAMENTO.md
(as mesmas curvas que os itens do jogo já seguem, ajustadas pelos dados atuais). Gera Content/items_lendarios.json.
Os itens nascem sem drop: quem liga é a tabela de saque / a loja de recompensas extras (ver o guia, seção 7).
Uso: python Tools/gerar_itens_lendarios.py"""
import json, os

C = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Content")

# ------------------------------------------------------------------ fórmulas (guia, seção 2)
RARIDADE = {"enferrujada": 0.80, "comum": 1.00, "refinada": 1.12, "excelente": 1.25, "conjunto": 1.25, "lendaria": 1.18, "mitica": 1.35}
INTERVALO = {"dagger": 750, "sword": 950, "sword2": 1100, "spear": 1050, "axe": 1100, "axe2": 1300, "hammer": 1250, "staff": 1000, "bow": 900, "talisman": 1000}

def dps_base(L):
    """dano por segundo de uma arma comum no nível L (1 golpe/s); bate com os itens atuais (L17≈55, L36≈106, L58≈166)"""
    return 10 + 2.75 * L

def poder_arma(L, subtipo, raridade):
    return round(dps_base(L) * INTERVALO[subtipo] / 1000 * RARIDADE[raridade])

def entra(L):
    """a parte fixa da curva entra aos poucos do nível 1 ao 15 (o começo do jogo é mais leve que a reta)"""
    return max(0.0, min(1.0, (L - 1) / 14.0))

def armadura_peito(cls, L):
    """peitoral comum no nível L — ajuste linear das armaduras regionais (armor_<classe>_<região>), a progressão principal"""
    a, b = {"guerreiro": (46, 14.0), "arqueiro": (10, 10.4), "mago": (20, 6.3)}.get(cls, (6, 7.4))
    return a * entra(L) + b * L

def vida_peito(cls, L):
    a, b = {"guerreiro": (80, 3.0), "mago": (72, 2.7)}.get(cls, (30, 4.2))
    return 10 + a * entra(L) + b * L

FRACAO = dict(capacete=(0.40, 0.40), peitoral=(1.0, 1.0), luvas=(0.30, 0.30), calca=(0.45, 0.52), botas=(0.35, 0.35))
PRECO_PECA = dict(capacete=0.46, peitoral=1.0, luvas=0.46, calca=0.51, botas=0.46)

def preco_peitoral(L): return int(3150 * (L / 20) ** 2.5)
def preco_arma(L): return int(1700 * (L / 20) ** 2.5)

def extras_peca(cls, pc, L):
    """atributo exclusivo de cada peça (regra do dono 11/11), crescendo de 0 (nível 10) a 1 (nível 60)"""
    t = max(0.0, min(1.0, (L - 10) / 50.0)); mago = cls in ("mago", "bruxa")
    if pc == "capacete":
        d = dict(cdr_pct=round(0.03 + 0.04 * t, 3), xp_pct=round(0.03 + 0.04 * t, 3))
        if mago: d["resource_regen_pct"] = round(0.2 + 0.1 * t, 2)
        return d
    if pc == "peitoral":
        d = dict(thorns_pct=round(0.05 + 0.08 * t, 3), damage_reduction_pct=round(0.02 + 0.04 * t, 3))
        if mago: d["resource_regen_pct"] = round(0.4 + 0.2 * t, 2)
        return d
    if pc == "calca": return dict(stamina_regen_pct=round(0.1 + 0.18 * t, 2), stun_resist_pct=round(0.05 + 0.1 * t, 3))
    if pc == "luvas": return dict(power_pct=round(0.02 + 0.04 * t, 3), attack_speed_pct=round(0.04 + 0.05 * t, 3), crit_pct=round(0.01 + 0.03 * t, 3))
    return dict(move_speed_pct=round(0.03 + 0.03 * t, 3), dodge_pct=round(0.02 + 0.03 * t, 3))

def bonus_conjunto(A, elemento_stat):
    """bônus de 2/3/4/5 peças: escala com a armadura do peitoral (k = A/172, igual aos conjuntos atuais) + o atributo do elemento"""
    k = A / 172.0
    b = {"2": {"max_health": int(18 * k), "power_pct": 0.03}, "3": {"double_pct": 0.04, "armor": int(22 * k)},
         "4": {"armor": int(34 * k), "power_pct": 0.04, "crit_pct": 0.03}, "5": {"armor": int(26 * k), "power_pct": 0.06, "crit_pct": 0.05, "double_pct": 0.04}}
    b["5"].update(elemento_stat)
    return b

# ------------------------------------------------------------------ conjuntos (faixa 45–60: o jogo vai até o 60)
SETS = [
    # id, classe, nível, elemento (atributo do bônus de 5), nomes
    ("arm_runico_ancestral", "mago", 45, {"cdr_pct": 0.05}, dict(capacete="Elmo Rúnico Ancestral", peitoral="Couraça Rúnica Ancestral", luvas="Manoplas Rúnicas Ancestrais", calca="Manto Rúnico Ancestral", botas="Botas Rúnicas Ancestrais")),
    ("arm_pedra", "guerreiro", 45, {"damage_reduction_pct": 0.05}, dict(capacete="Elmo de Pedra Viva", peitoral="Couraça de Pedra Viva", luvas="Punhos de Pedra Viva", calca="Coxotes de Pedra Viva", botas="Pés de Pedra Viva")),
    ("arm_floresta", "bruxa", 45, {"healing_received_pct": 0.10}, dict(capacete="Galhada da Floresta", peitoral="Corpete da Floresta", luvas="Braçadeiras da Floresta", calca="Saia de Folhas", botas="Botas de Raiz")),
    ("arm_cavaleiro_sagrado", "guerreiro", 50, {"healing_received_pct": 0.08, "magic_resist_pct": 0.06}, dict(capacete="Elmo Sagrado", peitoral="Couraça Sagrada", luvas="Manoplas Sagradas", calca="Coxotes Sagrados", botas="Grevas Sagradas")),
    ("arm_gelo_eterno", "bruxa", 50, {"tenacity_pct": 0.08}, dict(capacete="Coroa de Gelo Eterno", peitoral="Corpete de Gelo Eterno", luvas="Braçadeiras de Gelo Eterno", calca="Saia de Gelo Eterno", botas="Botas de Gelo Eterno")),
    ("arm_necromante", "bruxa", 50, {"mana_cost_pct": -0.08}, dict(capacete="Capuz do Necromante", peitoral="Corpete de Ossos do Necromante", luvas="Garras do Necromante", calca="Manto do Necromante", botas="Botas do Necromante")),
    ("arm_abissal_profundo", "arqueiro", 50, {"projectile_speed_pct": 0.10}, dict(capacete="Elmo Abissal", peitoral="Couraça Abissal", luvas="Braçadeiras Abissais", calca="Faldas Abissais", botas="Botas Abissais")),
    ("arm_dragao_fogo", "guerreiro", 55, {"thorns_pct": 0.08}, dict(capacete="Elmo do Dragão de Fogo", peitoral="Couraça do Dragão de Fogo", luvas="Garras do Dragão de Fogo", calca="Coxotes do Dragão de Fogo", botas="Grevas do Dragão de Fogo")),
    ("arm_abismo", "mago", 55, {"aoe_radius_bonus": 0.5}, dict(capacete="Capuz do Abismo", peitoral="Couraça do Abismo", luvas="Manoplas do Abismo", calca="Manto do Abismo", botas="Botas do Abismo")),
    ("arm_faraonico", "bruxa", 55, {"xp_pct": 0.06, "resource_regen_pct": 0.2}, dict(capacete="Nemes Faraônico", peitoral="Peitoral Faraônico", luvas="Braceletes Faraônicos", calca="Saiote Faraônico", botas="Sandálias Faraônicas")),
    ("arm_celestial", "arqueiro", 60, {"crit_pct": 0.04, "hit_pct": 0.05}, dict(capacete="Elmo Celestial", peitoral="Couraça Celestial", luvas="Braçadeiras Celestiais", calca="Faldas Celestiais", botas="Botas Celestiais")),
    ("arm_demoniaco", "guerreiro", 60, {"power_pct": 0.05}, dict(capacete="Elmo Demoníaco", peitoral="Couraça Demoníaca", luvas="Manoplas Demoníacas", calca="Coxotes Demoníacos", botas="Grevas Demoníacas")),
    ("arm_tempo", "mago", 60, {"cdr_pct": 0.08}, dict(capacete="Elmo do Tempo", peitoral="Couraça do Tempo", luvas="Manoplas do Tempo", calca="Faldas do Tempo", botas="Botas do Tempo")),
    ("arm_vazio", "mago", 60, {"power_pct": 0.06}, dict(capacete="Elmo do Vazio", peitoral="Couraça do Vazio", luvas="Manoplas do Vazio", calca="Manto do Vazio", botas="Botas do Vazio")),
    ("arm_anjo_caido", "arqueiro", 60, {"crit_pct": 0.06}, dict(capacete="Elmo do Anjo Caído", peitoral="Couraça do Anjo Caído", luvas="Braçadeiras do Anjo Caído", calca="Faldas do Anjo Caído", botas="Botas do Anjo Caído")),
]
ORIGEM_SET = {45: "Lendário: chefes de região do nível 45–50 (Coroa da Geada, Pico do Vento) — 1 peça por chefe, chance baixa.",
              50: "Lendário: chefes de região e dungeons do nível 50–55 (Mar de Escória, Fortaleza da Geada, Salão de Geada).",
              55: "Lendário: chefes do nível 55–60 (Fornalha, Cidadela de Escória, Acrópole) e o Chefe Errante.",
              60: "Lendário de fim de jogo: Labirinto do Juramento, Titã de Bronze e a aba Recompensas Extras da loja."}
CAT = dict(capacete="helmet", peitoral="armor", luvas="gloves", calca="pants", botas="boots")
ROT = dict(armor="Armadura", max_health="vida", resource_regen_pct="regeneração", cdr_pct="recarga", xp_pct="experiência", thorns_pct="espinhos",
           damage_reduction_pct="redução de dano", move_speed_pct="velocidade", stamina_regen_pct="fôlego", power_pct="poder", stun_resist_pct="resist. a atordoar",
           attack_speed_pct="vel. de ataque", crit_pct="crítico", dodge_pct="esquiva")

def resumo(st, cls):
    partes = []
    for k, v in st.items():
        if k in ("armor", "max_health"): partes.append(f"{ROT[k]} +{v}")
        elif k == "cdr_pct": partes.append(f"{ROT[k]} -{round(v * 100)}%")
        else: partes.append(f"{ROT.get(k, k)} {'+' if v >= 0 else ''}{round(v * 100)}%")
    s = ", ".join(partes); return s[0].upper() + s[1:] + f". Só para {cls.capitalize()}."

def main():
  itens = []
  for aid, cls, L, elem, nomes in SETS:
      A = armadura_peito(cls, L) * RARIDADE["conjunto"]; H = vida_peito(cls, L) * RARIDADE["conjunto"]
      for pc in ("capacete", "peitoral", "calca", "luvas", "botas"):
          fa, fh = FRACAO[pc]
          st = {"armor": int(round(A * fa)), "max_health": int(round(H * fh))}; st.update(extras_peca(cls, pc, L))
          it = {"id": f"{aid}_{pc}", "display_name": nomes[pc], "category": CAT[pc], "subtype": pc, "min_level": L, "allowed_classes": [cls],
                "handedness": 0, "base_stats": st, "affix_pool": "armor", "temperable": True, "trade_rule": "free", "stack_limit": 1,
                "icon_asset_id": f"ico_{aid}_{pc}", "model_asset_id": f"{aid}:{pc}", "summary": resumo(st, cls), "quality": "lendario",
                "price": int(preco_peitoral(L) * PRECO_PECA[pc] * 1.5), "source": ORIGEM_SET[L], "set": "set_" + aid}
          if pc == "peitoral": it["set_bonus"] = bonus_conjunto(A, elem)
          itens.append(it)

  # ------------------------------------------------------------------ armas lendárias
  TIPOS = {  # forma: (subtipo do jogo, classe, mãos, proc, golpes)
      "espada": ("sword", "guerreiro", 1, "skill_lend_corte_fantasma", 5), "machado_gigante": ("axe2", "guerreiro", 2, "skill_lend_rachar", 6),
      "foice": ("spear", "guerreiro", 2, "skill_lend_sangria", 4), "garfo": ("spear", "guerreiro", 2, "skill_lend_perfurar", 4),
      "espada_dupla": ("sword2", "guerreiro", 2, "skill_lend_corte_fantasma", 5), "lanca": ("spear", "guerreiro", 2, "skill_lend_perfurar", 4),
      "manopla": ("hammer", "guerreiro", 1, "skill_lend_terremoto", 7), "cajado": ("staff", "mago", 2, "skill_lend_eco", 5),
      "foco_natural": ("talisman", "bruxa", 1, "skill_lend_maldicao", 5), "arco": ("bow", "arqueiro", 2, "skill_lend_chuva", 5),
  }
  SUBTIPO_JOGO = {"axe2": "axe", "sword2": "sword"}
  ELEM = {  # elemento: (nível, atributo extra, nome)
      "sagrado": (45, {"healing_received_pct": 0.06}, ("Sagrado", "Sagrada")), "gelo": (50, {"stun_resist_pct": 0.08}, ("de Gelo", "de Gelo")),
      "fogo": (55, {"crit_pct": 0.04}, ("de Fogo", "de Fogo")), "vazio": (60, {"power_pct": 0.05}, ("do Vazio", "do Vazio")),
      "celestial": (60, {"attack_speed_pct": 0.05}, ("Celestial", "Celestial")),
  }
  NOME = dict(espada="Espada", machado_gigante="Machado Gigante", foice="Foice", garfo="Garfo de Guerra", espada_dupla="Espada Dupla",
              foco_natural="Foco Natural", cajado="Cajado", arco="Arco", lanca="Lança", manopla="Manopla")
  FEM = {"Espada", "Foice", "Espada Dupla", "Lança", "Manopla"}
  EFEITO = {"skill_lend_corte_fantasma": "um corte fantasma acerta tudo em volta do alvo", "skill_lend_rachar": "racha o chão e quebra a preparação de quem está perto",
            "skill_lend_sangria": "abre uma sangria em volta do alvo", "skill_lend_perfurar": "uma estocada atravessa a fila (muito dano)",
            "skill_lend_terremoto": "um terremoto em volta do alvo", "skill_lend_eco": "a magia ecoa em volta do alvo",
            "skill_lend_maldicao": "uma maldição deixa os inimigos lentos", "skill_lend_chuva": "chove flecha em volta do alvo"}
  p = "/home/claude/cinzas/armas_lend/_armas_lendarias_info.json"
  for forma, (sub, cls, maos, proc, golpes) in TIPOS.items():
      for e, (L, extra, nm) in ELEM.items():
          aid = f"arma_lend_{forma}_{e}"
          n = NOME[forma]; nome = f"{n} {nm[1] if n in FEM else nm[0]}"
          st = {"weapon_power": poder_arma(L, sub, "lendaria"), "attack_interval_ms": INTERVALO[sub], "proc_every": golpes}
          st.update(extra)
          it = {"id": aid, "display_name": nome, "category": "weapon", "subtype": SUBTIPO_JOGO.get(sub, sub), "min_level": L, "allowed_classes": [cls],
                "handedness": maos, "base_stats": st, "model_asset_id": aid, "icon_asset_id": "ico_" + aid[5:], "proc_skill": proc,
                "effect": f"A cada {golpes} golpes que acertam, {EFEITO[proc]}.",
                "req_primary": int(L * 2.4), "affix_pool": "physical_weapon" if cls == "guerreiro" else "none", "temperable": True, "trade_rule": "free",
                "stack_limit": 1, "quality": "lendario", "price": int(preco_arma(L) * 1.5),
                "source": "Lendária: " + {45: "chefes nível 45–50", 50: "chefes e dungeons 50–55", 55: "chefes 55–60 e Chefe Errante", 60: "fim de jogo (Labirinto do Juramento, Titã de Bronze) e aba Recompensas Extras"}[L] + "."}
          it["summary"] = f"{'Uma mão' if maos == 1 else 'Duas mãos'}, ataque a cada {str(round(INTERVALO[sub] / 1000, 2)).replace('.', ',')} s. " + it["effect"]
          itens.append(it)

  json.dump(itens, open(os.path.join(C, "items_lendarios.json"), "w", encoding="utf-8"), indent=2, ensure_ascii=False)
  print(len(itens), "itens lendários ->", os.path.join(C, "items_lendarios.json"))

if __name__ == "__main__":
    main()
