using System.Globalization;
using System.Text;

namespace CinzasDaMare.Shared.Diagnostics;

public enum LogLevel { Debug = 0, Info = 1, Aviso = 2, Erro = 3 }

/// <summary>
/// Log em arquivo texto, uma linha por evento, fácil de ler por pessoa e por ferramenta:
/// <c>2026-11-08 00:12:03.123 ERRO  [servidor.tick] mensagem | chave=valor</c> (exceções seguem indentadas com "  | ").
/// Sem Configure() não grava nada (os testes ficam limpos). Seguro entre threads; grava na hora (AutoFlush), para não
/// perder a última linha se o processo cair. Arquivos com mais de 5 MB viram uma continuação; guarda os 20 mais novos do prefixo.
/// </summary>
public static class GameLog
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public const int KeepFiles = 20;

    private static readonly object Gate = new();
    private static StreamWriter? _writer;
    private static string? _dir;
    private static string _prefix = "jogo";
    private static bool _perRun;
    private static string _day = "";
    private static int _part;
    private static string? _baseName;
    private static string? _lastErrorKey;
    private static DateTime _lastErrorAt;
    private static int _suppressed;

    public static LogLevel MinLevel { get; set; } = LogLevel.Info;
    public static string? CurrentFile { get; private set; }
    public static bool Enabled => _dir is not null;

    /// <summary>
    /// Liga o log. perRun = um arquivo por execução (cliente: várias janelas ao mesmo tempo não disputam o mesmo arquivo);
    /// senão um arquivo por dia (servidor).
    /// </summary>
    public static void Configure(string directory, string prefix, bool perRun = false, LogLevel minLevel = LogLevel.Info)
    {
        lock (Gate)
        {
            CloseWriter();
            Directory.CreateDirectory(directory);
            _dir = directory;
            _prefix = prefix;
            _perRun = perRun;
            _day = "";
            _part = 0;
            _baseName = null;
            MinLevel = minLevel;
            Cleanup();
        }
    }

    public static void Debug(string area, string message) => Write(LogLevel.Debug, area, message);
    public static void Info(string area, string message) => Write(LogLevel.Info, area, message);
    public static void Warn(string area, string message) => Write(LogLevel.Aviso, area, message);

    public static void Error(string area, string message, Exception? exception = null)
        => Write(LogLevel.Erro, area, message, exception);

    /// <summary>
    /// Executa a ação; se lançar exceção, registra (com pilha) e segue. A mesma exceção repetida em sequência é
    /// registrada uma vez e depois resumida ("repetiu N vezes"), para um erro por tick não encher o disco.
    /// Devolve false se houve exceção.
    /// </summary>
    public static bool Guard(string area, Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception e)
        {
            var key = area + "|" + e.GetType().FullName + "|" + FirstFrame(e);
            var now = DateTime.Now;
            lock (Gate)
            {
                if (key == _lastErrorKey && (now - _lastErrorAt).TotalSeconds < 30)
                {
                    _suppressed++;
                    _lastErrorAt = now;
                    return false;
                }
                if (_suppressed > 0)
                {
                    WriteLocked(LogLevel.Erro, area, $"a exceção anterior repetiu mais {_suppressed} vez(es)", null);
                    _suppressed = 0;
                }
                _lastErrorKey = key;
                _lastErrorAt = now;
            }
            Error(area, $"exceção não tratada (o processo continua): {e.GetType().Name}: {e.Message}", e);
            return false;
        }
    }

    public static void Write(LogLevel level, string area, string message, Exception? exception = null)
    {
        if (_dir is null || level < MinLevel) return;
        lock (Gate) WriteLocked(level, area, message, exception);
    }

    public static void Close()
    {
        lock (Gate)
        {
            CloseWriter();
            _dir = null;
            CurrentFile = null;
        }
    }

    /// <summary>Formata uma linha (público para testes e para o resumo).</summary>
    public static string Format(DateTime at, LogLevel level, string area, string message)
    {
        var name = level switch { LogLevel.Debug => "DEBUG", LogLevel.Info => "INFO ", LogLevel.Aviso => "AVISO", _ => "ERRO " };
        var clean = message.Replace('\r', ' ').Replace('\n', ' ');
        return $"{at.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} {name} [{area}] {clean}";
    }

    // ------------------------------------------------------------ interno

    private static void WriteLocked(LogLevel level, string area, string message, Exception? exception)
    {
        if (_dir is null) return;
        try
        {
            var now = DateTime.Now;
            EnsureWriter(now);
            var sb = new StringBuilder(Format(now, level, area, message));
            if (exception is not null)
                foreach (var line in exception.ToString().Split('\n'))
                    sb.Append('\n').Append("  | ").Append(line.TrimEnd('\r'));
            _writer!.WriteLine(sb.ToString());
        }
        catch
        {
            // log nunca pode derrubar o jogo (disco cheio, arquivo travado...)
        }
    }

    private static void EnsureWriter(DateTime now)
    {
        var day = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var newDay = !_perRun && day != _day;
        var full = _writer is not null && _writer.BaseStream.Length >= MaxBytes;
        if (_writer is not null && !newDay && !full) return;

        if (_baseName is null || newDay)
        {
            _day = day;
            _part = 0;
            _baseName = _perRun
                ? $"{_prefix}-{now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{Environment.ProcessId}"
                : $"{_prefix}-{day}";
        }
        else if (full)
        {
            _part++;
        }
        CloseWriter();
        CurrentFile = Path.Combine(_dir!, _baseName + (_part > 0 ? $"_parte{_part}" : "") + ".log");
        var stream = new FileStream(CurrentFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
    }

    private static void CloseWriter()
    {
        try { _writer?.Dispose(); } catch { /* ignora */ }
        _writer = null;
    }

    private static void Cleanup()
    {
        try
        {
            var old = new DirectoryInfo(_dir!).GetFiles(_prefix + "-*.log")
                .OrderByDescending(f => f.LastWriteTimeUtc).Skip(KeepFiles - 1);
            foreach (var f in old) f.Delete();
        }
        catch
        {
            // limpeza é opcional
        }
    }

    private static string FirstFrame(Exception e)
    {
        var stack = e.StackTrace ?? "";
        var end = stack.IndexOf('\n');
        return (end < 0 ? stack : stack[..end]).Trim();
    }
}
