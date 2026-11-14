# Cinzas da Maré (nome provisório)

RPG de ação online privado, progressão lenta, farm e PvE como foco.
Projeto secundário, desenvolvido em entregas pequenas.

**Jogar agora:** duplo clique em `Jogar.cmd` (compila se precisar, abre o servidor e o cliente com a conta alice).
Protótipo atual (E8, Vale das Cinzas): `Docs/PROTOTIPO_E8.md`.

- Retomar o trabalho: leia `Docs/STATUS.md`.
- Regras vigentes: `Docs/DECISOES_OFICIAIS.md` (prevalece sobre o GDD).
- Planejamento original: `Docs/GDD_Cinzas_da_Mare.md`.
- Ambiente (Godot/.NET): `Docs/AMBIENTE.md`.
- Validar: `dotnet restore; dotnet build -c Release; dotnet test -c Release --no-build` (na pasta da solução `CinzasDaMare.sln`).
- Referências visuais (PNG, conceito apenas): ficam no pacote `RPG_Completo_Lotes_00_a_08.zip`, fora do repositório.

- Servidor: `dotnet run -c Release --project .\ServerHost -- --provision <nome>` (cria conta/token) e depois `... -- --port 7777`
  (sobe o mapa do protótipo; `--arena` volta à arena do Marco A; `--list-accounts` lista contas e personagens).
- Cliente Godot (E5): `Client/README.md`.
- Aceite com dois clientes (E6): `Docs/ROTEIRO_E6.md` e `Tools/e6.ps1`.
