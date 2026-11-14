using System.Globalization;

namespace CinzasDaMare.Shared.Client;

public static class HudText
{
    public const string Help =
        "WASD mover · mouse mira · Q Golpe de Cunha · E Guarda · R ataque auto · F pegar item · 1 poção · G equipar arma · H desequipar";

    public static string Build(ClientModel m)
    {
        var inv = CultureInfo.InvariantCulture;
        var lines = new List<string>();
        var me = m.Me;
        var progress = m.Progress;

        if (me is null)
        {
            lines.Add("Aguardando o servidor…");
        }
        else
        {
            var level = progress?.Level ?? me.Level;
            var xp = progress is null ? "" : $"   XP {progress.Xp.ToString(inv)}/{progress.XpToNext.ToString(inv)}";
            lines.Add($"{me.Name}  Nv {level}{xp}");
            lines.Add($"Vida {me.Health.ToString("0", inv)}/{me.MaxHealth.ToString("0", inv)}   Fôlego {me.Resource.ToString("0", inv)}"
                      + (me.Guarding ? "   [GUARDA]" : "") + (me.Alive ? "" : "   [MORTO]"));
        }

        var target = m.NearestAliveMonster();
        lines.Add(target is null
            ? "Alvo: nenhum monstro vivo"
            : $"Alvo: {target.SpeciesName} {target.Health.ToString("0", inv)}/{target.MaxHealth.ToString("0", inv)}"
              + (m.AutoAttackOn ? "   [ataque automático]" : ""));

        var weapon = m.Equipment.FirstOrDefault(e => e.Slot == ClientModel.WeaponSlot);
        lines.Add("Arma: " + (weapon is null ? "mãos nuas" : ClientModel.DisplayName(weapon.DefinitionId)));
        lines.Add("Mochila: " + (m.Inventory.Count == 0
            ? "vazia"
            : string.Join(", ", m.Inventory.OrderBy(i => i.Slot).Select(i =>
                ClientModel.DisplayName(i.DefinitionId) + (i.Quantity > 1 ? " x" + i.Quantity.ToString(inv) : "")))));
        lines.Add("");
        lines.AddRange(m.Log);
        lines.Add("");
        lines.Add(Help);
        return string.Join("\n", lines);
    }
}
