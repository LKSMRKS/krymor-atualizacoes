<#
.SYNOPSIS
  Atalho de inicialização local do Cinzas da Maré (E8). Use pelo Jogar.cmd (duplo clique) na pasta do projeto.
.DESCRIPTION
  1. Acha o Godot 4.7.2 .NET (variável CDM_GODOT, configuração salva, pastas comuns ou pergunta uma vez e salva).
  2. Compila servidor e cliente SÓ se o código mudou desde a última compilação.
  3. Sobe o servidor (janela minimizada) se ele ainda não estiver aberto na porta. O banco migra sozinho, com backup.
  4. Abre o cliente já conectado com a conta escolhida. Personagem: escolhido na tela de seleção do jogo.
  Contas: reaproveita os tokens de e6-tokens.json (alice, bruno) e de contas-locais.json. Nada é apagado.
.PARAMETER Quem
  Conta a usar (ex.: alice). Sem isso: usa a última usada, ou pergunta.
.PARAMETER NovaConta
  Cria uma conta nova no banco atual, guarda o token em contas-locais.json e já entra com ela.
.PARAMETER Db
  Banco do servidor (padrão: e6.db, onde estão alice e bruno).
.PARAMETER CriarAtalho
  Cria "Cinzas da Maré" na Área de Trabalho apontando para o Jogar.cmd.
.PARAMETER Parar
  Fecha o servidor (salva os personagens conectados).
.EXAMPLE
  Jogar.cmd                       (abre com a última conta)
  Jogar.cmd -Quem bruno           (segunda janela, outra conta)
  Jogar.cmd -NovaConta carla
  Jogar.cmd -CriarAtalho
  Jogar.cmd -SemDeus              (sem o modo deus do F1: para jogar com os amigos)
  Jogar.cmd -Rede                 (11/11: abre o servidor para os amigos entrarem pela rede/VPN; mostra o seu IP)
  Jogar.cmd -Servidor 26.1.2.3 -Token xxxxx   (no PC do AMIGO: entra no servidor de outra pessoa, sem abrir servidor)
#>
# sem [CmdletBinding()]: ele cria o -Debug com apelido "db", que bate com o -Db daqui (erro no Windows PowerShell)
param(
    [string]$Quem = "",
    [string]$NovaConta = "",
    [string]$Db = "",
    [int]$Porta = 7777,
    [string]$Godot = "",
    [switch]$CriarAtalho,
    [switch]$Parar,
    [switch]$Recompilar,
    [switch]$SemDeus,
    [switch]$Rede,
    [string]$Servidor = "",
    [string]$Token = "",
    [switch]$ModoSeguro,   # 14/11 22h: abre com OpenGL (placas/drivers que fecham o jogo no Vulkan)
    [switch]$SoServidor    # 15/11 ("abrir o server sem abrir o jogo"): sobe só o servidor (ABRIR_SERVIDOR.cmd)
)

$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location $raiz

