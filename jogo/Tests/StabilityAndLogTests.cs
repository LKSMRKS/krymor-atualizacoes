using CinzasDaMare.Shared.Client;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>
/// Correções de estabilidade (08/11/2026): servidor caindo/desconectando o jogador. E o log em arquivo.
/// </summary>
public class StabilityTests
{
    [Fact]
    public void SetaQueMataAcolitoComBolaEmVoo_NaoDerrubaOServidor()
    {
        // Auditoria E8, item 1: remover projéteis da lista dentro do laço de projéteis lançava ArgumentOutOfRangeException.
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("a", "monster_r1_acolito_das_cinzas", 9, 0, level: 1) });
        using var h = new MapHarness(w);
        var id = h.Join("mia", "mago", 0, 0, level: 60);
        h.Face(id, 1, 0);
        Assert.True(h.TickUntil(() => h.LastSnapshot(id).Projectiles?.Any(p => p.Hostile) == true, 200));
        for (var i = 0; i < 10 && h.Monster(id, "m_a")!.Alive; i++)
        {
            h.Transport.ClientSends(id, new CastIntent("skill_mago_seta_de_brasa", null));
            h.Ticks(9); // lançaria exceção aqui antes da correção
        }
        Assert.False(h.Monster(id, "m_a")!.Alive);
        h.Ticks(40);
        Assert.DoesNotContain(h.LastSnapshot(id).Projectiles!, p => p.Hostile);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(144)]
    [InlineData(165)]
    [InlineData(240)]
    public void AndarGirandoACamera_NaoPassaDoLimiteDeMensagens_EmQualquerFps(int fps)
    {
        // Antes: uma MoveIntent por quadro com a câmera girando → acima de 120/s o servidor desconectava (a partir de 120 Hz).
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("ana", x: 0, y: 30);
        var sender = new MoveSender();
        var face = new FaceThrottle();
        var sent = 0;
        var framesPerTick = fps / 20.0;
        var nextTick = framesPerTick;
        for (var frame = 0; frame < fps * 3; frame++)
        {
            var now = (long)(frame * 1000.0 / fps);
            var yaw = frame * 0.4 * Math.PI / 180;
            var (x, y) = CameraMove.Direction(true, false, false, false, yaw);
            if (sender.ShouldSend(x, y, now)) { h.Transport.ClientSends(id, new MoveIntent(x, y)); sent++; }
            if (face.ShouldSend(x, y, now)) h.Transport.ClientSends(id, new FaceIntent(x, y));
            if (frame >= nextTick) { h.Ticks(1); nextTick += framesPerTick; }
        }
        Assert.False(h.Transport.WasDisconnected(id));
        Assert.InRange(sent, 1, 3 * 21);
    }

    [Fact]
    public void MoveSender_ParadaSaiNaHora_EGiroPequenoNaoSai()
    {
        var s = new MoveSender();
        Assert.True(s.ShouldSend(1, 0, 0));
        Assert.False(s.ShouldSend(Math.Cos(0.01), Math.Sin(0.01), 100)); // 0,6°: não vale enviar
        Assert.False(s.ShouldSend(0, 1, 10));                            // virou, mas cedo demais
        Assert.True(s.ShouldSend(0, 1, 60));
        Assert.True(s.ShouldSend(0, 0, 61));                             // parar sai na hora
        Assert.False(s.ShouldSend(0, 0, 200));
        Assert.True(s.ShouldSend(-1, 0, 201));                           // voltar a andar sai na hora
    }
}

public class GameLogTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cdm_log_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void SemConfigurar_NaoGravaNada_EGuardDevolveFalso()
    {
        GameLog.Close();
        Assert.False(GameLog.Enabled);
        Assert.False(GameLog.Guard("teste", () => throw new InvalidOperationException("x")));
        Assert.True(GameLog.Guard("teste", () => { }));
    }

    [Fact]
    public void GravaLinhasComNivelEArea_ExcecaoComPilha_ERepeticaoResumida()
    {
        var dir = NewDir();
        try
        {
            GameLog.Configure(dir, "teste");
            GameLog.Info("servidor.login", "alice entrou");
            GameLog.Debug("servidor.itens", "não aparece no nível Info");
            for (var i = 0; i < 5; i++) GameLog.Guard("servidor.tick", () => throw new ArgumentOutOfRangeException("index"));
            GameLog.Guard("servidor.tick", () => throw new InvalidOperationException("outra"));
            var file = GameLog.CurrentFile!;
            GameLog.Close();

            var text = File.ReadAllText(file);
            Assert.Contains("INFO  [servidor.login] alice entrou", text);
            Assert.DoesNotContain("não aparece", text);
            Assert.Single(text.Split('\n'), l => l.Contains("ArgumentOutOfRangeException") && l.Contains("ERRO"));
            Assert.Contains("repetiu mais 4 vez(es)", text);
            Assert.Contains("  | System.ArgumentOutOfRangeException", text);
            Assert.Contains("InvalidOperationException: outra", text);
        }
        finally
        {
            GameLog.Close();
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void ArquivoPorExecucao_TemNomeComHoraEProcesso()
    {
        var dir = NewDir();
        try
        {
            GameLog.Configure(dir, "cliente", perRun: true);
            GameLog.Info("cliente", "oi");
            Assert.Matches(@"cliente-\d{8}-\d{6}-\d+\.log$", GameLog.CurrentFile!);
        }
        finally
        {
            GameLog.Close();
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
