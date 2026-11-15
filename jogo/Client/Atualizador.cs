using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Atualizador automático (11/11, pedido: "acabar com os zips para o amigo"). Ao entrar num servidor que não é deste
/// PC, pede a lista de arquivos do jogo do anfitrião, compara com os daqui (SHA-256, com cache por tamanho+data) e baixa
/// só o que mudou para a pasta "atualizacao_pendente" ao lado de Client/. O Tools/jogar.ps1 aplica a pasta antes de
/// abrir o jogo na próxima vez (não dá para trocar a dll com o jogo aberto). Nada é apagado.
/// </summary>
public sealed class Atualizador
{
    public event Action<NetMessage>? Send;
    /// <summary>Texto para a tela (progresso / pronto). Vazio = nada a mostrar.</summary>
    public string Status { get; private set; } = "";
    public bool Done { get; private set; }

    private readonly string _root;      // pasta do jogo (a que tem Client/ e Tools/)
    private readonly string _pending;
    private readonly List<UpdateFileInfo> _remote = new();
    private int _pages = -1;
    private double _retryIn = -1;
    private int _retries;
    private Task<List<UpdateFileInfo>>? _diff;
    private readonly Queue<UpdateFileInfo> _queue = new();
    private UpdateFileInfo? _current;
    private FileStream? _out;
    private int _total, _got;
    private long _bytesTotal, _bytesGot;
    private bool _started;

    public Atualizador()
    {
        var client = ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\');
        _root = Directory.GetParent(client)?.FullName ?? client;
        _pending = Path.Combine(_root, "atualizacao_pendente");
    }

    /// <summary>Quantos arquivos novos vieram (0 = já estava em dia).</summary>
    public int Baixados => _got;

    /// <summary>
    /// 14/11 18h (dono: "queria que ele puxasse a atualização direto do servidor ao tentar logar"): com a atualização
    /// baixada, reabre o jogo pelo Jogar.cmd (que aplica a pasta pendente, recompila e entra de novo no servidor guardado)
    /// e fecha este. Devolve falso se não achou o Jogar.cmd (aí o jogador fecha e abre na mão).
    /// </summary>
    /// <summary>15/11 noite: no PC do amigo, ao baixar do anfitrião, deixa Tools/versao_do_anfitriao.txt — o Jogar.cmd
    /// não troca pela versão do GitHub enquanto essa marca for recente (12 h).</summary>
    public bool MarcarVersaoDoAnfitriao { get; set; }

    public bool Reabrir()
    {
        if (MarcarVersaoDoAnfitriao)
            try { File.WriteAllText(Path.Combine(_root, "Tools", "versao_do_anfitriao.txt"), DateTime.UtcNow.ToString("o")); }
            catch (Exception e) { GameLog.Warn("cliente.atualizador", "não gravei a marca da versão do anfitrião: " + e.Message); }
        var cmd = Path.Combine(_root, "Jogar.cmd");
        if (!File.Exists(cmd) || !OperatingSystem.IsWindows()) return false;
        try
        {
            var pid = OS.CreateProcess("cmd.exe", new[] { "/c", "start", "\"KRYMOR\"", "/d", _root, "cmd", "/c", "timeout /t 3 /nobreak >nul & call Jogar.cmd" });
            GameLog.Info("cliente.atualizador", $"reabrindo pelo Jogar.cmd (pid {pid})");
            return pid > 0;
        }
        catch (Exception e) { GameLog.Warn("cliente.atualizador", "não deu para reabrir: " + e.Message); return false; }
    }

    public void Start()
    {
        if (_started) return;
        _started = true;
        Status = "";
        Send?.Invoke(new UpdateCheckIntent(0));
    }

    public void Handle(NetMessage msg)
    {
        switch (msg)
        {
            case UpdateManifestMessage page:
                if (page.Pages == 0)
                {
                    if (_retries++ < 20) _retryIn = 3;   // o anfitrião ainda está montando a lista
                    return;
                }
                _pages = page.Pages;
                _remote.AddRange(page.Files);
                if (page.Page + 1 < page.Pages) Send?.Invoke(new UpdateCheckIntent(page.Page + 1));
                else _diff = Task.Run(Compare);
                break;
            case UpdateChunkMessage chunk when _current is not null && chunk.Path == _current.Path:
                OnChunk(chunk);
                break;
        }
    }

