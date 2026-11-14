using System.Diagnostics;
using System.Net;
using CinzasDaMare.Server.Content;
using CinzasDaMare.Server.Net;
using CinzasDaMare.Server.Persistence;
using CinzasDaMare.Shared;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.World;

// Uso:
//   dotnet run --project ServerHost -- --provision alice        cria conta e imprime o token (uma vez)
//   dotnet run --project ServerHost -- --revoke alice           revoga a conta
//   dotnet run --project ServerHost -- [--port 7777] [--bind 127.0.0.1] [--db game.db] [--content <pasta>] [--arena]
//   --logs <pasta> (padrão: logs) --log-debug   log em arquivo: logs\servidor-AAAAMMDD.log
//   --deus [contas]   modo deus (F1 no cliente: nível, moedas, itens, monstros, hora...). Sem lista = todas as contas
//   --list-accounts                                               lista contas e personagens do banco (sem tokens)
// Por padrão sobe o MAPA (Content/map_vale.json). --arena volta à arena do Marco A.
// Sem TLS: use apenas em localhost ou rede privada/VPN (ver Docs/STATUS.md).

static string? Arg(string[] a, string key)
{
    var i = Array.IndexOf(a, key);
    return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
}

// 12/11 ("atualizei um mapa e quando fui jogar não estava mais lá"): o servidor lia (e o editor SALVAVA) na cópia do
// Content dentro de bin/, que a próxima compilação apaga. Agora usa a pasta Content do projeto (a de verdade), quando acha.
var contentDir = Arg(args, "--content") ?? ProjectContent() ?? Path.Combine(AppContext.BaseDirectory, "Content");

static string? ProjectContent()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "CinzasDaMare.sln")) && File.Exists(Path.Combine(dir.FullName, "Content", "world.json")))
            return Path.Combine(dir.FullName, "Content");
    return null;
}
var dbPath = Arg(args, "--db") ?? "game.db";
var bind = Arg(args, "--bind") ?? "127.0.0.1";
if (!int.TryParse(Arg(args, "--port") ?? "7777", out var port) || port is < 0 or > 65535)
{
    Console.Error.WriteLine("Porta inválida.");
    return 1;
}
if (!IPAddress.TryParse(bind, out var address))
{
    Console.Error.WriteLine("Endereço --bind inválido.");
    return 1;
}

var content = ContentLoader.Load(contentDir);
var balance = ContentLoader.LoadBalance(contentDir);
// ---- log em arquivo (um por dia); o token nunca é registrado
GameLog.Configure(Arg(args, "--logs") ?? "logs", "servidor", perRun: false,
    args.Contains("--log-debug") ? LogLevel.Debug : LogLevel.Info);
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    GameLog.Error("servidor", "exceção fatal: o processo vai fechar", e.ExceptionObject as Exception);
TaskScheduler.UnobservedTaskException += (_, e) => { GameLog.Error("servidor", "exceção em tarefa de fundo", e.Exception); e.SetObserved(); };

var useArena = args.Contains("--arena") || !WorldContentLoader.Exists(contentDir);
var world = useArena ? null : WorldContentLoader.Load(contentDir);
var errors = ContentValidator.Validate(content).Concat(ContentValidator.ValidateBalance(balance))
    .Concat(world is null ? Array.Empty<string>() : ContentValidator.ValidateWorld(content, world)).ToList();
// 13/11 (o servidor não abria depois do dono salvar o Porto com a forja encostada na parede): peça/objeto/spawn "dentro de
// obstáculo" é coisa de posição feita no editor — vira AVISO (o jogo abre e o log diz o que arrumar); o resto continua impedindo
var avisos = errors.Where(e => e.Contains("dentro de obstáculo", StringComparison.Ordinal) || e.Contains("chega dentro de obstáculo", StringComparison.Ordinal)).ToList();
foreach (var a in avisos) { GameLog.Warn("servidor.conteudo", a + " (arrume no editor: mova um pouco)"); Console.WriteLine("Aviso de conteúdo: " + a); }
errors = errors.Except(avisos).ToList();
if (errors.Count > 0)
{
    foreach (var e in errors) GameLog.Error("servidor.conteudo", e);
    Console.Error.WriteLine("Conteúdo inválido:");
    foreach (var e in errors) Console.Error.WriteLine("  - " + e);
    return 1;
}

// mochila em grade estilo MU (8 × 8 casas; itens ocupam o seu tamanho) — os testes antigos usam a capacidade do balance
var db = new GameDatabase(dbPath, Math.Max(balance.InventoryCapacity, 96));   // 8×12: 8 linhas livres + 4 por missão (11/11)
db.Initialize();

var provision = Arg(args, "--provision");
if (provision is not null)
{
    var (_, token) = db.ProvisionAccount(provision);
    Console.WriteLine($"Conta '{provision}' criada. Token (guarde agora; não será mostrado de novo):");
    Console.WriteLine(token);
    return 0;
}

