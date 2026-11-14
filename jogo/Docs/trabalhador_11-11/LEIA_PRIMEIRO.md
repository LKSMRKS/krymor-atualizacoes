# Cinzas da Maré: tudo o que o programador ainda não aplicou (11/11/2026)

Ignore os pacotes anteriores. Este junta tudo o que falta.

Copie a pasta `Client/` deste zip por cima da `Client/` do jogo. Ela só **adiciona** arquivos; faça backup antes.

**Não sobrescreva** os arquivos do programador: `GeodeCrabVisual.cs`, `HudV2.cs` e `OrbeHud.cs`. Os trechos de código vêm nos guias, para ele colar onde for o caso.

Ordem sugerida:
1. **GUIA_APLICAR.md**: o que estava pendente desde ontem:
   - goblins v2;
   - modelos 3D dos itens v2;
   - barra com a gárgula e o dragão segurando os potes;
   - praça do porto (`PracaDoPorto.cs`);
   - barril e lanterna da rua;
   - cabeças de animal (`CabecaAnimalGlb`);
   - cena da costa (`CenaPrintCosta.cs`);
   - 8 arquivos `_colisao.glb`.
2. **GUIA_ESCADA_CASA.md** (novo):
   - o boneco sobe a escada do casarão de verdade, sem teleporte;
   - o boneco para de afundar no assoalho.
3. **GUIA_NOITE.md**:
   - 59 armas com qualidades;
   - móveis e puzzles;
   - Carniçal Cáustico e Fantasma de Calcário;
   - contorno colorido dos NPCs;
   - 42 sons.

Ao terminar: `dotnet build`, abrir o Godot (para importar os .glb/.ogg) e testar:
- uma arma de cada tipo no boneco andando e atacando;
- subir e descer a escada do casarão;
- a barra de vida e mana.
