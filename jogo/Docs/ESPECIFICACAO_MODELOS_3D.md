# Especificação dos modelos 3D animados (E7b): um Guerreiro e um monstro

> **ADIADA (07/11/2026, decisão do usuário):** a prioridade passou a ser o protótipo jogável com bonecos articulados de
> formas simples (`Docs/PROTOTIPO_E8.md`). Esta especificação continua válida para quando os modelos forem feitos;
> a integração entra como um novo `IEntityVisual` (Client/Visuals), sem mexer em regras, rede ou inventário.

**Situação: nenhum modelo 3D existe.** O pacote de artes tem só imagens 2D. Os cartazes do jogo são ilustrações de uma pose, **não são modelos**.
Este documento é o "briefing" completo: serve para encomendar, para gerar por ferramenta ou para escolher um pacote pronto.
Escopo desta etapa: **apenas** o Guerreiro e o Carrapato de Escória (monstro da arena). As outras classes e monstros ficam para depois.

## 1. O que falta, exatamente
| # | Item | Existe hoje? | Observação |
|---|---|---|---|
| 1 | Modelo 3D do Guerreiro (malha, UV, texturas PBR) | **Não** | só a arte frontal `Lote_02/01_Ferro_de_Vigilia.png` |
| 2 | Esqueleto (rig) humanoide e pesos de pele do Guerreiro | **Não** | |
| 3 | Peças separadas do Guerreiro: espada, escudo, elmo | **Não** | a espada existe só como imagem (`Lote_02/05`); o escudo só aparece pintado no Lote 0 |
| 4 | 11 animações do Guerreiro (seção 4) | **Não** | |
| 5 | Modelo 3D do Carrapato de Escória (malha, UV, texturas PBR) | **Não** | só a arte `Lote_05/01_Carrapato_de_Escoria.png` |
| 6 | Esqueleto, pesos e 5 animações do Carrapato (seção 5) | **Não** | |
| 7 | Vistas de lado e de costas (as artes só têm a frente) | **Não** | quem modelar precisa criá-las e **mostrar para aprovação antes de modelar** |

## 2. Formato e convenções (valem para os dois)
- **Arquivo:** glTF 2.0 binário (`.glb`), **um arquivo por criatura**, com malha, esqueleto, materiais, texturas **embutidas** e **todas as animações** dentro: `guerreiro.glb` e `carrapato.glb`. O Godot lê esse formato direto.
- **Unidades e eixos:** 1 unidade = 1 metro; Y para cima; a criatura **olha para +Z** (convenção do glTF). Origem **no chão, entre os pés**; ao andar em linha reta sem raiz móvel, o modelo não se desloca (animações *in place*, sem *root motion*).
- **Escala aplicada:** nenhuma escala ou rotação pendente nos nós (exporte com transformações aplicadas, escala 1,0).
- **Altura:** Guerreiro **1,85 a 1,95 m** (com elmo). Carrapato **1,5 a 2,2 m de largura** e **1,0 a 1,4 m de altura** (a arte mostra ~1,6 : 1).
- **Triângulos (total, PC):** Guerreiro 15 mil a 40 mil. Carrapato 10 mil a 30 mil. Sem LODs nesta etapa.
- **Materiais:** no máximo **3** por criatura. PBR metal-rugosidade: cor base, **normal (OpenGL, +Y)** e **ORM empacotado** (oclusão, rugosidade, metal). Texturas até **2048 × 2048**, PNG ou JPEG.
- **Esqueleto:** até 4 influências por vértice. Guerreiro: humanoide padrão (quadril, coluna, peito, pescoço, cabeça, braços, antebraços, mãos, coxas, canelas, pés), de **18 a 80 ossos**. Nomes do Mixamo (`mixamorig:...`) ou do tipo `hips, spine, chest, neck, head, upperarm_l/r, lowerarm_l/r, hand_l/r, upperleg_l/r, lowerleg_l/r, foot_l/r` são aceitos.
- **Pose de repouso do Guerreiro:** **A-pose** (braços a ~45° do corpo), mãos levemente fechadas.
- **Estilo:** fantasia medieval sombria, materiais gastos (aço riscado, couro, tecido vermelho desbotado), como nas artes dos lotes 0, 2 e 5. Nada de cores saturadas ou aparência de desenho animado.

