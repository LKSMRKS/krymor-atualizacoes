using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 13/11 ("uma progressão para acompanhar o que você ganha em cada nível — montaria, TP... tipo um passe de batalha, numa
/// tecla"): a Trilha do Herói. Tudo sai do conteúdo que já existe (nada é inventado aqui): regiões e passagens que abrem,
/// montarias, asas, viagens de NPC, poderes (tomos) da sua classe, trocas de classe, capítulos da Crônica e o Merlin.
/// "Tem" = você já tem o nível (e, quando há marca, a marca).
/// </summary>
public sealed partial class GameServer
{
    private void SendTrilha(Session s)
    {
        var itens = new List<TrilhaItem>();
        var nivel = s.Level;
        void Add(int lvl, string tipo, string titulo, string detalhe, bool? tem = null)
        {
            if (lvl < 1 || lvl > 60) return;
            itens.Add(new TrilhaItem(lvl, tipo, titulo, detalhe, tem ?? nivel >= lvl));
        }
        // regiões (faixa "11–16" → abre no 11)
        foreach (var r in _world!.Regions)
        {
            var faixa = r.Levels.Replace('-', '–').Split('–');
            if (faixa.Length >= 1 && int.TryParse(faixa[0].Trim(), out var de) && de > 1)
                Add(de, "mapa", r.Name, $"região de nível {r.Levels} — " + (r.Description.Length > 90 ? r.Description[..90] + "…" : r.Description));
        }
        // passagens com nível (dungeons, portões)
        foreach (var l in _world.Maps.Values.Where(l => !l.Instanced))
            foreach (var p in l.Portals.Where(p => p.MinLevel > 1 && _world.Maps.ContainsKey(p.ToMap)))
                Add(p.MinLevel, _world.Maps[p.ToMap].Kind == "dungeon" ? "dungeon" : "mapa", _world.Maps[p.ToMap].DisplayName, $"passagem em {l.DisplayName}");
        // viagens de NPC (Tião, Sibila, Merlin, barqueiro...)
        foreach (var l in _world.Maps.Values)
            foreach (var n in l.Npcs)
                foreach (var t in n.Travel.Where(t => t.MinLevel > 1 && _world.Maps.ContainsKey(t.ToMap)))
                    Add(t.MinLevel, "viagem", _world.Maps[t.ToMap].DisplayName, $"com {n.Name.Split(',')[0]}" + (t.RequiresFlag is { } f ? " (precisa de uma missão antes)" : ""),
                        nivel >= t.MinLevel && (t.RequiresFlag is null || s.Flags.Contains(t.RequiresFlag)));
        // montarias, asas e mascotes (pela primeira vez que aparecem)
        foreach (var it in _content.Items.Values.Where(i => i.Category is "mount" or "wings").OrderBy(i => i.MinLevel))
            Add(it.MinLevel, it.Category == "mount" ? "montaria" : "asas", it.DisplayName, it.Category == "mount" ? "montaria (V para montar)" : "asas (G para voar)");
        // 14/11 ("capas com níveis; item a cada poucos níveis"): capas, companheiros e skins (bracelete) pela 1ª vez que aparecem
        foreach (var it in _content.Items.Values.Where(i => i.Category is "cape" or "pet" or "bracelet").OrderBy(i => i.MinLevel))
            Add(Math.Max(2, it.MinLevel), it.Category == "cape" ? "capa" : it.Category == "pet" ? "pet" : "skin", it.DisplayName,
                it.Category == "cape" ? "capa (espaço de asas)" : it.Category == "pet" ? "companheiro" : "skin (bracelete)");
        // presentes que chegam sozinhos na mochila
        foreach (var (lv, item, _, texto) in CinzasDaMare.Shared.World.Desbloqueios.Presentes)
            Add(lv, "presente", "Presente: " + texto, "chega sozinho na mochila ao alcançar o nível", s.Flags.Contains("presente:" + lv));
        // provas de classe e a casa
        foreach (var q in _world!.Quests.Values.Where(q => q.Category == "classe" && (q.Class is null || q.Class == s.ClassId)))
            Add(q.MinLevel, "classe", q.Name, "prova com o seu mentor", s.Quests.TryGetValue(q.Id, out var stq) && stq.State == "done");
        if (_world.Quests.TryGetValue("q_pref_casa", out var casa)) Add(casa.MinLevel, "casa", "Casa própria", "escritura com o Prefeito Belmiro (Monte do Prefeito)", s.Flags.Contains("casa:propria"));
        // poderes da sua classe (tomos)
        foreach (var t in _tomes.Values.Where(t => t.ClassId == s.ClassId || t.ClassId == "todos").OrderBy(t => t.MinLevel))
            if (_content.Skills.TryGetValue(t.SkillId, out var sk))
                Add(t.MinLevel, "poder", sk.DisplayName, s.Flags.Contains(TomeFlag(t.SkillId)) ? "poder (já aprendido)" : "poder novo (tomo na loja/arcanista)");
        // trocas de classe, Crônica, Merlin, renascer
        foreach (var q in _world.Quests.Values)
        {
            if (q.Kind == "evolve" && (q.Class is null || q.Class == s.ClassId)) Add(q.MinLevel, "classe", q.Name, "troca de classe com o seu mentor");
            else if (q.Category == "historia_principal") Add(q.MinLevel, "historia", q.Name, "capítulo da história", s.Quests.TryGetValue(q.Id, out var st) && st.State == "done");
            else if (q.Giver == "merlin") Add(q.MinLevel, "merlin", q.Name, "missão do Merlin (Porto)", s.Quests.TryGetValue(q.Id, out var st2) && st2.State == "done");
            else if (q.Kind == "reset") Add(q.MinLevel, "extra", q.Name, "renascer: volta ao 1 mais forte");
        }
        // um marco por (nível, título): sem repetidos
        var lista = itens.GroupBy(i => (i.Nivel, i.Titulo)).Select(g => g.First()).OrderBy(i => i.Nivel).ThenBy(i => i.Tipo).ToList();
        SendTo(s, new TrilhaMessage(nivel, lista));
    }
}
