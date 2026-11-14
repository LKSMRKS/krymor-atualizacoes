# Origem dos ativos derivados

Derivados **redimensionados** (e recortados nas margens transparentes) das artes conceituais do pacote `RPG_Completo_Lotes_00_a_08`. Os originais ficam no pacote, fora do repositório.

| Derivado | Original | SHA-256 do original |
|---|---|---|
| `guerreiro.png` / `guerreiro_retrato.png` | `Lote_02/01_Ferro_de_Vigilia.png` | `7c89345a449d9f5c…` |
| `monstro_carrapato.png` | `Lote_05/01_Carrapato_de_Escoria.png` | `7408808b3fcff9e2…` |
| `icone_espada.png` | `Lote_02/05_Espada_de_Vigilia.png` | `c6fe976a98a170cc…` |
| `fundo_login.jpg` | `Lote_00/01_Direcao_visual_gameplay.png` | `e41077b693439f82…` |
| `icone_golpe.png`, `icone_guarda.png`, `icone_auto.png`, `icone_pocao.png` | `Lote_07/01_HUD.png` (recorte dos ícones da barra de habilidades do mockup; o algarismo da poção foi apagado) | `020de7f4f79d4348…` |

Não são modelos 3D nem sprites animados: são **uma pose só**, de frente, usados como cartazes (billboards) provisórios.
Se algum arquivo faltar, o cliente cai para as formas simples da E5 (nada quebra).
Os ícones do mockup têm ~50 px de origem (ampliados 3x): servem como provisórios; o ideal é um kit de ícones próprio, em resolução maior.

## Texturas procedurais (não vêm das artes)
`chao_pedra.jpg`, `chao_pedra_normal.png` e `parede_pedra.jpg` são **geradas por código** (`Tools/gerar_texturas.py`, determinístico, precisa de numpy e Pillow).
São provisórias: um kit PBR próprio do cenário é o ideal (ver `Docs/ASSETS_E7.md`).

| `icone_frasco.png` | Gerado por código (PIL) em 08/11 a pedido do usuário: frasco de vida **cilíndrico** no lugar do redondo. Não é arte do usuário. | — |
