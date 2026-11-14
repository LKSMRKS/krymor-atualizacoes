param([string]$GodotPath = "")
Write-Host "== .NET SDKs =="
if (Get-Command dotnet -ErrorAction SilentlyContinue) { dotnet --list-sdks } else { Write-Host "dotnet NAO encontrado" }
Write-Host "== Git =="
if (Get-Command git -ErrorAction SilentlyContinue) { git --version } else { Write-Host "git NAO encontrado" }
Write-Host "== Godot =="
if ($GodotPath -and (Test-Path $GodotPath)) {
  $v = & $GodotPath --version
  Write-Host $v
  if ($v -notmatch "mono") { Write-Host "ATENCAO: nao parece a edicao .NET" }
} else { Write-Host "Informe -GodotPath com o executavel Godot .NET" }
