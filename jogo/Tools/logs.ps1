<#
.SYNOPSIS
  Resumo dos logs do Cinzas da Maré (pasta logs\ na raiz). Use depois de um problema e mande o resultado (ou peça ao
  Claude "analise os logs": ele lê a pasta logs\ diretamente).
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\Tools\logs.ps1            # resumo das últimas 24 h
  powershell -ExecutionPolicy Bypass -File .\Tools\logs.ps1 -Horas 2
#>
param([int]$Horas = 24, [int]$Linhas = 25)
$raiz = Split-Path -Parent $PSScriptRoot
$pasta = Join-Path $raiz "logs"
if (-not (Test-Path $pasta)) { Write-Host "Ainda não há logs em $pasta (rode o jogo pelo Jogar.cmd)."; return }

# copia o log do próprio Godot (erros do motor e de C# que não passaram pelo nosso log)
$godot = Join-Path $env:APPDATA "Godot\app_userdata\Cinzas da Maré\logs\godot.log"
if (Test-Path $godot) { Copy-Item $godot (Join-Path $pasta "godot-ultimo.log") -Force }

$desde = (Get-Date).AddHours(-$Horas)
$arquivos = Get-ChildItem $pasta -Filter "*.log" | Where-Object { $_.LastWriteTime -ge $desde } | Sort-Object LastWriteTime
if (-not $arquivos) { Write-Host "Nenhum log nas últimas $Horas h."; return }
Write-Host "Logs das últimas $Horas h em $pasta"
foreach ($a in $arquivos) {
    $texto = Get-Content $a.FullName -Encoding UTF8
    $erros = @($texto | Where-Object { $_ -match '^\S+ \S+ ERRO' }).Count
    $avisos = @($texto | Where-Object { $_ -match '^\S+ \S+ AVISO' }).Count
    "{0,-45} {1,6} linhas  {2,4} erros  {3,4} avisos" -f $a.Name, $texto.Count, $erros, $avisos
}
Write-Host ""
Write-Host "Últimos erros e avisos (com a pilha):"
foreach ($a in $arquivos) {
    $texto = Get-Content $a.FullName -Encoding UTF8
    $saida = @()
    for ($i = 0; $i -lt $texto.Count; $i++) {
        if ($texto[$i] -match '^\S+ \S+ (ERRO|AVISO)') {
            $saida += "$($a.Name): $($texto[$i])"
            $j = $i + 1
            while ($j -lt $texto.Count -and $texto[$j].StartsWith("  | ") -and $j -le $i + 8) { $saida += "    $($texto[$j])"; $j++ }
        }
    }
    $saida | Select-Object -Last $Linhas
}
