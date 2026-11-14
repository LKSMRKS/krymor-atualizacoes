# O que falta criar (lista viva, 11/11/2026)

Lista para ir preenchendo aos poucos. Marque com [x] quando entrar no jogo.

**Objetivo:** variedade de drop como no MU.
- No começo cai pouca coisa e tudo "cru", sem atributo.
- Quanto mais forte o monstro, mais chance de vir com atributo.

## 0. Regras de drop (já valem no jogo)
- [x] **Loja vende item cru** (qualidade "comum", sem atributo).
- [x] **Drop de monstro até o nível 6: sempre cru.**
- [x] **Do nível 7 ao 20, a chance de vir melhor sobe aos poucos:**
  - incomum (1 atributo): até 20%;
  - raro (2 atributos): até 5%.
  - Constantes `PlainLootUntil`/`FullUpgradeAt` em `GameServer.Map.cs`.
- [x] **Excelente estilo MU** (feito 11/11: 0,5% a partir do nível 15, 3 atributos ×1,5, raio de luz e aviso no mapa) (item com brilho e 2–3 atributos especiais, ex.: +dano 2%, +vida ao matar, +velocidade de ataque): nova qualidade "excelente", rara (~0,5%), só a partir do nível 15.
- [x] **Item +1..+15 (refino com joia, feito 11/11: arraste a joia sobre o item; +7 em diante pode falhar):**
  - joia de refino com chance de falhar acima de +6;
  - o brilho da arma cresce com o +.
- [x] **Joias caem de qualquer monstro (feito 11/11)**; uso hoje = refino. Falta: atributo/reroll e troca — (Joia da Maré, Joia da Brasa, Joia do Vazio, que já têm modelo e ícone):
  - cada uma tem um uso fixo (refino, atributo, reroll);
  - todas caem raramente de qualquer monstro.

## 1. Armas (hoje: 109 itens, mas só 12 modelos 3D)
**Modelos 3D:** existem só espada, machado, cajado e escudo, cada um em t1–t3 (`Models/armas/`). Arco e talismã usam os bonecos de formas simples.

| Falta | Classe | Quantidade sugerida |
|---|---|---|
| [ ] Modelos de **arco** t1/t2/t3 (+ aljava) | Arqueiro | 3 + 1 |
| [ ] Modelos de **talismã/amuleto de mão** t1/t2/t3 | Bruxa | 3 |
| [ ] **Lança** t1/t2 (já existe "Lança de Vigia" sem modelo) | Guerreiro | 2 |
| [ ] **Martelo/malho** t1/t2 (já existe "Malho do Capataz") | Guerreiro | 2 |
| [ ] **Porrete** (Porrete de Deriva/Afogado) | Guerreiro | 1 |
| [ ] **Adaga** (2ª arma leve; Arqueiro e Bruxa) | Arq./Bruxa | 2 |
| [ ] **Varinha** (mão única, mais rápida que o cajado) | Mago | 2 |
| [ ] Espada t4/t5 e machado t4/t5 (nível 30+ e 45+) | Guerreiro | 4 |
| [ ] Cajado t4/t5 | Mago | 2 |

**Buraco de níveis:** entre o nível 11 e o 17 não há arma nova para nenhuma classe.
- [ ] Criar 1 arma por classe no **nível 14** (região Vale das Cinzas).

## 2. Armaduras e elmos (hoje: 51 armaduras/elmos de dados, sem modelo 3D próprio)
- [ ] **Modelos 3D de armadura**, 3 por tipo (leve/couro, malha, placa, manto), vestindo o manequim.
- [ ] **Elmos 3D**: gorro, capuz, elmo de ferro, elmo com penacho, elmo fechado.
- [x] **Luvas e botas** (11/11): espaços novos `luvas` e `botas`; 6 conjuntos do Meshy separados em capacete, peitoral, luvas e botas (`Tools/arte/armadura_meshy.py`, itens em `Tools/gerar_conjuntos.py`). Cada conjunto é de uma classe só.
- [ ] Calças (ainda vão junto do peitoral).
- [x] **Anel e colar** (11/11): espaços `anel1`, `anel2` e `colar`; 3 anéis e 2 colares (vida, ataque, regeneração).
- [ ] Atributos puros (força, destreza) em anel/colar: falta ligar o atributo do item ao cálculo de Força/Agilidade.
- [x] **Capa** no espaço de asas (asa OU capa): Capa de Viajante e Capa Rubra.
- [ ] Mesmo buraco de 11 → 17: armadura e elmo do **nível 14** para cada classe.

## 3. Escudos (hoje: 10, todos do Guerreiro)
- [ ] **Escudo leve (broquel)** para a Bruxa e para o Arqueiro: defesa menor, sem perder a esquiva.
- [ ] **Tomo/orbe de mão esquerda** para o Mago (+mana, +dano mágico).
- [ ] Escudo t4 com modelo próprio (nível 30+).

