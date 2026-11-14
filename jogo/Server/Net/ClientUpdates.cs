using System.Security.Cryptography;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// Atualizador automático (11/11): o servidor do anfitrião serve os arquivos do cliente (pasta Client, menos o cache
/// .godot — mas COM a dll compilada — e o Tools/jogar.ps1). O cliente do amigo compara com o que tem e baixa só o que
/// mudou para "atualizacao_pendente/"; o jogar.ps1 aplica antes de abrir o jogo na próxima vez.
/// Só serve arquivos da lista (nada fora da pasta do jogo). A lista é montada uma vez, em segundo plano.
/// </summary>
public sealed class ClientUpdates
{
    public const int PageSize = 400;
    public const int ChunkBytes = 128 * 1024;
    private readonly string _root;
    private Task<List<UpdateFileInfo>>? _manifest;

    public ClientUpdates(string projectRoot) { _root = Path.GetFullPath(projectRoot); }

    public void Warm() => _manifest ??= Task.Run(Build);

    public List<UpdateFileInfo>? Manifest => _manifest is { IsCompletedSuccessfully: true } t ? t.Result : null;

    /// <summary>Quais arquivos vão: tudo de Client/ (sem .godot, bin, obj, logs) + a dll do cliente + o jogar.ps1.</summary>
    public static bool Ships(string rel)
    {
        rel = rel.Replace('\\', '/');
        if (rel == "Tools/jogar.ps1" || rel == "Jogar.cmd") return true;
        // 12/11 ("amigo sem zip"): também os scripts do Windows de Tools (logs, atalho...)
        if (rel.StartsWith("Tools/", StringComparison.Ordinal) && rel.Count(c => c == '/') == 1
            && (rel.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) || rel.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))) return true;
        // 14/11: o código de Shared/ também vai — o jogo do amigo é recompilado do código quando ele muda (jogar.ps1),
        // e sem o Shared novo a versão compilada ficava a antiga (e o servidor barra versão diferente)
        if (rel.StartsWith("Shared/", StringComparison.Ordinal))
            return (rel.EndsWith(".cs", StringComparison.Ordinal) || rel.EndsWith(".csproj", StringComparison.Ordinal))
                   && !rel.Contains("/bin/", StringComparison.Ordinal) && !rel.Contains("/obj/", StringComparison.Ordinal);
        if (rel is "Directory.Build.props" or "global.json") return true;
        if (!rel.StartsWith("Client/", StringComparison.Ordinal)) return false;
        if (rel.StartsWith("Client/.godot/", StringComparison.Ordinal))
            return rel.StartsWith("Client/.godot/mono/temp/bin/Debug/CinzasDaMare.", StringComparison.Ordinal);
        foreach (var bad in new[] { "/bin/", "/obj/", "/logs/", "/.vs/" }) if (rel.Contains(bad, StringComparison.Ordinal)) return false;
        return !rel.EndsWith(".tmp", StringComparison.Ordinal) && !rel.EndsWith(".bak", StringComparison.Ordinal);
    }

    private List<UpdateFileInfo> Build()
    {
        var list = new List<UpdateFileInfo>();
        var t0 = DateTime.UtcNow;
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(_root, file).Replace('\\', '/');
            if (!Ships(rel)) continue;
            try
            {
                using var fs = File.OpenRead(file);
                var hash = Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
                list.Add(new UpdateFileInfo(rel, fs.Length, hash));
            }
            catch (IOException) { /* arquivo preso: fica de fora desta vez */ }
        }
        list.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        GameLog.Info("servidor.atualizador", $"{list.Count} arquivos do cliente prontos para os amigos ({list.Sum(f => f.Size) / 1048576} MB, {(DateTime.UtcNow - t0).TotalSeconds:0.0} s)");
        return list;
    }

    public UpdateManifestMessage? Page(int page)
    {
        var m = Manifest;
        if (m is null) return null;
        var pages = Math.Max(1, (m.Count + PageSize - 1) / PageSize);
        page = Math.Clamp(page, 0, pages - 1);
        var version = m.FirstOrDefault(f => f.Path.EndsWith("CinzasDaMare.Client.dll", StringComparison.Ordinal))?.Sha256[..12] ?? "";
        return new UpdateManifestMessage(page, pages, m.Skip(page * PageSize).Take(PageSize).ToList(), version);
    }

    public UpdateChunkMessage Chunk(string path, long offset)
    {
        var m = Manifest;
        var info = m?.FirstOrDefault(f => f.Path == path);
        if (info is null) return new UpdateChunkMessage(path, offset, 0, "", "not_shipped");
        var full = Path.GetFullPath(Path.Combine(_root, path));
        if (!full.StartsWith(_root, StringComparison.Ordinal)) return new UpdateChunkMessage(path, offset, 0, "", "not_shipped");
        try
        {
            using var fs = File.OpenRead(full);
            if (offset < 0 || offset > fs.Length) return new UpdateChunkMessage(path, offset, fs.Length, "", "bad_offset");
            fs.Seek(offset, SeekOrigin.Begin);
            var buf = new byte[(int)Math.Min(ChunkBytes, fs.Length - offset)];
            var read = 0;
            while (read < buf.Length) { var n = fs.Read(buf, read, buf.Length - read); if (n <= 0) break; read += n; }
            return new UpdateChunkMessage(path, offset, fs.Length, Convert.ToBase64String(buf, 0, read));
        }
        catch (IOException) { return new UpdateChunkMessage(path, offset, 0, "", "busy"); }
    }
}
