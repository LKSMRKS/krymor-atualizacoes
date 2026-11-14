<#
.SYNOPSIS
  Ajudante do aceite E6 (Windows PowerShell 5.1 ou PowerShell 7). Escrito pelo assistente e AINDA NÃO EXECUTADO.
.PARAMETER DatabasePath
  Arquivo do banco SQLite do teste (padrao: e6.db). Nao pode se chamar -Db: esse nome e apelido do parametro comum -Debug.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\Tools\e6.ps1 -Acao Preparar
  powershell -ExecutionPolicy Bypass -File .\Tools\e6.ps1 -Acao Servidor     # numa janela só para ele
  powershell -ExecutionPolicy Bypass -File .\Tools\e6.ps1 -Acao Cliente -Quem alice -Godot "C:\...\Godot_v4.7.2-stable_mono_win64_console.exe"
  powershell -ExecutionPolicy Bypass -File .\Tools\e6.ps1 -Acao Cliente -Quem bruno -Godot "C:\...\Godot_v4.7.2-stable_mono_win64_console.exe"
  powershell -ExecutionPolicy Bypass -File .\Tools\e6.ps1 -Acao Limpar       # recomeça do zero (servidor fechado)
  Dica: defina $env:CDM_GODOT uma vez para não repetir -Godot.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet("Preparar", "Servidor", "Cliente", "Limpar")][string]$Acao,
    [ValidateSet("alice", "bruno")][string]$Quem = "alice",
    [string]$Godot = $env:CDM_GODOT,
    [string]$DatabasePath = "e6.db",
    [int]$Porta = 7777
)

$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location $raiz
$arquivoTokens = Join-Path $raiz "e6-tokens.json"
$servidorExe = Join-Path $raiz "ServerHost\bin\Release\net8.0\CinzasDaMare.ServerHost.exe"

function Invoke-Dotnet {
    param([string[]]$Argumentos)
    & dotnet @Argumentos
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Argumentos -join ' ') falhou (código $LASTEXITCODE)." }
}

switch ($Acao) {
    "Preparar" {
        if (Test-Path $DatabasePath) { throw "Já existe '$DatabasePath'. Para recomeçar do zero: .\Tools\e6.ps1 -Acao Limpar" }
        Write-Host "Compilando servidor (Release) e cliente..."
        Invoke-Dotnet @("build", "-c", "Release", ".\ServerHost\CinzasDaMare.ServerHost.csproj")
        Invoke-Dotnet @("build", ".\Client\CinzasDaMare.Client.csproj")
        if (-not (Test-Path $servidorExe)) { throw "Não achei $servidorExe depois do build." }

        $tokens = @{}
        foreach ($nome in @("alice", "bruno")) {
            $saida = & $servidorExe --db $DatabasePath --provision $nome
            if ($LASTEXITCODE -ne 0) { throw "Falha ao criar a conta $nome." }
            $tokens[$nome] = ($saida | Where-Object { $_ -and $_.ToString().Trim() } | Select-Object -Last 1).ToString().Trim()
        }
        $tokens | ConvertTo-Json | Set-Content -Path $arquivoTokens -Encoding UTF8
        Write-Host "Pronto. Contas alice e bruno criadas. Tokens de TESTE em e6-tokens.json (não compartilhe, não versione)."
    }
    "Servidor" {
        if (-not (Test-Path $servidorExe)) { throw "Servidor não compilado. Rode primeiro: .\Tools\e6.ps1 -Acao Preparar" }
        if (-not (Test-Path $DatabasePath)) { throw "Banco '$DatabasePath' não existe. Rode primeiro: .\Tools\e6.ps1 -Acao Preparar" }
        Write-Host "Servidor em 127.0.0.1:$Porta. Ctrl+C encerra e salva os personagens."
        & $servidorExe --db $DatabasePath --port $Porta
    }
    "Cliente" {
        if (-not (Test-Path $arquivoTokens)) { throw "Sem tokens. Rode primeiro: .\Tools\e6.ps1 -Acao Preparar" }
        if (-not $Godot -or -not (Test-Path $Godot)) { throw 'Informe -Godot com o caminho do executável (ou defina $env:CDM_GODOT).' }
        $token = (Get-Content $arquivoTokens -Raw | ConvertFrom-Json).$Quem
        $posicao = if ($Quem -eq "alice") { "20,60" } else { "980,60" }
        # Aspas explicitas: o caminho pode ter espacos (ex.: "LKS X") e Start-Process nao as adiciona sozinho.
        $cliente = '"' + (Resolve-Path .\Client).Path + '"'
        Start-Process -FilePath $Godot -ArgumentList @(
            "--path", $cliente, "--position", $posicao, "--resolution", "940x600",
            "--", "--host", "127.0.0.1", "--port", "$Porta", "--token", $token, "--autoconnect")
        Write-Host "Cliente de $Quem aberto."
    }
    "Limpar" {
        foreach ($arquivo in @($DatabasePath, "$DatabasePath-wal", "$DatabasePath-shm", $arquivoTokens)) {
            if (Test-Path $arquivo) { Remove-Item $arquivo -Force; Write-Host "Removido $arquivo" }
        }
        Write-Host "Pronto. Rode -Acao Preparar para criar tudo de novo."
    }
}
