using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>11/11: editor no jogo (modo deus) grava no map_*.json, guarda a versão anterior e o "Voltar versão" desfaz.</summary>
public class EditorNoJogoTests
{
    [Fact]
    public void Editor_PoeArvoreEMonstro_GravaEVoltaVersao()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cdm_editor_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        foreach (var f in Directory.GetFiles(MapHarness.ContentDir)) File.Copy(f, Path.Combine(tmp, Path.GetFileName(f)));
        try
        {
            using var h = new MapHarness(MapHarness.RealWorld());
            h.Server.ContentDir = tmp;
            h.Server.GodAccounts = new HashSet<string>();
            var id = h.Join("ed", x: 0, y: -9.5, level: 5);
            h.Ticks(2);
            var porto = Path.Combine(tmp, "map_porto.json");
            var antes = File.ReadAllText(porto);
            h.Transport.ClientSends(id, new GodIntent("edit", "tree", X: 14, Y: 6));
            h.Ticks(2);
            // 11/11 noite (editor v2): a mudança vale na hora, mas o arquivo só muda ao Salvar
            Assert.Equal(antes, File.ReadAllText(porto));
            Assert.Equal(1, h.Last<EditorStateMessage>(id).Unsaved);
            h.Transport.ClientSends(id, new GodIntent("edit", "spawn", Amount: 2, Extra: "monster_r0_caranguejinho", X: -14, Y: 12));
            h.Ticks(2);
            Assert.Contains(h.LastSnapshot(id).Monsters, m => m.DefinitionId == "monster_r0_caranguejinho");
            // Ctrl+Z desfaz o monstro (rascunho), Salvar grava a árvore
            h.Transport.ClientSends(id, new GodIntent("edit", "undo"));
            h.Ticks(2);
            Assert.DoesNotContain(h.LastSnapshot(id).Monsters, m => m.DefinitionId == "monster_r0_caranguejinho");
            h.Transport.ClientSends(id, new GodIntent("edit", "save"));
            h.Ticks(2);
            var depois = File.ReadAllText(porto);
            Assert.Contains("\"editor\": true", depois);
            Assert.DoesNotContain("monster_r0_caranguejinho", depois);
            Assert.Equal(0, h.Last<EditorStateMessage>(id).Unsaved);
            // selecionar, mudar o tamanho e descartar (volta ao salvo)
            h.Transport.ClientSends(id, new GodIntent("edit", "select", X: 14, Y: 6));
            h.Ticks(1);
            Assert.Equal("obstacles", h.Last<EditorStateMessage>(id).SelList);
            h.Transport.ClientSends(id, new GodIntent("edit", "set", Extra: "{\"key\":\"height\",\"value\":9}"));
            h.Ticks(1);
            Assert.Contains("\"height\": 9", h.Last<EditorStateMessage>(id).SelJson);
            h.Transport.ClientSends(id, new GodIntent("edit", "discard"));
            h.Ticks(1);
            Assert.Equal(depois, File.ReadAllText(porto));
            // Voltar versão salva: o arquivo volta a ser o de antes
            h.Transport.ClientSends(id, new GodIntent("edit", "restore"));
            h.Ticks(2);
            Assert.Equal(antes.TrimEnd(), File.ReadAllText(porto).TrimEnd());
            // mover a Capitã Ondina (NPC) e girar
            var layout = h.World.Maps["porto_das_cinzas"];
            var ondina = layout.Npcs.First(n => n.Name.Contains("Ondina"));
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            h.Transport.ClientSends(id, new GodIntent("edit", "move", Extra: $"{ondina.X.ToString(inv)};{ondina.Y.ToString(inv)}", X: ondina.X + 2, Y: ondina.Y));
            h.Ticks(2);
            Assert.Equal(ondina.X + 2, h.World.Maps["porto_das_cinzas"].Npcs.First(n => n.Id == ondina.Id).X, 1);
            h.Transport.ClientSends(id, new GodIntent("edit", "rotate", Yaw: 45));
            h.Ticks(2);
            Assert.Equal((ondina.Facing + 45) % 360, h.World.Maps["porto_das_cinzas"].Npcs.First(n => n.Id == ondina.Id).Facing, 1);
        }
        finally { Directory.Delete(tmp, true); }
    }
}
