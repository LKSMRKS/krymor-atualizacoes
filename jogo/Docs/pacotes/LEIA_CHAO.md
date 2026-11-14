## Chão pintado, relevo suave e profundidade (referência do dono, 11/11)

Objetivo: textura simples e suave, que de perto é básica mas de longe fica harmônica; chão que não é totalmente reto; e o fundo ganhando a cor do céu. **Cada região continua com o seu chão:** a Costa segue com areia e cal, e a grama nova vale onde já era grama.

### A. Texturas (troca de arquivo, sem código)
`Client/Assets/Art/ter_grama.png`, `ter_grama_n.png`, `ter_terra.png` e `ter_terra_n.png` substituem as atuais. As antigas estão em `backups/chao_antes/`.
- **Grama:** verde-oliva claro, manchas largas, pinceladas, pontinhos de flor e manchas de capim seco.
- **Terra batida:** lisa, com pedrinhas e sem as rachaduras-minhoca.
- Emendam sozinhas, sem costura no ladrilho, e são do mesmo tamanho de antes (1024).
- Valem para Vale, Mata, Brejo, Cânions e Acrópole, que usam essas duas no `TerrainMaterial`.

### B. Profundidade (`Client/Ambiente/Profundidade.cs`, arquivo novo)
1. Em `MapView.AddEnvironment`, troque:
   ```csharp
   Root.AddChild(new WorldEnvironment { Environment = _env });
   ```
   por:
   ```csharp
   var we = new WorldEnvironment { Environment = _env }; Root.AddChild(we); Profundidade.Aplicar(_env, we, Map.Theme);
   ```
2. No ciclo do dia (onde está `_env.FogDensity = look.FogDensity * (...)`):
   ```csharp
   _env.FogDensity = Profundidade.Ativo(Map.Theme) ? Profundidade.Densidade(Map.Theme, Night) : look.FogDensity * (_env.VolumetricFogEnabled ? 1f : 1.6f);
   ```

O que muda:
- **Névoa de profundidade no lugar da exponencial:** fica limpa até uns 30 m e fecha no fundo, com a cor do céu.
- **Custo:** o mesmo de antes.
- **Gráfico Alto:** desfoque leve de longe, em qualidade baixa de bokeh.
- **Cavernas e interiores:** não muda nada.

### C. Relevo (`Client/Ambiente/Relevo.cs`, arquivo novo)
Ondulações largas, de até 0,5 m, só no visual. O servidor continua plano.
1. No começo do construtor do `MapView`, antes de montar terreno e objetos:
   ```csharp
   Relevo.Configurar(Map.Theme, (float)Map.HalfSize, Map.Instanced);
   ```
2. **Objetos e obstáculos acompanham o chão:** `private static Vector3 P(double x, double y, float h = 0f) => new((float)x, h + Relevo.Altura(x, y), (float)y);`
3. **Pés de jogadores, monstros e NPCs:** no `Visuals/EscadaCasa.Altura`, troque o `return 0f;` do fim por:
   ```csharp
   return Relevo.Altura(mapX, mapY);
   ```
4. **Itens no chão** (`Main.CreateItem`): `Position = new Vector3((float)item.X, Relevo.Altura(item.X, item.Y), (float)item.Y)`.
5. **Terreno:**
   - No `TerrainShader`, cole `Relevo.ShaderRelevo` logo depois das `uniform`, e troque o `void vertex() { ... }` atual por `Relevo.ShaderVertex`.
   - No `TerrainMaterial()`:
     ```csharp
     m.SetShaderParameter("relevo_amp", Relevo.Amplitude); m.SetShaderParameter("relevo_meia", Relevo.MeiaLargura);
     ```
   - A malha do chão precisa ter vértices: `new PlaneMesh { Size = new Vector2(size, size), SubdivideWidth = (int)(size / 2), SubdivideDepth = (int)(size / 2) }`. São uns 80 mil triângulos, leve.
6. **Não sobem** (continuam planos): cidade, interiores, grutas e a borda do mapa (o relevo some nos últimos 20 m, para o mar casar).