## 3. Guerreiro: modelo e peças
- **Referência:** `Lote_02/01_Ferro_de_Vigilia.png` (armadura inicial: elmo aberto, ombreiras, peitoral de placas, saiote acolchoado vermelho, cintos, joelheiras, botas) e `Lote_00/01_Direcao_visual_gameplay.png` (escudo redondo e espada em uso, capa vermelha).
- **Malhas separadas, nesta nomenclatura (nomes dos nós):**
  - `Guerreiro_Corpo`: corpo e armadura, **uma malha com pele** (skinned).
  - `Elmo`: elmo separado (permite trocar no futuro). Opcional nesta etapa: se vier junto ao corpo, avise.
  - `Espada`: lâmina de aço, ~0,95 m (referência `Lote_02/05_Espada_de_Vigilia.png`). **Nó-filho do osso da mão direita**, já posicionada na mão na pose de repouso.
  - `Escudo`: escudo redondo de madeira e ferro, ~0,65 m de diâmetro. **Nó-filho do osso do antebraço esquerdo**, já posicionado.
  - `Capa`: opcional (tecido vermelho curto). Se entrar, com ossos simples e sem física.
- Cada peça com material próprio ou compartilhado, sem ultrapassar 3 materiais no total.

## 4. Guerreiro: animações (nomes exatos, 30 quadros por segundo, *in place*)
Os tempos vêm das regras já validadas do servidor (arquivos `Content/skills.json`, `items.json` e `balance.json`).
| Nome | Repete? | Duração | Quadro de impacto / detalhe | Ligação com o jogo |
|---|---|---|---|---|
| `idle` | sim | 2,0 a 3,0 s | respiração, peso no escudo | parado |
| `walk` | sim | 0,8 a 1,2 s | passo compatível com **2,5 m/s** | andar com a Guarda ativa (movimento a 50%) |
| `run` | sim | 0,5 a 0,8 s | passo compatível com **5 m/s** | velocidade normal de movimento |
| `attack_basic` | não | **até 0,9 s** | impacto em ~0,30 s | ataque básico (intervalo da espada: **1,0 s**) |
| `attack_golpe` | não | **0,70 s** | **impacto em 0,30 s** | Golpe de Cunha: conjuração 300 ms + recuperação 400 ms |
| `guard_start` | não | 0,15 a 0,35 s | escudo sobe à frente | Guarda de Escudo ativada |
| `guard_loop` | sim | 1,0 a 2,0 s | escudo firme, leve oscilação | Guarda ativa (dura até **3 s**) |
| `guard_end` | não | 0,15 a 0,35 s | escudo desce | fim da Guarda |
| `hit` | não | 0,25 a 0,45 s | recuo curto | levar dano |
| `death` | não | 1,0 a 1,6 s | cai e **fica no chão** no último quadro | morte (renasce depois de 5 s) |
| `drink` | não | 0,7 a 1,0 s | leva o frasco à boca | usar poção (a cura dura 4 s, a animação é só o gesto) |
Opcionais, se houver tempo: `cast` (carga rápida), `pickup` (agachar e pegar, 0,8 s), `respawn` (levantar, 1,0 s).
Cada animação que repete deve **fechar o laço sem salto** (primeiro e último quadros iguais).

