using CinzasDaMare.Server.Content;
using CinzasDaMare.Shared;

namespace CinzasDaMare.Tests;

public class ContentTests
{
    [Fact]
    public void ConteudoDoMarcoA_CarregaEPassaNaValidacao()
    {
        // E8: entram o Mago (2 classes), 4 habilidades novas, 11 equipamentos e 4 monstros do mapa.
        // O conteúdo do Marco A continua igual (o primeiro monstro e as duas habilidades do Guerreiro).
        var b = TestContent.Load();
        Assert.Equal(new[] { "guerreiro", "mago", "bruxa", "arqueiro" }, b.Classes.Keys.ToArray());
        // E9: +2 ataques sem arma (soco, faísca), itens de início/região/dungeon, elmos e materiais, monstros por
        // região e um segundo tônico. Nada foi removido nem renomeado (IDs gravados no banco continuam iguais).
        Assert.True(b.Skills.Count >= 8); // + habilidades de arma e de evolução (direção consolidada)
        Assert.True(b.Items.Count >= 12);
        Assert.True(b.Monsters.Count >= 5);
        Assert.Equal("monster_r1_caranguejo_de_cal", b.Monsters.Keys.First());
        Assert.True(b.Consumables.Count >= 4);
        foreach (var id in new[] { "weapon_guerreiro_sword_p1", "staff_mago_aprendiz", "armor_cota_de_malha", "staff_mago_cetro_capataz" })
            Assert.True(b.Items.ContainsKey(id), id);
        Assert.Empty(ContentValidator.Validate(b));
    }

    [Fact]
    public void Validador_DetectaReferenciaInexistente_ENegativos()
    {
        var b = TestContent.Load();
        var skill = b.Skills[TestContent.Golpe] with { Id = "skill_ruim", ClassId = "classe_fantasma", ResourceCost = -5 };
        var item = b.Items[TestContent.Sword] with
        {
            Id = "item_ruim",
            AllowedClasses = new[] { "mago_inexistente" },
            BaseStats = new Dictionary<string, double> { ["weapon_power"] = -1 },
        };
        var broken = new ContentBundle(
            b.Classes,
            b.Skills.Concat(new[] { new KeyValuePair<string, SkillDefinition>(skill.Id, skill) }).ToDictionary(k => k.Key, v => v.Value),
            b.Items.Concat(new[] { new KeyValuePair<string, ItemDefinition>(item.Id, item) }).ToDictionary(k => k.Key, v => v.Value),
            b.Monsters,
            b.Consumables);

        var errors = ContentValidator.Validate(broken);

        Assert.Contains(errors, e => e.Contains("skill_ruim") && e.Contains("classe_fantasma"));
        Assert.Contains(errors, e => e.Contains("skill_ruim") && e.Contains("recurso negativo"));
        Assert.Contains(errors, e => e.Contains("item_ruim") && e.Contains("mago_inexistente"));
        Assert.Contains(errors, e => e.Contains("item_ruim") && e.Contains("negativo"));
    }

    [Fact]
    public void Carregador_RejeitaIdDuplicado()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cdm_dup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            foreach (var f in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Content")))
                File.Copy(f, Path.Combine(dir, Path.GetFileName(f)));
            var classes = File.ReadAllText(Path.Combine(dir, "classes.json"));
            var duplicated = "[" + classes.Trim().TrimStart('[').TrimEnd(']') + "," + classes.Trim().TrimStart('[').TrimEnd(']') + "]";
            File.WriteAllText(Path.Combine(dir, "classes.json"), duplicated);

            var ex = Assert.Throws<InvalidDataException>(() => ContentLoader.Load(dir));
            Assert.Contains("duplicado", ex.Message);
        }
        finally { Directory.Delete(dir, true); }
    }
}
