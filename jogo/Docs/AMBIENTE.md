# Ambiente

## Versões confirmadas no PC do usuário (30/10/2026)
- .NET SDK **8.0.427** (fixado em `global.json`, `rollForward: latestPatch`).
- Godot **4.7.2.stable.mono** (edição .NET).
- Git: não encontrado (opcional; instalar com `winget install Git.Git` se quiser versionar).

## Compatibilidade (verificada por pesquisa, não por compilação)
- Pacotes Godot 4.7.x (GodotSharp / GodotSharpEditor 4.7.2) têm como alvo `net8.0`; o SDK 8.0.427 atende.
- Um SDK mais novo (9/10/11) pode compilar com alvo `net8.0`, mas o projeto fixa o 8.0.x para evitar surpresas.
- Estratégia: servidor, regras e testes (E2) são bibliotecas `net8.0` **sem dependência do Godot**, então compilam e testam só com `dotnet`. O projeto Godot (cliente) entra na E4 e só será dado como compatível depois de compilar na sua máquina.

## Dependências NuGet (restauradas na primeira execução; precisam de internet)
Microsoft.Data.Sqlite 8.0.11, Microsoft.NET.Test.Sdk 17.11.1, xunit 2.9.2, xunit.runner.visualstudio 2.8.2.

## Verificação rápida
```powershell
.\Tools\verificar-ambiente.ps1 -GodotPath "C:\caminho\Godot_v4.7.2-stable_mono_win64.exe"
```

## Cliente Godot (E5)
- Projeto em `Client/` (`CinzasDaMare.Client.csproj`, `Godot.NET.Sdk/4.7.2`, alvo `net8.0`). O pacote 4.7.2 estável existe no NuGet (verificado por pesquisa, não por compilação no PC do usuário).
- O build do Godot grava a saída em `Client/.godot/mono/temp/bin/Debug/`. Essa pasta **não** vai para o versionamento.
- Renderizador: GL Compatibility (roda em qualquer placa; dá para trocar depois).
- **Compatibilidade do cliente com o Godot .NET 4.7.2: validada** em 02/11/2026 (compila com 0 erros e 0 avisos, abre, conecta e joga com 1 cliente). Dois clientes ao mesmo tempo: aguardando o aceite E6.
