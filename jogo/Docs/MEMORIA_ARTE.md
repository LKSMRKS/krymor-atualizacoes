# Memória de arte — Cinzas da Maré (atualizado 09/11/2026)

## Regras do dono (não esquecer)
- O servidor decide tudo; arte e efeitos são do cliente. Não mexo em `.cs` existente sem pedido: entrego arquivos novos + ficha com código para o outro agente colar.
- Ids de conteúdo nunca mudam; dados de mapa só via `Tools/gerar_regioes.py`; migrações aditivas.
- Backup antes de substituir arquivo (pasta `backups/`).
- Toda entrega diz: implementado / executado / não testado / próximo comando.
- O dono gosta dos goblins atuais do jogo (pequeno e esqueleto): não substituir; os novos são para variações.
- Reaproveitar tudo com variações (nome, velocidade, ataque).
- Entrega final = .zip com `Client/` na estrutura do projeto + fichas; cópia em `entregas/` no projeto.

## Padrão técnico
- `.glb`, metros, frente +Z, origem no chão; PBR embutido (cor+AO, ORM, normal). Pipeline: `Tools/criaturas/pbr.py` (malha alta → bake).
- Criaturas: clipes idle/walk/attack_windup/attack_strike/attack_recover/hit/death (+ espécie). Humanoides: esqueleto Mixamo idêntico ao `manequim_masc`.
- Câmera do jogo: FOV 62, pitch 35,5°, 8,5 m; teste de leitura sempre nessa câmera (`render_game.py`, `render_scene.py`).
- Hook de cenário no cliente: `MapKit.Glb(node, "pasta/arquivo", pos, yaw, escala)` → `res://Assets/Models/<arquivo>.glb`.

## Entregas
- Lote 2 (antigo): caranguejos, Tirano, casas, armas, itens.
- Lote 3 (Costa jogável): caranguejos PBR, Rei das Pinças, Javali, 4 goblins Mixamo, barril, acampamento, farol, entrada da Galeria → `entregas/` + `FICHA_LOTE3.md`.
- **Cal Lote 1** (tapa artístico): texturas de chão, rochas, penhascos, vegetação, praia, ponte, ravina, lago, horizonte, shader de contorno → `entregas/CinzasDaMare_Costa_Arte_2026-11-09.zip` + `FICHA_COSTA_ARTE.md`.
- **Cal Lote 2** (entregue 09/11): texturas gen_* 1024 com desgaste; prédios do lote 2 com PBR procedural assado (rebake_lote2.py);
  cr_* PBR com os nomes que o jogo carrega (o jogo usava cópias antigas na raiz de Models!); barril explosivo real; gaivota animada;
  fogueira; ossada gigante; clipes Sentado/Mexendo/Conversando nos goblins; 14 pacotes de NPC (npcs/npc_<id>.glb, nós = nomes de osso);
  código novo: Ambiente/MarDaCosta.cs, CicloDoDia.cs, VidaAmbiente.cs + Shaders/mar_costa.gdshader → entregas/CinzasDaMare_Cal_Lote2_2026-11-09.zip + FICHA_CAL_LOTE2.md.
- Atenção: o jogo carrega modelos da RAIZ de Assets/Models com prefixos (cr_, proj_, cenario_, edificio_). Entregar sempre com esses nomes.

## Cal Lote 2 — pedido do dono (09/11 22:35)
1. Arte de verdade em monstros e NPCs (goblins/esqueleto já feitos no Lote 3 → faltam NPCs: 14 humanos com peça de destaque).
2. Vida no ambiente: gaivotas, caranguejinhos de enfeite, poeira no vento, folhas, espuma do mar, fogueiras com fumaça, NPCs fazendo coisas no acampamento.
3. Marcos de longe: farol aceso (feixe), fumaça do acampamento, ossada gigante na Enseada do Tirano.
4. Luz por hora do dia: fim de tarde dourado, noite com tochas.
5. Água de verdade: mar com ondas na borda e areia molhada perto.
6. Muros e casas com textura e desgaste (prédios do lote 2 no Porto).

- **HUD MU/Diablo** (10/11): ui_* refeitas em 3D (build_ui.py), mesmas margens; novas ui_slot_hab, ui_orbe_moldura, ui_orbe_vidro;
  OrbeHud.cs + orbe_liquido.gdshader (troca do GlassOrb) → entregas/CinzasDaMare_HUD_2026-11-10.zip + FICHA_HUD.md.
