#!/usr/bin/env python3
"""
12/11 (pedido do dono: "liberar habilidades da classe 1 — mesma quantidade para todos, um poder novo a cada tantos níveis"):
cada classe passa a ter 8 poderes de tomo na 1ª classe (níveis 1 a 14), na mesma cadência:
  Guerreiro: Cunha 1 · Guarda 3 · ESTOCADA DE LANÇA 5 · Giro 6 · LÂMINAS DE SANGUE 8 · Salto 10 · CHUVA DE ESPADAS 12 · Fúria 14
  Mago:      Explosão 1 · Passo 3 · ESFERA DE PROTEÇÃO 5 · Estaca de Gelo 6 · PRISÃO DE GELO 8 · Meteoros 10 · ANEL DE CHAMAS 12 · Cometa 14
  Bruxa:     Toque 1 · Barreira 3 · Aura 5 · RAÍZES QUE PRENDEM 6 · Espinhos 8 · TRONCO ESMAGADOR 10 · VINHAS CHICOTE 12 · Floresta 14
  Arqueiro:  Emboscada 1 · Passos 3 · Olhar 5 · Rajada 6 · Perfurante 8 · Chuva 10 · TIRO CERTEIRO (sniper) 12 · Fênix 14 (+ Serpente 16)
Só acrescenta (skills_classe1.json + tomos + nível II + lojas dos mentores); não mexe nos poderes que já existiam.
"""
import json, os
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
def L(n): return json.load(open(os.path.join(C, n), encoding="utf-8"))
def D(n, o): open(os.path.join(C, n), "w", encoding="utf-8").write(json.dumps(o, ensure_ascii=False, indent=2) + "\n")
W0 = {"move_mult_windup": 0, "move_mult_recovery": 0.5}

def sk(id, cls, nome, modo, alc, raio, cd, custo, coef, ef, cast, rec, desc):
    return {"id": id, "class_id": cls, "display_name": nome, "unlock_level": 1, "target_mode": modo, "range": alc, "radius": raio,
            "cooldown_ms": cd, "resource_cost": custo, "resource_generated": 0, "coefficient": coef, "effects": ef,
            "cast_ms": cast, "recovery_ms": rec, "description": desc}