# 14/11 20h (dono: "fazer o download pelo GitHub para facilitar os amigos — está muito trabalhoso mandar arquivo"):
# no PC do AMIGO (tem Client\amigo.txt), antes de abrir, confere a versão publicada no GitHub; se mudou, baixa só os
# arquivos diferentes (lista com SHA-256 em manifest.json) para "atualizacao_pendente" — o bloco de baixo aplica.
$amigoTxt = Join-Path $raiz "Client\amigo.txt"
if ((Test-Path $amigoTxt) -and -not $env:KRYMOR_SEM_GITHUB) {
    $gh = "https://raw.githubusercontent.com/LKSMRKS/krymor-atualizacoes/main"
    $verLocalPath = Join-Path $PSScriptRoot "versao_github.txt"
    try {
        $ProgressPreference = "SilentlyContinue"   # a barra do PowerShell deixa o download 10x mais lento
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $remota = (Invoke-WebRequest "$gh/versao.txt?t=$([DateTime]::UtcNow.Ticks)" -UseBasicParsing -TimeoutSec 15).Content.Trim()
        $local = if (Test-Path $verLocalPath) { (Get-Content $verLocalPath -Raw).Trim() } else { "" }
        if ($remota -and $remota -ne $local) {
            Write-Host "Versão nova no GitHub: $remota (você: $(if ($local) { $local } else { 'sem registro' })). Conferindo arquivos..."
            $man = Invoke-RestMethod "$gh/manifest.json?t=$([DateTime]::UtcNow.Ticks)" -TimeoutSec 60
            $pend = Join-Path $raiz "atualizacao_pendente"
            $faltam = @()
            foreach ($e in $man.arquivos) {
                $alvo = Join-Path $raiz ($e.p -replace '/', '\')
                if ((Test-Path $alvo) -and (Get-Item $alvo).Length -eq [long]$e.s -and (Get-FileHash $alvo -Algorithm SHA256).Hash -eq $e.h) { continue }
                $faltam += $e
            }
            $i = 0; $mb = [math]::Round((($faltam | Measure-Object -Property s -Sum).Sum) / 1MB, 1)
            Write-Host "Baixando $($faltam.Count) arquivo(s) ($mb MB)..."
            foreach ($e in $faltam) {
                $i++
                $dest = Join-Path $pend ($e.p -replace '/', '\')
                New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest) | Out-Null
                $rel = (($e.p -split '/') | ForEach-Object { [uri]::EscapeDataString($_) }) -join '/'
                $url = "$gh/jogo/$rel"
                for ($t = 1; $t -le 3; $t++) {
                    try { Invoke-WebRequest $url -OutFile $dest -UseBasicParsing -TimeoutSec 300; break }
                    catch { if ($t -eq 3) { throw "não consegui baixar $($e.p): $($_.Exception.Message)" }; Start-Sleep -Seconds 2 }
                }
                if ($i % 25 -eq 0 -or $i -eq $faltam.Count) { Write-Host "  $i/$($faltam.Count)" }
            }
            Set-Content -Path $verLocalPath -Value $remota -Encoding UTF8
            Write-Host "Atualização $remota baixada."
        }
    }
    catch { Write-Host "Não deu para conferir o GitHub agora ($($_.Exception.Message)). Abrindo com a versão atual." }
}

# 11/11: atualizador automático — o jogo baixou uma versão nova do servidor do seu amigo em "atualizacao_pendente";
# aplica agora (o jogo está fechado, então dá para trocar a dll). Nada é apagado além da própria pasta pendente.
$pendente = Join-Path $raiz "atualizacao_pendente"
# 11/11: atualização entregue como zip (pelo Claude, direto na sua pasta): descompacta para a pasta pendente
$pendenteZip = Join-Path $raiz "atualizacao_pendente.zip"
# 14/11 ("mandei a atualização e foi com _1"): o navegador/Discord renomeia (atualizacao_pendente_1.zip, "(1)", .zip.zip);
# aceita qualquer variação e usa a MAIS NOVA; as mais velhas vão para backups\zips_antigos (nada é apagado)
$variacoes = @(Get-ChildItem -Path $raiz -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "atualizacao_pendente*.zip*" -or $_.Name -like "PARA_O_AMIGO*.zip" } | Sort-Object LastWriteTime -Descending)   # 14/11 20h: aceita o nome do pacote do amigo sem renomear
if ($variacoes.Count -gt 0) {
    $maisNova = $variacoes[0]
    $antigos = Join-Path $raiz "backups\zips_antigos"
    foreach ($v in ($variacoes | Select-Object -Skip 1)) {
        New-Item -ItemType Directory -Force -Path $antigos | Out-Null
        Move-Item $v.FullName (Join-Path $antigos ($v.LastWriteTime.ToString("yyyyMMdd_HHmm") + "_" + $v.Name)) -Force
    }
    if ($maisNova.FullName -ne $pendenteZip) {
        if (Test-Path $pendenteZip) { Remove-Item $pendenteZip -Force }
        Move-Item $maisNova.FullName $pendenteZip -Force
    }
}
if (Test-Path $pendenteZip) {
    Write-Host "Descompactando a atualização..."
    Expand-Archive -Path $pendenteZip -DestinationPath $pendente -Force
    Remove-Item $pendenteZip -Force
}
if (Test-Path $pendente) {
    Write-Host "Aplicando a atualização baixada do servidor..."
    Get-ChildItem $pendente -Recurse -File -Filter "*.part" | Remove-Item -Force -ErrorAction SilentlyContinue
    # cópia de segurança do que vai ser substituído (backupsntes_atualizacao_<data-hora>)
    $copia = Join-Path $raiz ("backups\antes_atualizacao_" + (Get-Date -Format "yyyy-MM-dd_HHmm"))
    Get-ChildItem $pendente -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($pendente.Length + 1)
        $atual = Join-Path $raiz $rel
        # 12/11: mapa que VOCÊ editou depois que a atualização foi feita fica como está (o editor salva no Content do projeto)
        if ($rel -like "Content\map_*.json" -and (Test-Path $atual) -and ((Get-Item $atual).LastWriteTime -gt $_.LastWriteTime.AddMinutes(1))) {
            Write-Host "  mantive o seu mapa editado: $rel"
            Remove-Item $_.FullName -Force
            return
        }
        # 14/11 20h40 (dono: "6 min aplicando a atualização"): arte (Client\Assets) não ganha cópia de segurança —
        # é só substituída (copiar ~1 GB de modelos antes de trocar dobrava o tempo); código e mapas continuam com cópia
        if ((Test-Path $atual) -and -not $rel.StartsWith("Client\Assets\")) {
            $destino = Join-Path $copia $rel
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destino) | Out-Null
            Copy-Item $atual $destino -Force
        }
    }
    robocopy $pendente $raiz /E /MOVE /NFL /NDL /NJH /NJS | Out-Null
    if (Test-Path $pendente) { Remove-Item $pendente -Recurse -Force -ErrorAction SilentlyContinue }
    Write-Host "Atualização aplicada."
}
$configPath = Join-Path $PSScriptRoot "jogar.config.json"
$contasPath = Join-Path $raiz "contas-locais.json"
$e6TokensPath = Join-Path $raiz "e6-tokens.json"
$servidorExe = Join-Path $raiz "ServerHost\bin\Release\net8.0\CinzasDaMare.ServerHost.exe"
$clienteDll = Join-Path $raiz "Client\.godot\mono\temp\bin\Debug\CinzasDaMare.Client.dll"

