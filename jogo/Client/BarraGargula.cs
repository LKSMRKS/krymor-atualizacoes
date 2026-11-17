using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Barra do HUD com a gárgula (pote de vida) e o dragão (pote de mana), pedido do dono (referência estilo Diablo).
/// Monta: líquido da vida + líquido da mana (shader pote_liquido) ATRÁS e a arte ui_barra_gargula.png POR CIMA
/// (o vidro do pote é transparente na arte, então o líquido aparece dentro). Tamanho de referência 2048×456 px (mesmo formato da referência do dono, ~4,5:1).
/// Uso: var barra = new BarraGargula(); hud.AddChild(barra); barra.Escala = 0.62f;  // ancorar embaixo no centro
///      barra.Vida = hp / maxHp; barra.Mana = mp / maxMp;   // o líquido sobe/desce suave e balança a cada mudança
///      barra.SlotRect(i) → retângulo do slot i (0..7; o 5º é o de destaque, maior); números 1..8 em y = NumeroY; barra.XpRect → canal da barra de XP.
/// </summary>
public partial class BarraGargula : Control
{
    public const float W = 2048f, H = 456f;
    // 17/11 (dono: "adiciona o botão direito ali na barra de poderes"): o grupo da esquerda ganhou um 5º quadrado
    // (Clique, Dir, Q, E, R — um pouco menores, no mesmo espaço; a arte ganhou 5 molduras, original em backups/ui_17-11);
    // o de destaque (T) passou a ser o 6º
    private static readonly Vector2[] SlotCentros =
    {
        new(572, 355), new(684, 355), new(796, 355), new(908, 355), new(1020, 355), new(1132, 355), new(1244, 355), new(1356, 355), new(1468, 355),
    };
    private const float SlotLado = 90f;
    public const int Slots = 9, Destaque = -1;   // 17/11 (dono: "não precisa deixar o T maior — tudo do mesmo tamanho e junto")
    private static readonly Rect2 Xp = new(532, 262, 984, 12);
    public const float NumeroY = 413f;

    private readonly PoteLiquido _vida, _mana;
    private float _escala = 0.6f;

    public float Vida { set => _vida.Alvo = Mathf.Clamp(value, 0, 1); }
    public float Mana { set => _mana.Alvo = Mathf.Clamp(value, 0, 1); }
    /// <summary>Troca a cor do líquido do pote da direita (Fôlego do Arqueiro amarelo, Mana azul...).</summary>
    public void CorMana(Color clara) => _mana.Cores(clara, clara.Darkened(0.85f));
    public static readonly Vector2 CentroVida = new(355, 289), CentroMana = new(1693, 289);
    public float Escala { get => _escala; set { _escala = value; Scale = Vector2.One * value; } }
    public Rect2 SlotRect(int i) { var l = 84f; return new(SlotCentros[i] - Vector2.One * l / 2f, Vector2.One * l); }
    public Rect2 XpRect => Xp;

    public BarraGargula()
    {
        CustomMinimumSize = new Vector2(W, H); Size = new Vector2(W, H); MouseFilter = MouseFilterEnum.Ignore;
        _vida = new PoteLiquido("ui_pote_mascara_vida", 0.1733f, 0.0591f, 0.4057f, 0.9496f, new Color(1f, 0.12f, 0.08f), new Color(0.22f, 0f, 0.02f));
        _mana = new PoteLiquido("ui_pote_mascara_mana", 0.8267f, 0.0591f, 0.4057f, 0.9496f, new Color(0.2f, 0.45f, 1f), new Color(0.02f, 0.03f, 0.22f));
        // camada de trás (o que fica atrás do vidro: braço do dragão, asas) -> líquido -> camada da frente
        var fundo = VisualAssets.Load("ui_barra_gargula_fundo.png");
        if (fundo is not null) AddChild(new TextureRect { Texture = fundo, Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore });
        AddChild(_vida); AddChild(_mana);
        var arte = VisualAssets.Load("ui_barra_gargula.png");
        if (arte is not null) AddChild(new TextureRect { Texture = arte, Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore });
        Scale = Vector2.One * _escala;
    }
}

/// <summary>Líquido de um pote (shader pote_liquido), do tamanho da barra inteira; a máscara recorta o interior.</summary>
public partial class PoteLiquido : ColorRect
{
    private ShaderMaterial? _m;

    public PoteLiquido() { }   // o Godot precisa de um construtor vazio
    private float _mostrado = 1f, _balanco, _fase;
    public float Alvo = 1f;

    public PoteLiquido(string mascara, float centroX, float meiaLargura, float topo, float fundo, Color clara, Color funda)
    {
        Size = new Vector2(BarraGargula.W, BarraGargula.H); MouseFilter = MouseFilterEnum.Ignore; Color = new Color(1, 1, 1, 0);   // shader com erro: não cobre a barra de branco
        _m = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/Shaders/pote_liquido.gdshader") };
        if (VisualAssets.Load(mascara + ".png") is { } masc) _m.SetShaderParameter("mascara", masc);
        _m.SetShaderParameter("centro_x_uv", centroX); _m.SetShaderParameter("meia_largura_uv", meiaLargura);
        _m.SetShaderParameter("topo_uv", topo); _m.SetShaderParameter("fundo_uv", fundo);
        _m.SetShaderParameter("cor_clara", clara); _m.SetShaderParameter("cor_funda", funda);
        Material = _m;
    }

    private Color _clara;
    public void Cores(Color clara, Color funda)
    {
        if (_m is null || clara == _clara) return;
        _clara = clara;
        _m.SetShaderParameter("cor_clara", clara); _m.SetShaderParameter("cor_funda", funda);
    }

    public override void _Process(double delta)
    {
        if (_m is null) return;
        var dt = (float)delta;
        var dif = Alvo - _mostrado;
        if (Mathf.Abs(dif) > 0.0005f) _balanco = Mathf.Min(1f, _balanco + Mathf.Abs(dif) * 3f * dt * 10f);
        _mostrado = Mathf.Lerp(_mostrado, Alvo, 1f - Mathf.Exp(-5f * dt));
        _balanco *= Mathf.Exp(-1.4f * dt);
        _fase += dt * 7f;
        _m.SetShaderParameter("nivel", _mostrado);
        _m.SetShaderParameter("balanco", _balanco);
        _m.SetShaderParameter("inclinacao", Mathf.Sin(_fase) * _balanco * 0.6f);
    }
}
