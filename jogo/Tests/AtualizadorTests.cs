using CinzasDaMare.Server.Net;

namespace CinzasDaMare.Tests;

/// <summary>11/11: atualizador automático — o que vai para o amigo e o envio em pedaços.</summary>
public class AtualizadorTests
{
    [Fact]
    public void Lista_SoClienteDllEJogar_EPedacosRemontamOArquivo()
    {
        Assert.True(ClientUpdates.Ships("Client/Main.cs"));
        Assert.True(ClientUpdates.Ships("Client/.godot/mono/temp/bin/Debug/CinzasDaMare.Client.dll"));
        Assert.True(ClientUpdates.Ships("Tools/jogar.ps1"));
        Assert.True(ClientUpdates.Ships("Shared/VersaoJogo.cs"));   // 14/11: o amigo recompila com o Shared novo
        Assert.False(ClientUpdates.Ships("Shared/bin/Debug/x.dll"));
        Assert.False(ClientUpdates.Ships("Client/.godot/imported/x.ctex"));
        Assert.False(ClientUpdates.Ships("e6.db"));
        Assert.False(ClientUpdates.Ships("Server/Net/GameServer.cs"));
        Assert.False(ClientUpdates.Ships("Client/obj/x.dll"));

        var root = Path.Combine(Path.GetTempPath(), "cdm_upd_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Client", "Assets"));
            Directory.CreateDirectory(Path.Combine(root, "Client", ".godot", "imported"));
            var big = new byte[300_000];
            new Random(1).NextBytes(big);
            File.WriteAllBytes(Path.Combine(root, "Client", "Assets", "big.bin"), big);
            File.WriteAllText(Path.Combine(root, "Client", ".godot", "imported", "x.ctex"), "cache");
            File.WriteAllText(Path.Combine(root, "e6.db"), "segredo");
            var up = new ClientUpdates(root);
            up.Warm();
            for (var i = 0; i < 100 && up.Manifest is null; i++) Thread.Sleep(20);
            var page = up.Page(0)!;
            Assert.Single(page.Files);
            Assert.Equal("Client/Assets/big.bin", page.Files[0].Path);
            using var ms = new MemoryStream();
            long off = 0;
            while (true)
            {
                var c = up.Chunk("Client/Assets/big.bin", off);
                var b = Convert.FromBase64String(c.Data);
                ms.Write(b);
                off += b.Length;
                if (off >= c.Total || b.Length == 0) break;
            }
            Assert.Equal(big, ms.ToArray());
            Assert.Equal("not_shipped", up.Chunk("e6.db", 0).Error);
            Assert.Equal("not_shipped", up.Chunk("Client/../e6.db", 0).Error);
        }
        finally { Directory.Delete(root, true); }
    }
}