function Ler-Json([string]$caminho) {
    if (-not (Test-Path $caminho)) { return @{} }
    $obj = Get-Content $caminho -Raw -Encoding UTF8 | ConvertFrom-Json
    $tabela = @{}
    if ($obj) { foreach ($p in $obj.PSObject.Properties) { $tabela[$p.Name] = $p.Value } }
    return $tabela
}

function Salvar-Json([hashtable]$tabela, [string]$caminho) {
    ($tabela | ConvertTo-Json) | Set-Content -Path $caminho -Encoding UTF8
}

function Porta-Aberta([int]$p) {
    $cliente = New-Object System.Net.Sockets.TcpClient
    try {
        $tentativa = $cliente.BeginConnect("127.0.0.1", $p, $null, $null)
        if (-not $tentativa.AsyncWaitHandle.WaitOne(300)) { return $false }
        $cliente.EndConnect($tentativa)
        return $true
    } catch { return $false } finally { $cliente.Close() }
}

function Mais-Recente([string[]]$pastas, [string[]]$filtros) {
    $maior = [datetime]::MinValue
    foreach ($pasta in $pastas) {
        if (-not (Test-Path $pasta)) { continue }
        foreach ($filtro in $filtros) {
            Get-ChildItem -Path $pasta -Filter $filtro -Recurse -File -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -notmatch '\\(bin|obj|\.godot)\\' } |
                ForEach-Object { if ($_.LastWriteTime -gt $maior) { $maior = $_.LastWriteTime } }
        }
    }
    return $maior
}

function Precisa-Compilar([string]$saida, [string[]]$pastas) {
    if ($Recompilar -or -not (Test-Path $saida)) { return $true }
    return (Mais-Recente $pastas @("*.cs", "*.csproj", "*.json")) -gt (Get-Item $saida).LastWriteTime
}