if (args.Contains("--list-accounts"))
{
    foreach (var (account, characters) in db.ListAccounts())
        Console.WriteLine($"{account}: {(characters.Count == 0 ? "(sem personagens)" : string.Join(", ", characters))}");
    return 0;
}

var revoke = Arg(args, "--revoke");
if (revoke is not null)
{
    Console.WriteLine(db.RevokeAccount(revoke) ? $"Conta '{revoke}' revogada." : $"Conta '{revoke}' não encontrada.");
    return 0;
}

using var transport = new TcpServerTransport(address, port);
var server = new GameServer(content, db, balance, transport, null, world);
if (world is not null) server.EnableGridInventory(8);
server.ContentDir = contentDir;
server.ExigirVersao = !args.Contains("--sem-checar-versao");   // 14/11: versão diferente não entra
// 11/11: atualizador — serve Client/ (com a dll compilada) e Tools/jogar.ps1 para os amigos ("--sem-atualizador" desliga)
if (!args.Contains("--sem-atualizador"))
{
    // raiz do projeto: a pasta atual (o Jogar.cmd roda dali) ou subindo a partir do .exe até achar Client/project.godot
    string? projectRoot = null;
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        for (var dir = new DirectoryInfo(start); dir is not null && projectRoot is null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Client", "project.godot"))) projectRoot = dir.FullName;
    if (projectRoot is not null)
    {
        server.Updates = new ClientUpdates(projectRoot);
        server.Updates.Warm();
    }
}   // 11/11: editor no jogo grava os mapas aqui (com histórico em Content/editor_historico)
// modo deus (F1 no cliente): "--deus" libera para todas as contas; "--deus alice,bruno" só para essas
if (args.Contains("--deus"))
{
    var list = Arg(args, "--deus");
    server.GodAccounts = list is null || list.StartsWith("--")
        ? new HashSet<string>()
        : new HashSet<string>(list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);
    Console.WriteLine("Modo deus LIGADO (F1 no jogo)" + (server.GodAccounts.Count > 0 ? ": " + string.Join(", ", server.GodAccounts) : " para todas as contas") + ".");
    GameLog.Info("servidor", "modo deus ligado" + (server.GodAccounts.Count > 0 ? ": " + string.Join(", ", server.GodAccounts) : " (todas as contas)"));
}
transport.Start();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
Console.WriteLine($"Servidor ouvindo em {bind}:{transport.Port} (banco: {Path.GetFullPath(dbPath)}; {(world is null ? "arena do Marco A" : "mapa " + world.Map.DisplayName)}). Ctrl+C para encerrar.");

GameLog.Info("servidor", $"iniciado: porta {transport.Port}, banco {Path.GetFullPath(dbPath)} (esquema v{db.GetSchemaVersion()}), " +
                         $"{(world is null ? "arena do Marco A" : "mapa " + world.Map.Id)}, log em {Path.GetFullPath(Arg(args, "--logs") ?? "logs")}");

var clock = Stopwatch.StartNew();
long next = 0;
var tickWatch = new Stopwatch();
double tickSum = 0, tickMax = 0;
int tickCount = 0, tickErrors = 0;
var healthAt = clock.ElapsedMilliseconds + 60_000;
while (!cts.IsCancellationRequested)
{
    tickWatch.Restart();
    // Uma exceção num tick é registrada com a pilha e o servidor segue (antes derrubava todo mundo).
    if (!GameLog.Guard("servidor.tick", server.Tick)) tickErrors++;
    tickWatch.Stop();
    var ms = tickWatch.Elapsed.TotalMilliseconds;
    tickSum += ms;
    tickCount++;
    if (ms > tickMax) tickMax = ms;
    if (ms > 40) GameLog.Warn("servidor.desempenho", $"tick {server.CurrentTick} levou {ms:0} ms (orçamento {GameServer.TickMs} ms)");
    if (clock.ElapsedMilliseconds >= healthAt)
    {
        GameLog.Info("servidor.saude", $"jogadores {server.ConnectedPlayers}, monstros vivos {server.AliveMonsters}, itens no chão {server.WorldItemCount}, " +
                                      $"tick médio {tickSum / Math.Max(1, tickCount):0.00} ms máx {tickMax:0.0} ms, erros {tickErrors}, " +
                                      $"snapshot {server.LastSnapshotBytes} B (máx {server.MaxSnapshotBytes} B), memória {GC.GetTotalMemory(false) / 1048576.0:0} MB");
        healthAt += 60_000;
        tickSum = 0; tickMax = 0; tickCount = 0; tickErrors = 0;
    }
    next += GameServer.TickMs;
    var wait = next - clock.ElapsedMilliseconds;
    if (wait > 0) Thread.Sleep((int)wait);
    else if (wait < -1000) next = clock.ElapsedMilliseconds; // ficou muito para trás: reseta o ritmo
}

GameLog.Guard("servidor", server.Shutdown);
GameLog.Info("servidor", "encerrado; personagens salvos");
GameLog.Close();
Console.WriteLine("Servidor encerrado; personagens salvos.");
return 0;
