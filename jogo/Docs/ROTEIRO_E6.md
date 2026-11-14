# Roteiro de aceite E6 — dois clientes reais

**Situação: NÃO EXECUTADO.** Nenhum cenário abaixo está aprovado. Marque você mesmo ao testar e me envie o resultado.
Leia antes: os testes automáticos já cobrem a lógica (coleta concorrente, repetição de requisição, reinício, dano forjado), e você já
validou um cliente jogando. Este roteiro confere o que só aparece com **duas pessoas/janelas reais ao mesmo tempo**.

## 0. Preparação (uma vez)
Abra o PowerShell na pasta do projeto e defina o caminho do Godot (pode trocar pelo seu):
```powershell
cd "C:\Users\LKS X\Documents\JOGOS\CinzasDaMare"
$env:CDM_GODOT = "C:\caminho\Godot_v4.7.2-stable_mono_win64_console.exe"
powershell -ExecutionPolicy Bypass -File .\Tools\e6.ps1 -Acao Preparar
```
Isso compila servidor e cliente e cria um banco novo `e6.db` com as contas **alice** e **bruno**.
(Se o script falhar, o passo a passo manual está em `Docs/STATUS.md`, "Próximo comando". Me diga o erro.)

## 1. Abrir o jogo
1. **Janela A (servidor):** `powershell -ExecutionPolicy Bypass -File .\Tools\e6.ps1 -Acao Servidor` — deixe aberta.
2. **Janela B:** `powershell -ExecutionPolicy Bypass -File .\Tools\e6.ps1 -Acao Cliente -Quem alice`
3. **Janela B de novo:** `powershell -ExecutionPolicy Bypass -File .\Tools\e6.ps1 -Acao Cliente -Quem bruno`

Aparecem duas janelas do jogo lado a lado. Cápsula **azul** = o personagem da própria janela; **verde** = o outro.
Teclas: WASD mover · mouse mirar · Q golpe · E guarda · R ataque automático · F pegar item · 1 poção · G/H equipar/desequipar.

## 2. Cenários

### A. Sincronização entre as duas janelas
1. Nas duas janelas aparecem os dois personagens (nome, nível, vida).
2. Alice anda: na janela do Bruno a cápsula verde se move (um pequeno atraso é normal).
3. Alice mexe o mouse: o "nariz" branco do avatar dela gira na janela do Bruno.
4. Bruno vai até o monstro (caixa vermelha) e aperta **R**: a vida do monstro cai **igual** nas duas janelas.
5. Alice aperta **E**: Bruno vê o escudo azul na frente dela por cerca de 3 s.

**Esperado:** tudo que um faz aparece no outro em até ~1 s; a vida do monstro é a mesma nas duas telas.
Resultado: ☐ Aprovado  ☐ Falhou — Observações: ________________

### B. Disputa pelo item (só um leva)
*(banco novo: a espada ainda está no chão, caixa amarela perto do ponto de partida)*
1. Deixe os dois perto da espada, janelas lado a lado.
2. Combinem "já!" e apertem **F** o mais juntos possível.

**Esperado:** exatamente um lê "Item coletado." e vê a Espada na Mochila; o outro lê "Coleta falhou: já foi pego."
A caixa amarela some nas duas janelas. **Nunca** pode haver duas espadas.
Para repetir com outro momento: feche tudo, `-Acao Limpar`, `-Acao Preparar` e comece de novo (a espada volta com banco novo).
Resultado: ☐ Aprovado  ☐ Falhou — Quem levou: ________  Observações: ________________

### C. Desconexão e reconexão
1. **Bruno fecha a janela** do jogo.
   **Esperado:** na janela da Alice, o Bruno some em cerca de 1 s; o servidor continua normal.
2. Reabra o Bruno (`-Acao Cliente -Quem bruno`).
   **Esperado:** entra com o **mesmo** personagem (nível, XP, mochila, arma) e perto de onde saiu; Alice o vê de volta.
3. *(opcional)* Com o Bruno aberto, abra uma **segunda** janela dele.
   **Esperado:** a segunda é recusada ("Login recusado: already_logged_in") e a primeira segue jogando.

Resultado: ☐ Aprovado  ☐ Falhou — Observações: ________________

### D. Persistência depois de reiniciar o servidor
1. Antes: com a Alice, pegue e equipe a espada (**F**, **G**), derrote o monstro (XP sobe) e use uma poção (**1**).
   Anote de cada jogador: nível, XP, vida, mochila, arma e posição aproximada.
2. Na **janela A (servidor)** aperte **Ctrl+C**. Deve aparecer "Servidor encerrado; personagens salvos."
   As janelas do jogo mostram "Desconectado do servidor." e voltam ao painel de login.
3. Suba o servidor de novo (`-Acao Servidor`) e clique **Conectar** nas duas janelas (o token continua preenchido).

**Esperado:** mesmos nível/XP, mesma mochila e arma equipada, mesma quantidade de poções, posição e vida iguais (ou quase);
a espada **não** reaparece no chão. O monstro volta vivo com vida cheia (ele não é salvo — isso é esperado).
Resultado: ☐ Aprovado  ☐ Falhou — Observações: ________________

### E. *(opcional)* Queda brusca do servidor
Feche a janela do servidor no **X** (sem Ctrl+C), suba de novo e reconecte.
**Esperado:** nível, XP, itens e poções preservados (são salvos na hora). Posição e vida podem voltar **até 30 s**
(autosave a cada 30 s) — isso é o comportamento atual, não um defeito.
Resultado: ☐ Aprovado  ☐ Falhou — Observações: ________________

## 3. O que me enviar
Para cada cenário: Aprovado ou Falhou, o que você viu, e se falhou, o texto da janela do servidor e (se for o cliente) o painel Output do Godot.
Com isso eu atualizo o `STATUS.md` e só então marco o Marco A como aceito.

## 4. Já coberto por testes automáticos (159 aprovados no seu PC)
| Assunto | Testes |
|---|---|
| Só um vencedor, sem duplicar, repetição idempotente | `ConcurrentClaimIntegrationTests`, `TcpIntegrationTests` |
| Personagem e item sobrevivem ao reinício do servidor | `ServerGameplayTests`, `ServerE4Tests` (equipamento) |
| Dano enviado pelo cliente é ignorado; mensagens de servidor vindas do cliente são recusadas | `ServerGameplayTests` |
| Guarda: −40% de frente, sem redução pelas costas | `FacingTests` |