    public void Process(double delta)
    {
        if (_retryIn > 0 && (_retryIn -= delta) <= 0) Send?.Invoke(new UpdateCheckIntent(0));
        if (_diff is { IsCompleted: true } d)
        {
            _diff = null;
            if (d.IsFaulted) { GameLog.Warn("cliente.atualizador", d.Exception?.GetBaseException().Message ?? "erro"); return; }
            foreach (var f in d.Result) _queue.Enqueue(f);
            _total = _queue.Count;
            _bytesTotal = d.Result.Sum(f => f.Size);
            GameLog.Info("cliente.atualizador", $"{_total} arquivo(s) novos no servidor ({_bytesTotal / 1024} KB)");
            if (_total == 0) { Done = true; return; }
            Next();
        }
    }

    private void Next()
    {
        _out?.Dispose(); _out = null;
        if (_queue.Count == 0)
        {
            _current = null;
            Done = true;
            Status = $"Atualização baixada ({_got} arquivo(s)). Feche o jogo e abra de novo para aplicar.";
            GameLog.Info("cliente.atualizador", "pronta em " + _pending);
            return;
        }
        _current = _queue.Dequeue();
        var dest = Path.Combine(_pending, _current.Path) + ".part";
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        _out = new FileStream(dest, FileMode.Create, System.IO.FileAccess.Write);
        Send?.Invoke(new UpdateFileRequest(_current.Path, 0));
    }

    private void OnChunk(UpdateChunkMessage chunk)
    {
        var cur = _current!;
        if (chunk.Error.Length > 0) { GameLog.Warn("cliente.atualizador", $"{cur.Path}: {chunk.Error}"); Next(); return; }
        var bytes = Convert.FromBase64String(chunk.Data);
        _out!.Write(bytes, 0, bytes.Length);
        _bytesGot += bytes.Length;
        var pct = _bytesTotal > 0 ? 100.0 * _bytesGot / _bytesTotal : 100;
        Status = $"Baixando atualização do servidor: {_got + 1}/{_total} arquivos ({pct:0}%)";
        var next = chunk.Offset + bytes.Length;
        if (bytes.Length > 0 && next < chunk.Total) { Send?.Invoke(new UpdateFileRequest(cur.Path, next)); return; }
        _out.Dispose(); _out = null;
        var part = Path.Combine(_pending, cur.Path) + ".part";
        var final = Path.Combine(_pending, cur.Path);
        if (Sha(part) == cur.Sha256) { File.Move(part, final, true); _got++; }
        else { GameLog.Warn("cliente.atualizador", $"{cur.Path}: veio diferente, ignorado"); File.Delete(part); }
        Next();
    }

    /// <summary>O que o servidor tem e aqui está diferente (nem igual já na pasta pendente).</summary>
    private List<UpdateFileInfo> Compare()
    {
        var cachePath = Path.Combine(_root, "Tools", "atualizador_cache.json");
        Dictionary<string, string> cache;
        try { cache = File.Exists(cachePath) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(cachePath)) ?? new() : new(); }
        catch { cache = new(); }
        var need = new List<UpdateFileInfo>();
        foreach (var f in _remote)
        {
            if (f.Path.Contains("..")) continue;
            var local = Path.Combine(_root, f.Path);
            var pend = Path.Combine(_pending, f.Path);
            if (File.Exists(pend) && new FileInfo(pend).Length == f.Size && Sha(pend) == f.Sha256) continue;
            if (File.Exists(local))
            {
                var fi = new FileInfo(local);
                if (fi.Length == f.Size)
                {
                    var key = $"{f.Path}|{fi.Length}|{fi.LastWriteTimeUtc.Ticks}";
                    if (!cache.TryGetValue(key, out var h)) cache[key] = h = Sha(local);
                    if (h == f.Sha256) continue;
                }
            }
            need.Add(f);
        }
        try { Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!); File.WriteAllText(cachePath, JsonSerializer.Serialize(cache)); }
        catch { /* sem cache: compara de novo da próxima vez */ }
        return need;
    }

    private static string Sha(string path)
    {
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
    }
}