## 4. Ataques e poderes (hoje: 7 por classe + 1 evolução)
- [ ] **Livros nível 2**: só depois da 2ª classe.
  - Cada habilidade da 1ª classe ganha versão "II", mais forte e com um efeito a mais.
- [ ] **Golpes novos da 2ª classe**: 3 por classe (ver seção 5).
- [ ] **Combos do Meshy** (combo duplo, corte pela esquerda), item 62 da lista de prioridades.
- [ ] **Habilidade de arma lendária** (`proc_skill`): hoje só 4 têm. Meta: 1 por arma lendária.

## 5. Segunda classe (missão no nível 15) e final da classe (nível 60)
| Classe | 2ª classe | Final (nv 60, ainda não feito) |
|---|---|---|
| Guerreiro | Campeão | [ ] Senhor da Guerra |
| Mago | Arcanista | [ ] Arquimago da Brasa |
| Bruxa | Matriarca | [ ] Grã-Bruxa da Raiz |
| Arqueiro | Batedor | [ ] Caçador Sombrio |

- [ ] Final no nível 60: missão longa (dungeon), +atributos, 1 golpe final e um visual (asas/aura).

## 6. Monstros (pedidos marcados "depois")
- [ ] Espantalho que joga corvos (48).
- [ ] Anfíbio de lodo junto dos slimes (49).
- [ ] Monstros da cal: Gargulito no muro, Carniçal, Fantomata (50).
- [ ] Goblins com personagem e mecânica próprios. Hoje são só variação de skin.

## 7. Arte que ainda falta
- [ ] Mão de trás do dragão da barra da HUD em forma de garra.
- [ ] Ícones das armas/armaduras sem ícone pintado.
- [ ] Cidade com lógica (40) e praça (41): os assets já chegaram; a praça foi montada em 11/11.

## 8. Feito em 11/11 (madrugada)
- [x] Pedra de Portal (Téo, nível 7, vinculada): viaja de qualquer lugar para pedras já descobertas.
- [x] Missões novas: Ratos no Pão da Tropa, Cães Sarnentos, Resgate do Joca, A Cabeça do Capataz.
- [x] Missão do muro com barril de pólvora (pegar, pôr no chão, acender, correr).
- [x] Trilhas, placas, cercas quebradas, postes, tochas e montes de areia na Costa (`Tools/decorar_costa.py`).
- [x] Mais enfeites no Porto (floreiras, bancos, caixotes, carrinho, postes).
- [x] Fúria do Guerreiro só enche com o ataque básico.
- [x] Lista de monstros para o modelador (documento compartilhável).

## Feito em 11/11 (Atualização 08)
- [x] Refino +1..+15 com Joia da Maré/Brasa/Vazio; brilho da arma cresce com o +; aviso no servidor a partir de +9.
- [x] Excelente; raio de luz no chão para drop raro/Excelente/joia; aviso "fulano fez cair".
- [x] Apresentação do chefe (nome grande + rugido) e barra do chefe no topo.
- [x] Minimapa (tecla N).
- [x] Atualizador automático: o amigo baixa as mudanças do servidor do anfitrião sozinho e aplica ao abrir o jogo.
- [x] Pet junta material, consumível e joia a até 5 m do dono (equipamento não; o que você jogou fora, também não).
- [x] Chefe de campo com renascer ≥ 5 min vira evento: aviso para o servidor inteiro 5 min e 1 min antes, e quando aparece.
- [x] Contra-ataque: depois de aparar, o próximo golpe no mesmo monstro (até 1,5 s) é crítico garantido com dano ×1,6.
- [ ] Arma na mão sem atravessar o corpo (precisa ver no jogo).
- [ ] Animações novas, pular/escalar/nadar, golpe carregado, modelos por classe (fase 2).

