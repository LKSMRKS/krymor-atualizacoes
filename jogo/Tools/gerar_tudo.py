#!/usr/bin/env python3
"""14/11: roda os geradores de conteúdo na ORDEM certa (um depende do outro). Uso: python3 Tools/gerar_tudo.py"""
import subprocess, sys, os
AQUI = os.path.dirname(os.path.abspath(__file__))
ORDEM = ["gerar_ramos2", "gerar_cidades", "gerar_merlin", "gerar_plantas_dg", "decorar_dungeons", "gerar_fabula", "gerar_hub_reinos", "ajustes_logica", "regular_missoes", "gerar_colecao_dono", "gerar_ambientacao", "alinhar_poder", "regular_economia"]
for g in ORDEM:
    r = subprocess.run([sys.executable, os.path.join(AQUI, g + ".py")], capture_output=True, text=True)
    print(f"{g}: {'ok' if r.returncode == 0 else 'ERRO'}  {r.stdout.strip().splitlines()[-1] if r.stdout.strip() else ''}")
    if r.returncode != 0:
        print(r.stderr); sys.exit(1)