# (a função NÃO pode se chamar "Dotnet": o PowerShell não diferencia maiúsculas e ela chamaria a si mesma para sempre)
function Rodar-Dotnet([string[]]$argumentos) {
    $exe = (Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    & $exe @argumentos
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($argumentos -join ' ') falhou (código $LASTEXITCODE). Veja as mensagens acima." }
}

$config = Ler-Json $configPath
if (-not $Db) { $Db = if ($config.ContainsKey("db")) { [string]$config["db"] } else { "e6.db" } }

# ---------------------------------------------------------------- parar
if ($Parar) {
    $proc = Get-Process -Name "CinzasDaMare.ServerHost" -ErrorAction SilentlyContinue
    if ($proc) { $proc | Stop-Process; Write-Host "Servidor fechado." } else { Write-Host "Servidor não estava aberto." }
    return
}

# ---------------------------------------------------------------- atalho na Área de Trabalho
if ($CriarAtalho) {
    $desktop = [Environment]::GetFolderPath("Desktop")
    $shell = New-Object -ComObject WScript.Shell
    $lnk = $shell.CreateShortcut((Join-Path $desktop "Cinzas da Maré.lnk"))
    $lnk.TargetPath = Join-Path $raiz "Jogar.cmd"
    $lnk.WorkingDirectory = $raiz
    $lnk.WindowStyle = 7
    if ($config.ContainsKey("godot") -and (Test-Path $config["godot"])) { $lnk.IconLocation = [string]$config["godot"] }
    $lnk.Save()
    Write-Host "Atalho criado na Área de Trabalho: Cinzas da Maré"
    return
}

# ---------------------------------------------------------------- Godot
if (-not $Godot) { $Godot = $env:CDM_GODOT }
if ((-not $Godot) -and $config.ContainsKey("godot")) { $Godot = [string]$config["godot"] }
if ((-not $Godot) -or -not (Test-Path $Godot)) {
    Write-Host "Procurando o Godot 4.7.2 .NET..."
    $candidatos = @()
    foreach ($pasta in @((Join-Path $raiz "Godot"), [Environment]::GetFolderPath("Desktop"), (Join-Path $env:USERPROFILE "Downloads"), $env:USERPROFILE,
                         (Split-Path $raiz -Parent), "C:\Godot", "C:\Program Files\Godot", $env:LOCALAPPDATA)) {
        if ($pasta -and (Test-Path $pasta)) {
            $candidatos += Get-ChildItem -Path $pasta -Filter "Godot_v4.7*mono*win64*.exe" -Recurse -Depth 3 -File -ErrorAction SilentlyContinue
        }
    }
    $escolhido = $candidatos | Sort-Object { if ($_.Name -match "console") { 1 } else { 0 } } | Select-Object -First 1
    if ($escolhido) { $Godot = $escolhido.FullName }
    else {
        Add-Type -AssemblyName System.Windows.Forms
        $dialogo = New-Object System.Windows.Forms.OpenFileDialog
        $dialogo.Title = "Onde está o Godot 4.7.2 .NET (Godot_v4.7.2-stable_mono_win64.exe)?"
        $dialogo.Filter = "Godot (*.exe)|*.exe"
        if ($dialogo.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { throw "Sem o Godot não dá para abrir o cliente." }
        $Godot = $dialogo.FileName
    }
    Write-Host "Godot: $Godot (guardado em Tools\jogar.config.json)"
}
$config["godot"] = $Godot
$config["db"] = $Db

# ---------------------------------------------------------------- amigo: entra no servidor de outra pessoa
# 12/11 (amigo abriu o Jogar.cmd sem -Servidor e deu "ServerHost.csproj não existe"): o IP e o token usados uma vez ficam
# guardados; no PC do amigo (sem a pasta ServerHost) o Jogar.cmd sozinho já entra no servidor guardado — ou pergunta.
$semServidorLocal = -not (Test-Path (Join-Path $raiz "ServerHost\CinzasDaMare.ServerHost.csproj"))
# 14/11 (amigo com a pasta ServerHost de um pacote antigo caiu em "Nenhuma conta guardada"): sem conta local guardada,
# este PC é de amigo — entra no servidor guardado (ou pergunta o IP e o token), a não ser que esteja criando conta
$temContaLocal = ((Ler-Json $contasPath).Count -gt 0) -or ((Ler-Json $e6TokensPath).Count -gt 0)
if (-not $Servidor -and -not $NovaConta -and ($semServidorLocal -or -not $temContaLocal)) {
    if ($config.ContainsKey("servidor")) { $Servidor = [string]$config["servidor"] }
    if (-not $Token -and $config.ContainsKey("token")) { $Token = [string]$config["token"] }
    if (-not $Servidor) { $Servidor = Read-Host "IP do servidor do seu amigo (o do Radmin, ex.: 26.1.2.3)" }
    if (-not $Token) { $Token = Read-Host "Seu token (o que seu amigo te mandou)" }
}
if ($Servidor) {
    if (-not $Token) { throw "Faltou o token: Jogar.cmd -Servidor $Servidor -Token <o token que seu amigo mandou>" }
    # o pacote do amigo já vem com o cliente compilado: só compila se não tiver (ou com -Recompilar)
    # 14/11 (amigo preso na u55): o pacote traz o CÓDIGO do cliente, não a dll — recompila sempre que o código mudar
    if (Precisa-Compilar $clienteDll @("Client", "Shared")) {
        Write-Host "Compilando o cliente..."
        Rodar-Dotnet @("build", ".\Client\CinzasDaMare.Client.csproj", "-nologo", "-v", "q")
    }
    $config["godot"] = $Godot
    $config["servidor"] = $Servidor
    $config["token"] = $Token
    Salvar-Json $config $configPath
    $cliente = '"' + (Resolve-Path .\Client).Path + '"'
    $video = if ($ModoSeguro -or (Test-Path (Join-Path $PSScriptRoot "modo_seguro.txt"))) { @("--rendering-driver", "opengl3") } else { @() }
    if ($ModoSeguro) { Set-Content (Join-Path $PSScriptRoot "modo_seguro.txt") "OpenGL (apague este arquivo para voltar ao Vulkan)" }
    Start-Process -FilePath $Godot -ArgumentList (@("--path", $cliente) + $video + @("--", "--host", $Servidor, "--port", "$Porta",
        "--token", $Token, "--autoconnect")) | Out-Null
    Write-Host "Entrando no servidor $Servidor`:$Porta..."
    exit 0
}

# ---------------------------------------------------------------- compilar (só se mudou)
if (Precisa-Compilar $servidorExe @("Server", "Shared", "ServerHost", "Content")) {
    if (Porta-Aberta $Porta) { throw "O código do servidor mudou, mas o servidor antigo está aberto. Feche-o (Jogar.cmd -Parar) e rode de novo." }
    Write-Host "Compilando o servidor (Release)..."
    Rodar-Dotnet @("build", "-c", "Release", ".\ServerHost\CinzasDaMare.ServerHost.csproj", "-nologo", "-v", "q")
}
if (-not $SoServidor -and (Precisa-Compilar $clienteDll @("Client", "Shared"))) {
    Write-Host "Compilando o cliente..."
    Rodar-Dotnet @("build", ".\Client\CinzasDaMare.Client.csproj", "-nologo", "-v", "q")
}

# ---------------------------------------------------------------- contas (reaproveita as que já existem)
$contas = Ler-Json $contasPath
$e6 = Ler-Json $e6TokensPath
foreach ($k in $e6.Keys) { if (-not $contas.ContainsKey($k)) { $contas[$k] = $e6[$k] } }

if ($NovaConta) {
    if ($contas.ContainsKey($NovaConta)) { throw "Já existe uma conta '$NovaConta' guardada. Use -Quem $NovaConta." }
    $saida = & $servidorExe --db $Db --provision $NovaConta
    if ($LASTEXITCODE -ne 0) { throw "Não foi possível criar a conta '$NovaConta' (nome repetido?)." }
    $token = ($saida | Where-Object { $_ -and $_.ToString().Trim() } | Select-Object -Last 1).ToString().Trim()
    $locais = Ler-Json $contasPath
    $locais[$NovaConta] = $token
    Salvar-Json $locais $contasPath
    $contas[$NovaConta] = $token
    $Quem = $NovaConta
    Write-Host "Conta '$NovaConta' criada (token guardado em contas-locais.json; não compartilhe)."
}

if (-not $Quem -and $config.ContainsKey("conta") -and $contas.ContainsKey([string]$config["conta"])) { $Quem = [string]$config["conta"] }
if (-not $Quem) {
    if ($contas.Count -eq 0) { throw "Nenhuma conta guardada. Crie uma: Jogar.cmd -NovaConta seunome" }
    $nomes = @($contas.Keys | Sort-Object)
    if ($nomes.Count -eq 1) { $Quem = $nomes[0] }
    else {
        Write-Host "Contas guardadas: $($nomes -join ', ')"
        $Quem = Read-Host "Com qual conta entrar"
    }
}
if (-not $contas.ContainsKey($Quem)) { throw "Conta '$Quem' não tem token guardado. Contas: $(@($contas.Keys) -join ', ')" }
$config["conta"] = $Quem
Salvar-Json $config $configPath

# ---------------------------------------------------------------- servidor
if (-not (Porta-Aberta $Porta)) {
    Write-Host "Abrindo o servidor (banco $Db, porta $Porta)..."
    # modo deus (F1) ligado para testar; -SemDeus para jogar com os amigos sem ele
    $argsServidor = @("--db", $Db, "--port", "$Porta")
    if ($Rede) {
        # aceita conexões de outros PCs (rede local ou VPN tipo Radmin/Tailscale/ZeroTier); sem TLS: só rede de confiança
        $argsServidor += @("--bind", "0.0.0.0")
        try { if (-not (Get-NetFirewallRule -DisplayName "Cinzas da Mare $Porta" -ErrorAction SilentlyContinue)) {
            Start-Process powershell -Verb RunAs -Wait -ArgumentList "-NoProfile -Command New-NetFirewallRule -DisplayName 'Cinzas da Mare $Porta' -Direction Inbound -Protocol TCP -LocalPort $Porta -Action Allow" } }
        catch { Write-Host "Não consegui liberar a porta $Porta no Firewall do Windows; libere à mão se o amigo não conectar." }
    }
    # 12/11 ("F1 só na minha conta"): o modo deus vale SÓ para a conta de quem abriu o servidor (-Quem); amigos nunca têm F1
    if (-not $SemDeus) { $argsServidor += @("--deus", $Quem) }
    Start-Process -FilePath $servidorExe -ArgumentList $argsServidor -WorkingDirectory $raiz -WindowStyle Minimized | Out-Null
    $limite = (Get-Date).AddSeconds(20)
    while (-not (Porta-Aberta $Porta)) {
        if ((Get-Date) -gt $limite) { throw "O servidor não abriu a porta $Porta em 20 s. Rode-o à mão para ver o erro: $servidorExe --db $Db --port $Porta" }
        Start-Sleep -Milliseconds 300
    }
} else {
    Write-Host "Servidor já está aberto na porta $Porta."
    if ($Rede) { Write-Host "ATENÇÃO: ele foi aberto antes, talvez só para este PC. Feche (Jogar.cmd -Parar) e rode Jogar.cmd -Rede de novo." }
}
if ($Rede) {
    $ips = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*" } |
        ForEach-Object { "$($_.IPAddress)  ($($_.InterfaceAlias))" }
    Write-Host ""
    Write-Host "Servidor aberto para os amigos na porta $Porta. Passe para ele um destes IPs (o da VPN, se usarem uma):"
    $ips | ForEach-Object { Write-Host "   $_" }
    Write-Host ""
}

if ($SoServidor) {
    Write-Host "Servidor aberto (janela minimizada na barra de tarefas). O jogo NÃO foi aberto."
    Write-Host "Para fechar o servidor: Jogar.cmd -Parar"
    exit 0
}

# ---------------------------------------------------------------- cliente
$cliente = '"' + (Resolve-Path .\Client).Path + '"'
Start-Process -FilePath $Godot -ArgumentList @("--path", $cliente, "--", "--host", "127.0.0.1", "--port", "$Porta",
    "--token", [string]$contas[$Quem], "--autoconnect") | Out-Null
Write-Host "Cliente aberto com a conta '$Quem'. Escolha ou crie o personagem na tela de seleção."
