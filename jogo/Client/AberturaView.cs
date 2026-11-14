using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 12/11: abertura do KRYMOR (Assets/Video/abertura.ogv, gerada por Tools/gerar_abertura.py a partir do vídeo do dono).
/// Toca por cima de tudo ao abrir o jogo (o login/conexão continua por trás); qualquer tecla ou clique pula.
/// Não toca em automação (--shots / --quit-after / --vitrine) nem com --sem-abertura. Sem o arquivo, não faz nada.
/// </summary>
public sealed partial class AberturaView : CanvasLayer
{
    private const string Arquivo = "res://Assets/Video/abertura.ogv";
    private VideoStreamPlayer? _player;
    private ColorRect? _fundo;
    private bool _saindo;

    public static bool Tocar(Node pai)
    {
        if (!ResourceLoader.Exists(Arquivo) && !Godot.FileAccess.FileExists(Arquivo)) return false;
        var v = new AberturaView();
        pai.AddChild(v);
        return true;
    }

    public override void _Ready()
    {
        Layer = 120;
        _fundo = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Stop };
        _fundo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_fundo);
        var stream = new VideoStreamTheora { File = Arquivo };
        _player = new VideoStreamPlayer { Stream = stream, Expand = true, Autoplay = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _player.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_player);
        var dica = HudView.MakeLabel("qualquer tecla pula", 13, new Color(0.7f, 0.72f, 0.8f, 0.55f));
        dica.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        dica.Position = new Vector2(-170, -34);
        AddChild(dica);
        _player.Finished += Sair;
        _player.Play();
        Sfx.Play("ui_abrir", -12f);
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true } or InputEventMouseButton { Pressed: true })
        {
            GetViewport().SetInputAsHandled();
            Sair();
        }
    }

    private void Sair()
    {
        if (_saindo) return;
        _saindo = true;
        _player?.Stop();
        var tw = CreateTween();
        foreach (var c in GetChildren())
            if (c is CanvasItem ci) tw.Parallel().TweenProperty(ci, "modulate:a", 0f, 0.5);
        tw.TweenCallback(Callable.From(QueueFree));
    }
}
