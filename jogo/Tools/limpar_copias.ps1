# 13/11 (pedido do dono: "pode apagar o que tiver desnecessário na pasta, sem fazer merda"). Apaga SÓ cópias repetidas:
#   - PACOTE_AMIGO\PARA_O_AMIGO_uNN: fica só a mais nova (o amigo precisa só da última)
#   - PACOTE_AMIGO\CinzasDaMare_Atualizacao_NN*.zip: atualizações antigas (cada pasta nova já tem tudo)
#   - backups\antes_atualizacao_*: ficam as 3 mais novas (dá para voltar até 3 atualizações)
# NUNCA mexe em: e6.db, game.db, tokens, contas, Content, Client, entregas (artes originais), as outras pastas de backups
# (arte antes do lote, goblins_v1, praca_antes...), nem nos CinzasDaMare_Amigo_parte1-3.zip (instalação base do amigo).
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location $raiz
$apagar = @()
$amigos = Get-ChildItem "PACOTE_AMIGO" -Directory -Filter "PARA_O_AMIGO_u*" -ErrorAction SilentlyContinue |
    Sort-Object { [int]($_.Name -replace '\D', '') }
if ($amigos.Count -gt 1) { $apagar += $amigos[0..($amigos.Count - 2)] }
$apagar += Get-ChildItem "PACOTE_AMIGO" -File -Filter "CinzasDaMare_Atualizacao_*.zip" -ErrorAction SilentlyContinue
$bk = Get-ChildItem "backups" -Directory -Filter "antes_atualizacao_*" -ErrorAction SilentlyContinue | Sort-Object Name
if ($bk.Count -gt 3) { $apagar += $bk[0..($bk.Count - 4)] }
if ($apagar.Count -eq 0) { Write-Host "Nada para limpar."; exit 0 }
$total = 0
foreach ($i in $apagar) {
    $t = if ($i.PSIsContainer) { (Get-ChildItem $i.FullName -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum } else { $i.Length }
    $total += $t
    Write-Host ("  {0,8:N0} MB  {1}" -f ($t / 1MB), $i.FullName.Substring($raiz.Length + 1))
}
Write-Host ""
Write-Host ("Vai liberar {0:N1} GB. Fica: a pasta do amigo mais nova, as 3 ultimas copias de antes da atualizacao, entregas e o resto." -f ($total / 1GB))
$ok = Read-Host "Apagar esses itens? Digite S e Enter para confirmar"
if ($ok -ne "S" -and $ok -ne "s") { Write-Host "Cancelado. Nada foi apagado."; exit 0 }
foreach ($i in $apagar) { Remove-Item -LiteralPath $i.FullName -Recurse -Force -ErrorAction Continue }
Write-Host "Pronto."
