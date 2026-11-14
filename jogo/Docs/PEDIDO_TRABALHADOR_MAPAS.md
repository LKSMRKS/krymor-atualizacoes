# Pedido para o trabalhador de arte — peças básicas por área (13/11)

Formato igual aos pacotes anteriores: `.glb` com textura PBR (2048 no máximo, JPEG), escala real em metros, origem no chão.
Peças de cenário vão em `Assets/Models/cenario/<area>_<peça>.glb`; o editor de mapa já aceita `glb:cenario/<nome>`.
Peças grandes (acima de 4 m) mandam também um `_colisao.glb` simples (caixas).
Orçamento por peça: até 5 mil triângulos (pequena), 15 mil (média) e 40 mil (marco grande, uma por mapa).

## Prioridade 1 — o que o jogador mais vê

### Costa do Cal (1–13)
- Placa de madeira com seta, que aponta caminho (3 variações).
- Poço de pedra com balde.
- Barraca de pescador com redes penduradas.
- Ruína de muro de cal em L (2 tamanhos).
- Farol pequeno quebrado: marco do ponto secreto, no canto nordeste.
- Fogueira de acampamento com troncos e panela.

### Vale das Cinzas (11–16)
- Árvore seca queimada (3 formas).
- Carroça de pedreiro com blocos.
- Estátua quebrada de guerreiro, ajoelhada: marco do vale.
- Pedra de altar com runas: ponto secreto.

### Mata dos Enforcados (17–26)
- Árvore retorcida com corda pendurada (2 variações).
- Moinho de vento pequeno: marco da vila.
- Cerca de roça de madeira em módulo de 3 m.
- Toca de aranha: teia em arco sobre a trilha.
- Poço tampado com tábuas.

### Cânions dos Suplicantes (20–36)
- Elevador de mina de madeira, com cabos: marco.
- Lamparina de mineiro no chão.
- Vagonete tombado.
- Estátua de suplicante, um monge ajoelhado (2 poses).
- Ponte de corda em módulo de 6 m.

## Prioridade 2

### Brejo da Viúva (28–42)
- Casa sobre palafitas: a vila suspensa.
- Trilha de tábuas em módulo de 4 m (reta e curva).
- Árvore de mangue com raízes.
- Barco a remo afundado pela metade.
- Túmulo da Viúva com flores: marco.

### Véspera (35–50)
- Lápide torta (4 variações).
- Sino rachado no chão (pode ser o `dg_sino_rachado` em escala 1,5).
- Casa abandonada com janelas tapadas (2 tamanhos).
- Banca de mercado vazia, com toldo rasgado.
- Poste com lanterna apagada.

### Coroa da Geada (42–56)
- Tenda de caravana com peles.
- Trenó de carga.
- Runa de gelo em pé: as runas do Juramento.
- Árvore com neve (3 formas).
- Muralha de gelo em módulo de 4 m.

## Prioridade 3

### Mar de Escória (48–62)
- Rocha de basalto com veio de brasa acesa (3 formas).
- Bigorna grande do Mestre da Forja.
- Ponte de corrente: marco para a Acrópole.
- Ninho de fênix em cima de rocha.

### Acrópole das Cinzas (55–66)
- Coluna grega quebrada (2 alturas).
- Estátua de juramento com braço erguido.
- Escadaria em módulo.
- Braseiro de bronze.

## Dungeons (variedade: 4 tipos de corredor)
- **Mina** (Galeria, Cânions): já entregue (`dg_*`).
- **Cripta** (Cripta do Sino, Castelo de Véspera): já entregue (`dg_*`).
- **Templo alagado** (Mosteiro Afundado):
  - coluna com musgo;
  - piso quebrado com água;
  - altar submerso.
- **Forja** (Fornalha, Cidadela de Escória):
  - caldeirão de metal;
  - correntes;
  - grade de ferro;
  - poça de lava (plana, emissiva).
- **Gelo** (Salão e Fortaleza da Geada):
  - estalactite de gelo;
  - trono de gelo;
  - pilar rúnico.
- **Labirinto** (Labirinto do Juramento):
  - parede de sebe/bronze em módulo de 4 m;
  - estátua de minotauro;
  - porta de bronze.
- **Para todas**:
  - placa "→ Chefe" e "→ Saída", que o jogo já aponta na bússola;
  - porta de chefe trancada / aberta.

## Pontos de interesse (um a cada ~40 m nos mapas de campo)
Cada região precisa de 4 a 6 "lugares com nome", e cada lugar é a combinação de 2 ou 3 peças acima mais um marco. Exemplos:
- acampamento abandonado;
- altar;
- ruína;
- mirante;
- cemitério pequeno;
- ninho de chefe.

Os nomes e as posições eu ponho no mapa quando as peças chegarem.

## Também ficaram faltando (fora de mapa)
- **Montarias:** Javali de Guerra (nível 40) e Grifo (nível 55), no mesmo padrão das 8 que você mandou (`montarias/montaria_<id>.glb` + `_info.json`, clipes idle/walk/run/hit, altura da sela).
- **Casas pequenas por dentro:** 1 interior simples de 6×6 m (mesa, cama, baú, lareira pequena, janela), que serve para todas as portinhas do Porto.
- **Decoração da casa em 3D** (hoje é feita de peças simples): tapete real, quadro do mar, lustre de cristal, aquário, estátua de leão, jukebox, troféu de chefe, trono, estante de troféus, lareira, vaso de samambaia e estandarte.
- **Quadro de recados:** mural de cortiça com papéis presos, para a praça do Porto.
