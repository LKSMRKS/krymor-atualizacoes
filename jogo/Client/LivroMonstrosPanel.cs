using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 12/11: Livro dos Monstros pessoal (tecla L, do nível 10). Mostra só as criaturas que você já derrotou, por região:
/// nível, natureza (tipo) e o que cada uma pode soltar — o mesmo catálogo das bibliotecas, mas o seu.
/// </summary>
public sealed class LivroMonstrosPanel
{
    public Control Root { get; }
    public bool Open => Root.Visible;
    private readonly VBoxContainer _box;
    private BestiaryMessage? _msg;
    private string _open = "";
    private static readonly Color Gold = new(1f, 0.85f, 0.45f), Dim = new(0.7f, 0.7f, 0.68f);

    public LivroMonstrosPanel()
    {
        var panel = new StonePanel(16f) { Visible = false, CustomMinimumSize = new Vector2(540, 560) };
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(516, 536), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        panel.AddChild(scroll);
        _box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _box.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_box);
        Root = panel;
    }

    public void Toggle() => Root.Visible = !Root.Visible;
    public void Hide() => Root.Visible = false;

    public void Handle(BestiaryMessage m)
    {
        _msg = m;
        Rebuild();
    }

    public void Update()
    {
        if (!Root.Visible) return;
        var view = Root.GetViewportRect().Size;
        Root.Position = (new Vector2(40f, (view.Y - Root.Size.Y) / 2f) + Arrastador.Desvio("livro_monstros")).Floor();
    }

    private void Rebuild()
    {
        foreach (var c in _box.GetChildren()) c.QueueFree();
        _box.AddChild(HudView.MakeLabel("Livro dos Monstros", 22, Gold));
        if (_msg is not { } m) { _box.AddChild(HudView.MakeLabel("Abrindo…", 13, Dim)); return; }
        if (m.Locked)
        {
            _box.AddChild(Wrap($"Seu livro começa no nível {m.UnlockLevel}. Até lá, as bibliotecas (a do Cais, no Porto) contam o que sabem.", 14, Dim));
            return;
        }
        _box.AddChild(HudView.MakeLabel($"{m.Seen} de {m.Total} criaturas anotadas  ·  L fecha", 13, Dim));
        if (m.Entries.Count == 0) { _box.AddChild(Wrap("Nada anotado ainda: derrote uma criatura e ela entra no livro.", 14, Dim)); return; }
        foreach (var g in m.Entries.GroupBy(e => e.Region))
        {
            var open = _open == g.Key;
            var tab = new Button { Text = (open ? "▾ " : "▸ ") + $"{g.Key}  ({g.Select(e => e.MonsterId).Distinct().Count()})", Flat = true,
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin, FocusMode = Control.FocusModeEnum.None };
            var key = g.Key;
            tab.Pressed += () => { _open = _open == key ? "" : key; Rebuild(); };
            _box.AddChild(tab);
            if (!open) continue;
            foreach (var e in g)
            {
                var card = new PanelContainer();
                card.AddThemeStyleboxOverride("panel", HudView.CardStyle(e.Tag.Length > 0));
                var col = new VBoxContainer();
                col.AddThemeConstantOverride("separation", 2);
                card.AddChild(col);
                col.AddChild(HudView.MakeLabel($"{e.Monster}  —  nível {e.MonsterLevels}{(e.Tag.Length > 0 ? "  · " + e.Tag : "")}", 14, e.Tag.Length > 0 ? Gold : new Color(0.9f, 0.88f, 0.82f)));
                if (e.Type.Length > 0) col.AddChild(HudView.MakeLabel("Natureza: " + e.Type, 11, new Color(0.6f, 0.8f, 0.95f)));
                foreach (var dr in e.Drops) col.AddChild(Wrap("• " + dr, 12, Dim));
                _box.AddChild(card);
            }
        }
    }

    private static Label Wrap(string text, int size, Color color)
    {
        var l = HudView.MakeLabel(text, size, color);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(480, 0);
        return l;
    }
}
