<#
.SYNOPSIS
  Monta o zip do jogo para um amigo (11/11): só o que o cliente precisa, sem servidor, bancos, backups nem o cache .godot.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\Tools\pacote_amigo.ps1 -Conta alice -Servidor 26.12.34.56
  Gera "CinzasDaMare_Amigo.zip" na Área de Trabalho, já com "Entrar no servidor.cmd" (IP e token da conta prontos).
#>
param(
    [string]$Conta = "alice",
    [string]$Servidor = "",
    [int]$Porta = 7777
)
$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location $raiz

# token da conta (e6-tokens.json ou contas-locais.json)
$token = $null
foreach ($arq in @("contas-locais.json", "e6-tokens.json")) {
    if (Test-Path $arq) {
        $obj = Get-Content $arq -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($obj.PSObject.Properties.Name -contains $Conta) { $token = [string]$obj.$Conta; break }
    }
}
if (-not $token) { throw "Não achei o token da conta '$Conta'. Crie uma: Jogar.cmd -NovaConta $Conta" }

$tmp = Join-Path $env:TEMP "CinzasDaMare_Amigo"
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
$destino = Join-Path $tmp "CinzasDaMare"
New-Item -ItemType Directory -Path $destino | Out-Null

Write-Host "Copiando o cliente (sem o cache .godot)..."
robocopy "Client" (Join-Path $destino "Client") /E /XD ".godot" "bin" "obj" /XF "*.bak" /NFL /NDL /NJH /NJS | Out-Null
robocopy "Shared" (Join-Path $destino "Shared") /E /XD "bin" "obj" /NFL /NDL /NJH /NJS | Out-Null
# cliente já compilado (o amigo não precisa compilar nada)
$bin = "Client\.godot\mono\temp\bin"
if (-not (Test-Path "$bin\Debug\CinzasDaMare.Client.dll")) { throw "Rode Jogar.cmd uma vez antes (o cliente ainda não foi compilado)." }
robocopy $bin (Join-Path $destino $bin) /E /NFL /NDL /NJH /NJS | Out-Null
# o Godot vai junto (a pasta inteira: o .exe e a GodotSharp), assim o amigo só instala o .NET
$cfg = if (Test-Path "Tools\jogar.config.json") { Get-Content "Tools\jogar.config.json" -Raw | ConvertFrom-Json } else { $null }
if ($cfg -and $cfg.godot -and (Test-Path $cfg.godot)) {
    Write-Host "Copiando o Godot..."
    robocopy (Split-Path $cfg.godot -Parent) (Join-Path $destino "Godot") /E /NFL /NDL /NJH /NJS | Out-Null
} else { Write-Host "Não achei o Godot (rode Jogar.cmd uma vez); o amigo vai precisar baixar o Godot 4.7.2 .NET." }
New-Item -ItemType Directory -Path (Join-Path $destino "Tools") | Out-Null
Copy-Item "Tools\jogar.ps1" (Join-Path $destino "Tools\jogar.ps1")
Copy-Item "Jogar.cmd" (Join-Path $destino "Jogar.cmd")

$ip = if ($Servidor) { $Servidor } else { "COLOQUE_O_IP_AQUI" }
$entrar = "@echo off`r`nrem Entra no servidor do seu amigo (conta $Conta)`r`n" +
          "powershell -NoProfile -ExecutionPolicy Bypass -File `"%~dp0Tools\jogar.ps1`" -Servidor $ip -Porta $Porta -Token $token`r`n" +
          "if errorlevel 1 pause`r`n"
Set-Content -Path (Join-Path $destino "Entrar no servidor.cmd") -Value $entrar -Encoding ASCII
$leia = @"
Cinzas da Maré — como entrar no servidor do seu amigo

1. Instale o .NET 8 (SDK):  https://dotnet.microsoft.com/download/dotnet/8.0
2. O Godot já vem junto (pasta Godot). Não precisa baixar nada além do .NET.
3. Dois cliques em "Entrar no servidor.cmd".
   (Se o endereço mudou, abra o .cmd no Bloco de Notas e troque o que vem depois de -Servidor e de -Porta.)

Conta: $Conta  (não passe o token para mais ninguém)
"@
Set-Content -Path (Join-Path $destino "LEIA_AMIGO.txt") -Value $leia -Encoding UTF8

$zip = Join-Path ([Environment]::GetFolderPath("Desktop")) "CinzasDaMare_Amigo.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Write-Host "Compactando..."
Compress-Archive -Path $destino -DestinationPath $zip -CompressionLevel Optimal
Remove-Item $tmp -Recurse -Force
$mb = [math]::Round((Get-Item $zip).Length / 1MB)
Write-Host "Pronto: $zip ($mb MB). Mande para o seu amigo."