## Pacote do trabalhador aplicado (11/11 à tarde, Atualização 09)
- [x] Escada do casarão: sobe degrau por degrau (altura visual no cliente), portal no topo, desce pelo vão no 2º andar; boneco não afunda no assoalho.
- [x] Arsenal: clava, lança e maça esculpidas no lugar dos bonecos simples; arcos e talismãs esculpidos; qualquer item novo pode usar `model_asset_id: "arma_*"`.
- [x] Móveis na Sua Casa (lareira com fogo e som, mesa, cadeiras, tapete, cama, estante, caixas, banqueta).
- [x] Carniçal Cáustico e Fantasma de Calcário (nível 8–9) na Galeria Funda, 6 pontos.
- [x] Contorno de NPC com o shader novo (branco / amarelo / verde).
- [x] Sons .ogg novos no lugar dos antigos (golpes, flecha, interface, magias, passos de areia/pedra/madeira, mar/vento/caverna/fogueira) e música calma do Porto, baixinha.
- [ ] Itens novos para as formas que ainda não existem no jogo: faca, adaga, espada curta/longa, katana, machado de guerra, arco curto/longo por qualidade, aljava, varinha da Bruxa (os 59 modelos já estão no jogo, falta decidir valores e onde caem).
- [ ] Puzzles (alavanca, placa de pressão, runa): modelos prontos; falta o servidor ter puzzle (abrir porta/baú).
- [x] (11/11 tarde) 53 armas do arsenal viraram itens (`Tools/gerar_arsenal.py`, cai 2% de qualquer monstro do nível; elite x3, chefe x10).
- [x] (11/11 tarde) Inimigos com movimento de peso: arrancam/freiam, viram aos poucos, cercam pelos lados, andam em volta do alvo esperando o golpe.
- [x] (11/11 tarde) Galeria Funda: 3 runas abrem a sala do tesouro (portão + baú `bau_tesouro_runas`); 4 placas de pressão com dardos.
- [x] (11/11 tarde) Máquina do Caos no Porto (estação "caos", receitas com `chance`; falhou = perde os ingredientes).
- [x] (11/11 tarde) Monstros da noite na Costa (`night: true` no ponto; `Tools/noite_costa.py`).
- [x] (11/11 tarde) Dungeon contra o tempo: recorde pessoal (`recorde:mapa:ms`) e ranking top 3 ao derrubar o chefe final.
- [x] (11/11 tarde) Conquistas e títulos (10 conquistas, marca `conquista:id`; título da mais nova embaixo do nome).
- [x] (11/11 tarde) Arma nas costas fora de combate (6 s sem golpe; volta para a mão no primeiro golpe).
- [x] (11/11 tarde) Troca entre jogadores (tecla U; mesa com itens e moedas, os dois confirmam; transação única no banco).
- [x] (11/11 tarde) Renascer (reset estilo MU) com o Mestre Dorval no nível 60: volta ao 1 com itens e atributos, +40 pontos.
- [ ] Facções com reputação; diálogo com escolha; emotes; lista de amigos.
- [x] (11/11 tarde) Pacote "Monstros e Armaduras": Rastejante de Gesso e Gargulitos na Galeria Funda; Guardião com corpo do Golem de Cal Viva; Espantalhos e Verme-Broca (elite) no Vale; Insetos do Lodo com as melecas; gárgulas no muro do cal e no portão do Porto (`Tools/monstros_lote3.py`).
- [x] (11/11 tarde) 6 armaduras lendárias (4 peças como na referência do MU: capacete; peitoral + calça; luvas + braçadeiras; botas), caem de chefes (8%) e elites (1,2%) do nível; ícones em `Tools/arte/render_icones_armadura.py`.
- [x] (11/11 tarde) Carniçais e fantasmas também de dia na Costa; 7 missões de caça dos monstros novos.
- [x] (11/11 tarde) Armadura em 5 partes como no MU: capacete, luvas (mãos e braços), peitoral (tronco), calça (coxas, espaço novo "calca"), botas. Conjuntos antigos re-separados (`Tools/arte/separar_calca.py`, originais em backups/armaduras_4pecas_2026-11-11).
- [x] (11/11 tarde) Atributos exclusivos por peça: luvas (velocidade de ataque, crítico), botas (velocidade, esquiva), peitoral (espinhos, redução de dano), capacete (recarga, XP), calça (fôlego, vida). Bônus de conjunto em 2/3/4/5 peças (crítico e dano dobrado).
- [x] (11/11 tarde) 11 armas lendárias do Meshy em 3 faixas (início nv 12-18, meio 26-36, fim 44-58); `Tools/arte/armas_meshy.py`.
- [x] (11/11 noite) Asas de classe nível 2 (Dragão/Guerreiro, Abismo/Mago, Águia/Arqueiro, Cristal/Bruxa), pintadas pelas referências, cada metade com dobradiça no ombro e batida animada (`Tools/arte/asas_meshy.py`); compra no Santuário e receita na Máquina do Caos.
- [x] (11/11 noite) Santuário do Céu (só admin, painel de Deus → Viajar): 4 lojas com todos os itens (armas, armaduras, asas, consumíveis), forja, Máquina do Caos e arena de colunas para chamar monstros (`Tools/santuario.py`).
- [x] (11/11 noite) Corte das 6 armaduras refinado: peça inteira vai para onde está a maior parte; pedacinhos soltos abaixo do joelho → botas, entre joelho e cintura → calça (medido pelas próprias peças, vale para qualquer escala). Ícones refeitos.
- [ ] Asas nível 1 (ajustar depois, quando chegarem as referências).
- [x] (11/11 noite) Atributos com efeito: tenacidade, precisão, cura recebida, custo de mana, resistência a atordoamento, resistência mágica.
- [x] (11/11 noite) Santuário refeito simples (praça pequena com as 4 lojas, forja, Máquina do Caos e portais) + mapa à parte "Arena de Teste" com 12 bandos de níveis 1 a 60 (renascem em 8 s).
- [x] (11/11 noite) Bússola: missão em outro mapa aponta o portal do caminho; missão de "usar" aponta o objeto (pedra azul, armazém). Pedrinha menor e embaixo da faixa. Barra de vida do alvo aparecendo.
- [x] (11/11 noite, leva 2) Editor: ferramentas **Mover** (qualquer coisa: NPC, portal, pedra azul, baú, monstro, cenário) e **Girar** (cenário e NPC), salvando no mapa.
- [x] (11/11 noite, leva 2) Armas na mão: parado = guarda baixa (lâmina para a frente e para baixo, fora do corpo); andando = ponta para trás e para baixo; cajado e lança de pé. Ferreiro martela na frente do corpo.
- [x] (11/11 noite, leva 2) Classe 2: cabeça humana encolhe dentro da do bicho; bicho desce até o pescoço; Campeão (Guerreiro) mais largo.
- [x] (11/11 noite, leva 2) Voo com pés esticados (referência do anjo); asas antigas (ferro, neve) com os modelos novos.
- [x] (11/11 noite, leva 2) Goblin de skin verde +1 nível; estocada não quebra a postura de monstro de nível maior; missões bloqueadas na aba "Próximas missões".
- [x] (11/11 noite, leva 2) Contorno fino, só por fora do corpo e só com o mouse em cima (NPC e objetos); sem o quadrado das mesas.
- [x] (11/11 noite, leva 2) Capitã Ondina fora do meio da estrada; NPC sólido (não atravessa); cerca da Costa com colisão; sentar nos bancos da praça (F).
- [x] (11/11 noite, leva 2) Portal roxo com vórtex, parado; fumaça da morte redonda e macia; mapa (M) centralizado com zoom (rodinha) e arrastar.
- [x] (11/11 noite, leva 2) 3 gárgulas vivas na Costa (portão, final à direita, muro rachado); Sibila diferente da Hazel; 11 gestos de NPC.
- [x] (11/11 noite, leva 2) Duelo: "O Marinheiro Rebelde" — Cael se rebela e te espera na Costa para um x1.
- [ ] Interação com NPC só ao mirar o mouse (hoje: o contorno já é só no mouse; o "F falar" ainda aparece por perto).
- [ ] Luz da praça (pedido para depois).
- [x] (11/11 noite, leva 3) Classe do item embaixo do nome (vermelha se não pode usar); quadro do item troca ao passar de uma casa para outra.
- [x] (11/11 noite, leva 3) Ícones próprios: sementes (saquinho, cor por planta), frasco vazio, resíduo de larva, rebites, ligas, chapa, couro, carne, resina, cera, chave, pluma, sela, regador... (antes todos viravam a flor da erva: a chave do ícone era o modelo genérico).
- [x] (11/11 noite, leva 3) Forja/bancada: moldura dourada ao passar o mouse no cartão; ícones aparecem quando ficam prontos.
- [x] (11/11 noite, leva 3) Vender: arrastar o equipado direto para a loja; botão direito vende (mochila e equipado); recompra pelo mesmo preço.
- [x] (11/11 noite, leva 3) Renasce na última pedra de viagem por onde passou (também em outro mapa); dungeon continua no checkpoint.
- [x] (11/11 noite, leva 3) Correr (Shift) para quando o fôlego acaba; precisa apertar Shift de novo.
- [x] (11/11 noite, leva 3) Fantasmas de cal nos cantos do mapa, flutuando (pernas de voo); menos caranguejos na área do javali da duna.
- [x] (11/11 noite, leva 3) Projétil de monstro para no fim do risco vermelho (passava 4 m).
- [x] (11/11 noite, leva 3) Horta: um campo de terra só com leiras (sem "hambúrgueres"); mudas nas leiras.
- [x] (11/11 noite, leva 3) Asas só na Máquina do Caos do Santuário Celeste (asa de ferro nível 15); batida das asas para a frente e para trás.
- [x] (11/11 noite, leva 3) Assoalho da taverna grande: o boneco não afunda mais.
- [x] (11/11 noite, leva 3) Editor v2 (interface): abas embaixo por categoria com miniatura de cada peça, clicar seleciona (anel dourado), arrastar move, menu girar/duplicar/maior/menor/apagar, campos editáveis, Salvar / Descartar / Testar / Desfazer (Ctrl+Z) / Voltar versão, Ctrl+C/V/D, Delete, R.
- [ ] Escada do casarão afastada da parede (precisa refazer o modelo; o gerador do casarão não está neste ambiente).
- [ ] Encaixe 100% do conjunto Cavaleiro Azul.
