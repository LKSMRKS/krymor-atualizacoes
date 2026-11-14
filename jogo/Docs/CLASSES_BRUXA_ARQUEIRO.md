# Bruxa e Arqueiro, manequim e draco (08/11/2026)

## Classes
| | Bruxa | Arqueiro |
|---|---|---|
| Atributo / recurso | Sintonia / Éter (100, regenera como mana) | Destreza / Foco (100, regenera rápido; o básico devolve) |
| Básico (clique) | Lampejo Esmeralda (projétil das mãos) | Disparo (flecha) |
| Q | Toque Alquímico: cura o aliado mais ferido a até 10 m (ou você) e devolve 15% do fôlego/mana dele | Tiro de Emboscada: flecha pesada que interrompe; saindo das sombras causa o dobro |
| E (nível 3) | Barreira de Éter: escudo que absorve golpes por 6 s no aliado mais ferido | Passos Fantasma: 5 s escondido, 40% mais rápido, inimigos perdem o rastro; atacar revela |
| R (nível 5, inata) | Aura Primal: inimigos num raio de 6 m batem 30% mais fraco por 6 s | Olhar do Caçador: marca presas num raio de 18 m (+15% de dano por 10 s) e aponta lugares secretos próximos |
| T (evolução, nível 20) | Renovo da Matriarca: cura o grupo em volta | Chuva de Flechas: área no ponto mirado |
| Sem arma | Centelha | Pedra Arremessada |
| Caminhos do nível 50 | Guardiã (cura/barreira +30%) ou Maldição (+12% de dano; Aura mais forte) | Atirador (+20% em flechas) ou Sombra (emboscada ×1,5; Passos +2 s) |

- R: se a arma tiver habilidade própria, R usa a da arma; sem ela, a inata da classe.
- Quem cura ou protege um aliado em luta conta como participante (XP e loot).
- Decisões: não há escalada (subir em árvores/ruínas ficou fora, como já decidido); "detectar armadilhas" virou marcar
  presas e apontar segredos, porque o jogo não tem armadilhas. A Bruxa usa o frasco de mana (tecla 2) para o Éter.

## Conteúdo
- `Content/classes_novas.json`, `skills_novas.json`, `items_novas.json` (níveis 1–11), `quests_novas.json` (evolução).
- Níveis 17–60 e lendários: `Tools/gerar_regioes.py` (talismã, arco, vestes, gibão por faixa; 4 lendários; caminhos 50).
- Mentores no Porto: Hazel, a Bruxa (22, 3) e Kaelen, o Batedor (-24, 8).
- Recompensas de missão por classe: `@classe:guerreiro|mago|bruxa|arqueiro` (faltando uma parte, usa a última).

## Visual
- `Client/Visuals/MannequinVisual.cs`: manequim do usuário (Meshy + Mixamo), parado/andando/correndo do .glb; poses de ação
  por cima (modificador do esqueleto `ManequimPoser`). Roupas: camadas da própria malha com shader de roupa; a máscara de
  região (cabeça, peito, braço, coxa...) e a altura ficam em COLOR_0 (gravadas por `Tools/preparar_manequim.py`).
- Arco nas costas andando; na mão na hora do tiro. Bruxa: cabelo, diadema e névoa esmeralda/dourada na mão esquerda.
- Efeitos: bolha da barreira, número verde de cura, névoa no inimigo enfraquecido, losango vermelho no marcado,
  transparência no Passos Fantasma, flechas de verdade, chuva com marca no chão.
- Pequeno draco: `Client/Visuals/DracoVisual.cs` + `Assets/Models/draco.glb` (`Tools/rig_dragao.py`: 24 ossos, Pairar/Voar/Ataque).
- `--boneco-classico`: volta ao boneco de formas simples (para comparar ou se o .glb faltar).
- Vitrine: `--vitrine classes|base|pet [--vitrine-acao windup|golpe|recover|andar|correr]`.

## Verificação
- 268 testes (7 novos em `Tests/ClassesNovasTests.cs`: conteúdo, recompensas em 4 partes, cura, barreira, aura,
  emboscada furtiva, marca).
- Cliente: 0 erros/0 avisos. Capturas na vitrine e no jogo (lobby → Bruxa/Arqueiro no Porto → luta na Costa).
- Não testado: cura/barreira entre dois jogadores reais; ritmo da Bruxa sozinha nos níveis altos.
