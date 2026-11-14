using System.Linq;
using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>12/11: Máscara do Pandemônio — item de cabeça, qualquer classe (nível 20): +força e a habilidade Furacão (X).</summary>
public class MascaraTests
{
    [Fact]
    public void SemMascara_NaoTemFuracao_ComMascara_Gira()
    {
        var ct = TestContent.Load();
        Assert.Equal("skill_mascara_furacao", ct.Items["mascara_pandemonio"].ItemSkill);
        Assert.Equal("todos", ct.Skills["skill_mascara_furacao"].ClassId);
        using var h = new MapHarness(MapHarness.RealWorld());
        var a = h.Join("mira", "mago", 0, -60, 22, map: "costa_do_cal");
        h.Ticks(2);
        h.Transport.ClientSends(a, new CastIntent("skill_mascara_furacao", null));
        h.Ticks(2);
        Assert.Equal("NotLearned", h.Last<CastResultMessage>(a).Failure);
        var item = h.Temp.Db.CreateInventoryItem(h.CharacterId(a), "mascara_pandemonio", "comum", 1, "t:masc")!;
        h.Transport.ClientSends(a, new EquipIntent(item));
        h.Ticks(2);
        Assert.Contains(h.Temp.Db.GetEquipment(h.CharacterId(a)), e => e.DefinitionId == "mascara_pandemonio");
        h.Ticks(40);
        // 1º X: transforma no Pandemônio
        h.Transport.ClientSends(a, new CastIntent("skill_mascara_furacao", null));
        h.Ticks(3);
        Assert.Equal("forma_pandemonio", h.Me(a).Form);
        h.Ticks(20);
        var before = h.Transport.Received<CastResultMessage>(a).Count;
        h.Transport.ClientSends(a, new CastIntent("skill_mascara_furacao", null));
        Assert.True(h.TickUntil(() => h.Transport.Received<CastResultMessage>(a).Count > before, 40));
        var r = h.Transport.Received<CastResultMessage>(a).Last(c => c.SkillId == "skill_mascara_furacao");
        Assert.True(r.Success, r.Failure);
    }
}
