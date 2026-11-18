using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Client;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 18/11 (dono: "máquina de mesclar visual — coloca um visual de um no outro: arma com arma, skin com skin, capa com capa;
/// assim você fica do seu jeito e com o poder do outro; aparece na cidade Astrália"). 1) escolha a peça VESTIDA (fica com
/// os atributos); 2) escolha na MOCHILA o item do mesmo tipo que dá a aparência (é consumido); 3) Mesclar (moedas).
/// "Tirar visual" volta a peça ao original. O servidor confere tudo (GameServer.Mesclar.cs).
/// </summary>
public sealed class MesclarPanel
{
    public Control Root { get; }
    public bool Open => Root.Visible;
    public Action<NetMessage>? Send;
    private readonly VBoxContainer _alvos = new(), _fontes = new();
    private readonly Label _resumo;
    private readonly Button _botao;
    private ClientModel? _m;
    private string? _alvo, _fonte;
    private string _sig = "";

    private static readonly Color Ouro = new(0.95f, 0.78f, 0.30f);
    private static readonly Color Dim = new(0.72f, 0.69f, 0.62f);

    public MesclarPanel()
    {
        var panel = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(760, 0) };
        panel.AddThemeStyleboxOverride("panel", HudView.PanelStyle(12f, 0.96f));
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        panel.AddChild(col);
        var topo = new HBoxContainer();
        var t = HudView.MakeLabel("✦ Máquina de Mesclar Visual", 22, new Color(0.85f, 0.7f, 1f));
        if (HudView.TitleFont is not null) t.AddThemeFontOverride("font", HudView.TitleFont);
        t.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        topo.AddChild(t);
        var fechar = new Button { Text = "✕", FocusMode = Control.FocusModeEnum.None };
        fechar.Pressed += Close;
        topo.AddChild(fechar);
        col.AddChild(topo);
        var dica = HudView.MakeLabel("A peça que você VESTE fica com os atributos dela e ganha a aparência de outro item do mesmo tipo (arma com arma da mesma família, elmo com elmo, capa com capa, asas com asas, skin com skin). O item da aparência é consumido.", 12, Dim);
        dica.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        dica.CustomMinimumSize = new Vector2(730, 0);
        col.AddChild(dica);
        var linhas = new HBoxContainer();
        linhas.AddThemeConstantOverride("separation", 12);
        col.AddChild(linhas);
        Coluna(linhas, "1. Peça vestida (fica com o PODER)", _alvos);
        Coluna(linhas, "2. Aparência (da mochila — será consumida)", _fontes);
        _resumo = HudView.MakeLabel("Escolha a peça vestida.", 14, Ouro);
        _resumo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _resumo.CustomMinimumSize = new Vector2(730, 0);
        col.AddChild(_resumo);
        _botao = new Button { Text = "Mesclar", Disabled = true, CustomMinimumSize = new Vector2(220, 36), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        _botao.Pressed += () =>
        {
            if (_alvo is null || _fonte is null) return;
            Send?.Invoke(new MesclarIntent(_alvo, _fonte));
            Sfx.Play("forja");
            _fonte = null; _sig = "";
        };
        col.AddChild(_botao);
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.Position = new Vector2(-380, -260);
        Root = panel;
    }

    private static void Coluna(HBoxContainer onde, string titulo, VBoxContainer lista)
    {
        var v = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        v.AddChild(HudView.MakeLabel(titulo, 14, Ouro, 2));
        var sc = new ScrollContainer { CustomMinimumSize = new Vector2(360, 300), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        lista.AddThemeConstantOverride("separation", 4);
        lista.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        sc.AddChild(lista);
        v.AddChild(sc);
        onde.AddChild(v);
    }

    public void Show(ClientModel m) { _m = m; _alvo = null; _fonte = null; _sig = ""; Root.Visible = true; }
    public void Close() => Root.Visible = false;

    private static string Familia(string sub) => sub switch { "bow" => "arco", "staff" => "cajado", "talisman" => "talisma", _ => "corpo" };

    private static string Tipo(ItemInfo? i) => i is null ? "" : i.Category switch
    {
        "weapon" => "arma:" + Familia(i.Subtype),
        "offhand" => "offhand:" + i.Subtype,
        "helmet" or "armor" or "gloves" or "pants" or "boots" or "cape" or "wings" or "bracelet" => i.Category,
        _ => "",
    };

    private static string NomeDoEspaco(string slot) => slot switch
    {
        "weapon_main" => "Arma", "off_hand" => "Mão 2", "head" => "Elmo", "body" => "Peito", "luvas" => "Luvas", "calca" => "Calça",
        "botas" => "Botas", "asas" => "Asas/Capa", "bracelete" => "Skin", _ => slot,
    };

    private string? VisualAtual(string itemId)
    {
        var pre = "visual:" + itemId + "=";
        return _m?.Flags.FirstOrDefault(f => f.StartsWith(pre, StringComparison.Ordinal))?[pre.Length..];
    }

    public void Update()
    {
        if (!Root.Visible || _m is null) return;
        var sig = $"{_alvo}|{_fonte}|{_m.Coins}|{string.Join(",", _m.Equipment.Select(e => e.ItemId))}|{_m.Inventory.Count}|{string.Join(",", _m.Flags.Where(f => f.StartsWith("visual:")))}";
        if (sig == _sig) return;
        _sig = sig;
        foreach (var c in _alvos.GetChildren()) c.QueueFree();
        foreach (var c in _fontes.GetChildren()) c.QueueFree();
        var vestidos = _m.Equipment.Where(e => Tipo(_m.ItemInfoFor(e.DefinitionId)).Length > 0).ToList();
        if (vestidos.Count == 0) _alvos.AddChild(HudView.MakeLabel("Vista uma arma, armadura, capa, asas ou skin.", 13, Dim));
        foreach (var e in vestidos)
        {
            var info = _m.ItemInfoFor(e.DefinitionId);
            var vis = VisualAtual(e.ItemId);
            var linha = Linha(info, e.DefinitionId, $"{NomeDoEspaco(e.Slot)}: {info?.DisplayName ?? e.DefinitionId}"
                + (vis is not null ? $"\n   aparência: {_m.ItemInfoFor(vis)?.DisplayName ?? vis}" : ""), _alvo == e.ItemId);
            var id = e.ItemId;
            linha.Pressed += () => { _alvo = id; _fonte = null; _sig = ""; };
            var h = new HBoxContainer();
            linha.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            h.AddChild(linha);
            if (vis is not null)
            {
                var tirar = new Button { Text = "Tirar visual", FocusMode = Control.FocusModeEnum.None, TooltipText = "Volta à aparência original (de graça; o item usado não volta)." };
                tirar.Pressed += () => { Send?.Invoke(new MesclarIntent(id, "")); _sig = ""; };
                h.AddChild(tirar);
            }
            _alvos.AddChild(h);
        }
        var alvo = vestidos.FirstOrDefault(e => e.ItemId == _alvo);
        var tipo = alvo is null ? "" : Tipo(_m.ItemInfoFor(alvo.DefinitionId));
        if (alvo is null) _fontes.AddChild(HudView.MakeLabel("← escolha primeiro a peça vestida.", 13, Dim));
        else
        {
            var fontes = _m.Inventory.Where(i => i.DefinitionId != alvo.DefinitionId && Tipo(_m.ItemInfoFor(i.DefinitionId)) == tipo).ToList();
            if (fontes.Count == 0) _fontes.AddChild(Wrap("Nenhum item do mesmo tipo na mochila. Traga um (do baú, da loja ou de um drop).", 13, Dim));
            foreach (var i in fontes)
            {
                var info = _m.ItemInfoFor(i.DefinitionId);
                var linha = Linha(info, i.DefinitionId, info?.DisplayName ?? i.DefinitionId, _fonte == i.ItemId);
                var id = i.ItemId;
                linha.Pressed += () => { _fonte = id; _sig = ""; };
                _fontes.AddChild(linha);
            }
        }
        var fonte = _m.Inventory.FirstOrDefault(i => i.ItemId == _fonte);
        if (alvo is not null && fonte is not null)
        {
            var nivel = _m.ItemInfoFor(alvo.DefinitionId)?.MinLevel ?? 1;
            var custo = 1500 + 150L * Math.Max(1, nivel);
            _resumo.Text = $"{_m.ItemInfoFor(alvo.DefinitionId)?.DisplayName} fica com a aparência de {_m.ItemInfoFor(fonte.DefinitionId)?.DisplayName}. Custo: {custo:N0} moedas (você tem {_m.Coins:N0}).".Replace(",", ".");
            _botao.Disabled = _m.Coins < custo;
        }
        else
        {
            _resumo.Text = alvo is null ? "Escolha a peça vestida." : "Agora escolha o item que dá a aparência.";
            _botao.Disabled = true;
        }
    }

    private static Label Wrap(string t, int size, Color c)
    {
        var l = HudView.MakeLabel(t, size, c);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(340, 0);
        return l;
    }

    private static Button Linha(ItemInfo? info, string def, string texto, bool marcado)
    {
        var b = new Button
        {
            Text = texto, Alignment = HorizontalAlignment.Left, ToggleMode = false, FocusMode = Control.FocusModeEnum.None,
            Icon = ItemIconRenderer.Get(info, def), ExpandIcon = false, CustomMinimumSize = new Vector2(0, 44),
        };
        b.AddThemeConstantOverride("icon_max_width", 36);
        if (marcado)
        {
            var st = new StyleBoxFlat { BgColor = new Color(0.3f, 0.2f, 0.45f, 0.9f), BorderColor = new Color(0.85f, 0.7f, 1f) };
            st.SetBorderWidthAll(2); st.SetCornerRadiusAll(4); st.SetContentMarginAll(4);
            b.AddThemeStyleboxOverride("normal", st);
            b.AddThemeStyleboxOverride("hover", st);
        }
        return b;
    }
}