NOVAS = [
    # (skill, nível, preço)
    (sk("skill_guerreiro_estocada", "guerreiro", "Estocada de Lança", "cone", 7.0, 0, 6000, 20, 1.6, {"arc_degrees": 18, "stagger": 1, **W0}, 320, 380,
        "Estocada reta como uma lança de energia: acerta tudo numa linha estreita de 7 m à frente e interrompe preparações."), 5, 700),
    (sk("skill_guerreiro_laminas_sangue", "guerreiro", "Lâminas de Sangue", "cone", 4.6, 0, 9000, 30, 2.3,
        {"arc_degrees": 120, "armor_shred_pct": 0.2, "armor_shred_ms": 5000, **W0}, 380, 450,
        "Gira os dois braços e solta duas lâminas de sangue em X à frente (4,6 m): dano alto e a armadura do alvo cai 20% por 5 s."), 8, 2000),
    (sk("skill_guerreiro_chuva_espadas", "guerreiro", "Chuva de Espadas", "ground", 13, 4.0, 14000, 40, 2.8, {"stagger": 1, **W0}, 900, 450,
        "Joga a espada para o alto: o chão marcado (raio 4 m) recebe uma chuva de lâminas que cai do céu."), 12, 5500),
    (sk("skill_mago_esfera_protecao", "mago", "Esfera de Proteção", "barrier", 10, 0, 14000, 30, 1.1, {"absorb_flat": 40, "duration_ms": 7000, "move_mult_windup": 0.5, "move_mult_recovery": 0.8}, 250, 200,
        "Bola de proteção arcana em você (ou no aliado mais ferido a até 10 m): absorve golpes por 7 s."), 5, 700),
    (sk("skill_mago_prisao_gelo", "mago", "Prisão de Gelo", "ground", 13, 3.5, 11000, 30, 1.2, {"freeze_ms": 2200, "chill_ms": 4500, "chill_mult": 0.5, **W0}, 600, 350,
        "Congela o chão marcado (raio 3,5 m): quem estiver lá fica PRESO 2,2 s e lento depois (chefe: metade)."), 8, 2000),
    (sk("skill_mago_anel_chamas", "mago", "Anel de Chamas", "nova", 0, 4.5, 10000, 35, 2.2, {"stagger": 1, **W0}, 450, 450,
        "Um anel de fogo explode em volta de você (4,5 m) e empurra a preparação dos inimigos."), 12, 5500),
    (sk("skill_bruxa_raizes", "bruxa", "Raízes que Prendem", "ground", 13, 3.2, 11000, 25, 0.9, {"freeze_ms": 2500, "chill_ms": 4000, "chill_mult": 0.55, **W0}, 500, 350,
        "Raízes saem do chão marcado (raio 3,2 m) e PRENDEM os inimigos por 2,5 s (chefe: metade)."), 6, 1200),
    (sk("skill_bruxa_tronco", "bruxa", "Tronco Esmagador", "ground", 12, 2.4, 9000, 30, 2.6, {"stagger": 1, **W0}, 650, 400,
        "Um tronco brota e desaba no ponto marcado (raio 2,4 m): golpe pesado que quebra a preparação."), 10, 3500),
    (sk("skill_bruxa_vinhas", "bruxa", "Vinhas Chicote", "cone", 6.5, 0, 7000, 20, 1.5, {"arc_degrees": 75, "chill_ms": 2500, "chill_mult": 0.6, **W0}, 300, 350,
        "Vinhas chicoteiam à frente (6,5 m) e deixam os atingidos lentos."), 12, 5500),
    (sk("skill_arqueiro_tiro_certeiro", "arqueiro", "Tiro Certeiro", "projectile", 42, 0.45, 10000, 35, 3.4, {"projectile_speed": 60, "stagger": 1, **W0}, 1200, 400,
        "Mira longa de atirador: uma flecha muito rápida que alcança 42 m e acerta forte o primeiro alvo."), 12, 5500),
]
WHO = {"guerreiro": "ao Guerreiro", "mago": "ao Mago", "bruxa": "à Bruxa", "arqueiro": "ao Arqueiro"}
KIND = {"guerreiro": "Esfera", "mago": "Livro", "bruxa": "Grimório", "arqueiro": "Pergaminho"}

def main():
    D("skills_classe1.json", [s for s, _, _ in NOVAS])
    tomos = [t for t in L("consumables_tomos.json") if not any(t["id"] in ("tomo_" + s["id"][6:], "tomo_" + s["id"][6:] + "_ii") for s, _, _ in NOVAS)]
    shops = L("shops_tomos.json")
    for s, lvl, preco in NOVAS:
        cls, nome, base = s["class_id"], s["display_name"], s["id"][6:]
        tomos.append({"id": "tomo_" + base, "display_name": KIND[cls] + ": " + nome, "heal_percent_max_health": 0, "heal_duration_ms": 0,
                      "cooldown_group": "tomo", "cooldown_ms": 0, "stack_limit": 5, "price": preco, "classes": [cls], "teach_skill": s["id"], "min_level": lvl,
                      "summary": f"Ensina {WHO[cls]}: {nome} (nível {lvl}+). Use na mochila; depois escolha a tecla no Livro de Poderes (K)."})
        tomos.append({"id": "tomo_" + base + "_ii", "display_name": KIND[cls] + " II: " + nome, "heal_percent_max_health": 0, "heal_duration_ms": 0,
                      "cooldown_group": "tomo", "cooldown_ms": 0, "stack_limit": 5, "price": max(6000, preco * 5), "classes": [cls], "upgrade_skill": s["id"], "min_level": 15,
                      "summary": f"Nível 2 de {nome}: +40% de força e recarga 20% menor. Só depois da troca de classe (nível 15); precisa já saber o poder."})
        loja = next(x for x in shops if x["id"] == "loja_tomos_" + cls)
        for tid in ("tomo_" + base, "tomo_" + base + "_ii"):
            if tid not in loja["items"]:
                loja["items"].append(tid)
    D("consumables_tomos.json", tomos)
    D("shops_tomos.json", shops)
    print("classe 1:", len(NOVAS), "poderes novos")

if __name__ == "__main__":
    main()