- **Estudos 01/02** (10/11, "ignora só a gameplay"): HUD v2 (build_ui2.py: barra inferior 1800×220, orbes 640 px FracRaio 196/640,
  barra chefe/alvo, bússola, placa de zona, aba, tooltip, XP, moldura do mapa); mapa ilustrado 2048² (build_mapa.py + mapa_post.py,
  −130..130, norte no topo) + névoa + ícones de legenda; cenário (torre partida, ponte de arcos, cais, carroça, vagonete/trilhos,
  blocos de cal, estandarte, tochas); 27 ícones 128² de itens/armas (render_icones.py) → entregas/CinzasDaMare_Estudos_2026-11-10.zip
  + FICHA_ESTUDOS.md. Backup do HUD v1 dentro do zip (backups/hud_v1_antes_do_v2).

- **Atualização 10/11 (zip final, substitui o Estudos)** → entregas/CinzasDaMare_Atualizacao_2026-11-10.zip + FICHA_ATUALIZACAO.md:
  itens v2 (itemkit.py + build_items2.py: modelo esculpido, material procedural, icone 128/256, glb ~4k tris);
  cabecas Meshy recortadas (cortar_cabeca.py; origem = osso Head, escala do manequim) + CabecaAnimalGlb.cs;
  goblins v2 no corpo do manequim (build_goblin2.py: cabeca+armas do v1, roupa nova, chefe reforcado);
  CenaPrintCosta.cs (cena do print costa_depois; origem (-31,49); conversao mapa_x=X0-bx, mapa_y=Y0+by, yaw+180) + ParedaoVisual + guia visual da praia.
  Ja copiados direto no jogo (com backup): cr_* PBR e cabeca_*.glb.
- ATENCAO: NPCs "verdes" nas previas = cor crua do manequim; pacotes de NPC sao so acessorios. O dono NAO quer NPC verde; moradores com roupa propria = proximo lote.
- O ultimo lote que o dono mandou ao programador foi o Cal Lote 2 (antes da atualizacao de 10/11).

## Pendências conhecidas
- Tirano da Maré (cr_tirano_da_mare) ainda é a versão do lote 2: refazer no padrão PBR (lista: 4–5 m, fase 2 com rachaduras em brasa).
- Casas visitáveis na cidade exigem mudança de mapa/colisão (lote próprio).
- Muralha da cidade é desenhada por código com gen_bloco (melhorou pela textura); um trecho .glb dedicado ainda não existe.

## Lista do dono 10/11 (10 pontos, prioridade dele)
1. Moveis de interior: mesa, cadeira, cama, balcao de taverna, prateleira, lareira, tapete, caixa de mercadorias.
2. Moradores: ferreiro, curandeira, pescador, guarda, criancas com roupa propria (corpo do manequim); animacoes martelar, varrer, sentar, conversar, dormir.
3. Arsenal grande e variado (faquinha, graveto, espada, machado, clava, katana, espada enferrujada, arcos...) com raridades (ruim -> excelente/full), drops por regiao;
   talisma/varinha da Bruxa (3 niveis); aljava. (lote 2 so tinha espada, machado, cajado, escudo)
4. Armaduras/elmos/roupas por corte: tunica, manto, capa com/sem capuz, peitoral; 3 niveis; no esqueleto do manequim (hoje roupa e pintada).
5. Pet antes do draco (~nv 10) com 2-3 evolucoes.  6. Montaria.
7. Kit da Galeria Funda (paredes de mina, trilhos, vigas, cristais, porta de chefe).
8. Kits das regioes como o da Costa: Vale, Mata, Brejo, Canion, Neve, Fornalha, Acropole (chao, rochas, arvores, ruinas, monstros).
9. Sons (passos, golpes, ambiente, musica): sem download aqui; dá para sintetizar.
10. Puzzle: alavanca, placa de pressao, runa que acende.
Tambem pendente: barra de HUD compacta (orbe + 6 slots numerados + orbe) igual a referencia; Tirano PBR.

## Próximos lotes (lista do dono, ordem)
- Lote 4 Personagem: cabeças de evolução, armaduras Guerreiro/Mago, animações do Mago, arcos, talismãs.
- Lote 5 Porto vivo: 14 NPCs (pacotes já feitos no Cal L2), casa do jogador; cais/UI/orbes/ícones de item JÁ entregues (Estudos).
- Lote 6 Galeria Funda: kit de dungeon, Larva, Olho Alado, Sentinela, miniboss, Guardião.
- Lote 7 Vale: Planta, Saci, Anão, Capataz, pedreira, armaduras Arqueiro/Bruxa, música/efeitos.