## 5. Carrapato de Escória: modelo e animações
- **Referência:** `Lote_05/01_Carrapato_de_Escoria.png` (carapaça de placas enferrujadas, patas articuladas, par de garras na frente). Nome atual no conteúdo: Caranguejo de Cal (ver `Docs/ASSETS_E7.md`, seção 6).
- **Malha:** `Carrapato_Corpo`, **uma malha com pele**. Corpo achatado com carapaça, **6 a 8 patas** de 3 segmentos, **2 garras dianteiras** com mandíbula que abre e fecha. **Frente (garras) para +Z.** Pose de repouso: patas abertas, garras à frente, relaxado.
- **Esqueleto:** raiz, corpo, uma cadeia por pata, uma cadeia por garra (com osso de mandíbula). De **10 a 60 ossos**. Nomes livres, mas consistentes e legíveis (`leg_l1_a`, `claw_r_jaw`...).
| Nome | Repete? | Duração | Detalhe | Ligação com o jogo |
|---|---|---|---|---|
| `idle` | sim | 2,0 a 3,0 s | respira, garras mexem de leve | parado |
| `walk` | sim | 0,6 a 1,0 s | patas compatíveis com **2,5 m/s** | perseguir e voltar ao ponto de origem |
| `attack` | não | 0,8 a 1,2 s | **impacto em ~0,4 s** (garra desce) | ataque (intervalo do monstro: **2,0 s**) |
| `hit` | não | 0,25 a 0,45 s | encolhe | levar dano |
| `death` | não | 0,9 a 1,4 s | tomba de lado, patas se recolhem; **fica no chão** | morte (renasce em 20 s) |
Opcional: `spawn` (emergir do chão, 1,0 s) para o renascimento.

## 6. Como entregar
1. Coloque os arquivos em `Client/Assets/Models/` (`guerreiro.glb`, `carrapato.glb`).
2. Rode a conferência: `python Tools/validar_glb.py Client/Assets/Models/guerreiro.glb --tipo guerreiro` (e `--tipo carrapato`). Ela confere tamanho, origem, triângulos, ossos, nomes das peças, nomes e durações das animações, materiais e texturas, e diz PASSA, AVISO ou FALHA.
3. Me envie a saída da conferência, uma captura do modelo em pose de repouso (de frente, de lado e de costas) e a licença/autoria.
4. Eu integro: carrego o `.glb` em tempo de execução (sem passo de importação), ligo as animações aos **eventos do servidor** (andar, atacar, bloquear, levar dano, morrer) e mantenho o cartaz atual como reserva se o arquivo faltar.

## 7. Caminhos para obter os arquivos (a decidir; não verifiquei preço, prazo nem licença de nenhum)
| Caminho | Quando faz sentido | Cuidados |
|---|---|---|
| Encomendar a um artista 3D usando este documento como briefing | resultado final de maior qualidade | pedir as vistas de lado/costas para aprovação **antes** de modelar; combinar direitos de uso |
| Ferramenta de imagem para 3D e rigging automático, com limpeza manual | protótipo rápido a partir das artes | qualidade e topologia variam; a frente é conhecida, o resto é "inventado" pela ferramenta; **conferir os termos de licença** |
| Pacote pronto de personagem humanoide com animações (CC0 ou licença comercial) como **modelo provisório** | testar já a integração e a máquina de animações enquanto o definitivo é feito | estilo diferente das artes; serve para validar o encanamento, não o visual |
| Animações genéricas para humanoides (biblioteca pública) | acelerar o Guerreiro | não existe equivalente para o Carrapato; conferir licença e a compatibilidade do esqueleto |
Recomendação: **encomendar ou gerar o Guerreiro e o Carrapato com esta especificação** e, em paralelo, usar um humanoide provisório só para testar a integração.

## 8. Aceite (como saberemos que está pronto)
☐ A conferência `validar_glb.py` passa sem FALHA nos dois arquivos.
☐ O modelo abre no Godot sem erro e as animações têm os nomes exatos.
☐ No jogo: parado, andando, atacando, bloqueando, apanhando e morrendo mostram a animação certa, sincronizada com os eventos do servidor, sem deslizar os pés.
☐ Duas janelas continuam sincronizadas; contas, bancos e regras não mudam.
