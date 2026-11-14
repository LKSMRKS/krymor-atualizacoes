# Copia claude-config\ (skill e revisores) para .claude\ — o Claude Code só lê de .claude\.
# Uso (na raiz do projeto): powershell -ExecutionPolicy Bypass -File .\Tools\instalar-config-claude.ps1
$raiz = Split-Path -Parent $PSScriptRoot
$origem = Join-Path $raiz "claude-config"
$destino = Join-Path $raiz ".claude"
New-Item -ItemType Directory -Force -Path $destino | Out-Null
Copy-Item -Path (Join-Path $origem "*") -Destination $destino -Recurse -Force
Get-ChildItem -Path $destino -Recurse -File | ForEach-Object { Write-Host "instalado:" $_.FullName.Substring($raiz.Length + 1) }
