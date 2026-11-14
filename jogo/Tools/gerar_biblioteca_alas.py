#!/usr/bin/env python3
"""
13/11 (pedido do dono): Biblioteca do Continente com GRADES por ala, liberadas por nível + missão da Irmã Clarabela
(GameServer.Biblioteca.cs). Cada fileira de alcovas é uma ala; os livros vão para o fundo das alcovas (fora do alcance
pela grade) e a grade fecha a boca da alcova dos dois lados do corredor. Uma placa ("contemplar") em cada grade diz o
que pede. Livro novo: "A Lenda do Rei Macaco" (as 5 esferas), na Ala I.
  Entrada (livre): Crônica da Krypta, Diário da Irmã Clarabela.
  Ala I   (nível 10 + Descanso aos Mortos do Cal): As Oito Terras, Bestiário dos Tipos, A Lenda do Rei Macaco.
  Ala II  (nível 20 + O Sino Calado): Atlas dos Caminhos Esquecidos (segredos), A Máscara que Ria.
  Ala III (nível 35 + A Abadessa do Lodo): O Uivo sob a Lua, Cartas de um Conde.
  Ala IV  (nível 42 + A Abadessa do Lodo): O Livro da Foice, Do Cão de Três Cabeças, Hinário da Cidade Celestial.
Idempotente.
"""
import json, os

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
F = os.path.join(RAIZ, "Content", "map_biblioteca_continente.json")
ALAS = [  # (n, centro y da alcova, altura da boca, nível, missão)
    (1, -1.0, 5.3, 10, "Descanso aos Mortos do Cal"),
    (2, -7.0, 5.3, 20, "O Sino Calado"),
    (3, -13.0, 5.3, 35, "A Abadessa do Lodo"),
    (4, -19.6, 6.4, 42, "A Abadessa do Lodo"),
]
LUGAR = {   # livro -> (x, y)
    "livro_krypta": (-4.2, 4.0), "livro_clarabela": (3.6, 4.0),
    "livro_terras": (-6.7, -1.0), "livro_bestiario": (6.7, -1.0), "livro_macaco": (-6.7, -2.3),
    "livro_atlas": (-6.7, -7.0), "livro_mascara": (6.7, -7.0),
    "livro_uivo": (-6.7, -13.0), "livro_noite": (6.7, -13.0),
    "livro_foice": (-6.7, -19.0), "livro_cerbero": (6.7, -19.0), "livro_asas": (6.7, -20.4),
}
MACACO = ("Contam os monges do Porto que o Rei Macaco Vermelho, cansado de lutar, quebrou o próprio bastão em cinco esferas "
          "alaranjadas e as jogou do céu.\n\nUma caiu na Costa, perto de onde o mar guarda segredos. Outra na Mata, entre os "
          "enforcados. A terceira nos Cânions, a quarta no Brejo da Viúva e a última em Véspera — sempre junto dos lugares que "
          "ninguém visita.\n\nQuem juntar as cinco ouve de novo o riso do Rei e ganha a força dele. O Velho Wu, no Porto, sabe o resto.")

def main():
    m = json.load(open(F, encoding="utf-8"))
    m["gates"] = [g for g in m.get("gates", []) if not g["id"].startswith("grade_ala")]
    m["objects"] = [o for o in m["objects"] if not o["id"].startswith("placa_ala") and o["id"] != "livro_macaco"]
    m["objects"].append({"id": "livro_macaco", "kind": "livro", "x": 0, "y": 0, "label": "A Lenda do Rei Macaco", "text": MACACO})
    for o in m["objects"]:
        if o["id"] in LUGAR: o["x"], o["y"] = LUGAR[o["id"]]
    for n, y, h, nivel, missao in ALAS:
        for lado, x in (("o", -4.35), ("l", 4.35)):
            m["gates"].append({"id": f"grade_ala{n}_{lado}", "flag": f"biblio:ala{n}", "x": x, "y": y, "w": 0.3, "h": h,
                               "lever_x": round(x * 0.8, 2), "lever_y": round(y + h / 2 - 0.5, 2), "kind": "portao", "lever": False})
            m["objects"].append({"id": f"placa_ala{n}_{lado}", "kind": "contemplar", "x": round(x * 0.78, 2), "y": round(y + h / 2 - 0.6, 2),
                                 "label": f"Grade da Ala {'I' * n if n < 4 else 'IV'}",
                                 "text": f"Ala {'I' * n if n < 4 else 'IV'} — fechada. Abre no nível {nivel}, depois da missão \"{missao}\" "
                                         f"da Irmã Clarabela (aqui na entrada). Volte quando cumprir as duas coisas."})
    json.dump(m, open(F, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print("biblioteca: 4 alas gradeadas, 12 livros")

main()
