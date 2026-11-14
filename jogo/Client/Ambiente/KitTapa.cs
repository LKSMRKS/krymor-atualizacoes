using System;
using CinzasDaMare.Client.Visuals;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Tapa visual da Costa e do Porto (11/11, versão 2 — substitui a versão 1 do pacote TapaVisual).
/// Troca as peças que eram só formas geométricas por modelos com textura. Cada método devolve false se o .glb faltar,
/// e aí o desenho antigo continua valendo (nada quebra se um arquivo não estiver na pasta).
/// </summary>
public static class KitTapa
{
    private static bool G(Node3D n, string file, Vector3 at = default, float yaw = 0f, float s = 1f) => MapKit.Glb(n, file, at, yaw, s);

    private static void Rotulo(Node3D node, string text, Vector3 at)
        => node.AddChild(new Label3D
        {
            Text = text, FontSize = 34, OutlineSize = 8, PixelSize = 0.01f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = new Color(0.95f, 0.85f, 0.6f), Position = at,
        });

    /// <summary>MapView.AddProp: logo depois de `var wood = ...`, antes do `switch (p.Kind)`: `if (KitTapa.Prop(this, node, p.Kind)) return;`</summary>
    public static bool Prop(MapView v, Node3D node, string kind)
    {
        switch (kind)
        {
            case "tocha" when G(node, "cenario/cenario_tocha_chao"):
                v.AddFlame(node, new Vector3(0, 1.9f, 0), 1f, 1.3f, 6f);   // 13/11: tocha é chama pequena — clareia menos que o poste
                return true;
            case "placa" when G(node, "cenario/cenario_placa"): return true;
            case "tenda" when G(node, "cenario/cenario_tenda_lona"): return true;
            case "estandarte" when G(node, "cenario_estandarte_tridente"): return true;   // modelo que já existe no jogo
            case "espantalho" when G(node, "cenario/cenario_espantalho"): return true;
            case "feno" when G(node, "cenario/cenario_feno"): return true;
            case "regadores" when G(node, "cenario/cenario_regadores"): return true;
            case "cranios" when G(node, "cenario/cenario_cranios"): return true;
            case "ossada" when G(node, "cenario/cenario_ossada"): return true;
            case "redes" when G(node, "cenario/cenario_redes"): return true;
            case "barco_encalhado" when G(node, "cenario/cenario_barco_encalhado"): return true;
            case "acampamento_abandonado" when G(node, "cenario/cenario_acampamento_abandonado"): return true;
            case "cerca_quebrada" when G(node, "cenario/cenario_cerca_quebrada"): return true;
            // peças novas (kinds novos para o gerador de mapas / map_porto.json)
            case "mesa_cartas" when G(node, "cenario/cenario_mesa_cartas"):
                node.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.7f, 0.35f), LightEnergy = 0.9f, OmniRange = 3.5f, Position = new Vector3(-0.2f, 1.1f, -0.3f), ShadowEnabled = false });
                v.Collider(node.Position + new Vector3(0, 0.45f, 0), new CylinderShape3D { Radius = 0.8f, Height = 0.9f });
                return true;
            case "arvore_serpente" when G(node, "cenario/cenario_arvore_serpente"):
                v.Collider(node.Position + new Vector3(0, 2f, 0), new CylinderShape3D { Radius = 0.7f, Height = 4f });
                return true;
        }
        return false;
    }

    /// <summary>
    /// MapView.AddCraftObject, case "forja or bancada or ...": trocar `MapKit.TryBuild(this, node, propKind, wood);` por
    /// `if (!KitTapa.Oficina(this, node, propKind)) MapKit.TryBuild(this, node, propKind, wood);` (o som continua igual).
    /// </summary>
    public static bool Oficina(MapView v, Node3D node, string propKind)
    {
        switch (propKind)
        {
            case "caldeirao" when G(node, "cenario/cenario_caldeirao"):
                v.AddFlame(node, new Vector3(0, 0.08f, 0), 0.8f, 1.2f, 4f);
                Rotulo(node, "Caldeirão", new Vector3(0, 1.6f, 0));
                return true;
            case "panela" when G(node, "cenario/cenario_panela"):
                v.AddFlame(node, new Vector3(0, 0.1f, 0), 0.9f, 1.4f, 4f);
                Rotulo(node, "Panela", new Vector3(0, 1.8f, 0));
                return true;
            case "bancada" when G(node, "cenario/cenario_bancada"):
                Rotulo(node, "Bancada", new Vector3(0, 1.7f, 0));
                v.Collider(node.Position + new Vector3(0, 0.5f, 0), new BoxShape3D { Size = new Vector3(2.0f, 1.0f, 0.9f) });
                return true;
            case "bau_armazem" when G(node, "cenario/cenario_armazem"):
                Rotulo(node, "Armazém comunitário", new Vector3(0, 1.8f, 0));
                return true;
        }
        return false;
    }

    /// <summary>MapView.AddCraftObject, case "veio": logo depois de `var y = obj.Yields ?? "";`: `if (KitTapa.Veio(node, y)) return true;`</summary>
    public static bool Veio(Node3D node, string yields)
    {
        if (yields == "mat_tabua") return G(node, "cenario/cenario_arvore_tabua");
        if (yields == "mat_casca_serpente") return true;   // a própria Árvore da Serpente (prop) é o visual; o veio é só o ponto de coleta
        if (yields.Contains("erva")) return G(node, "cenario/cenario_moita_erva");
        if (yields.Contains("ferro"))   // pedra de minerar: rocha com cristais azuis (igual ao ícone do minério)
        {
            if (!G(node, "cenario/cenario_veio_ferro")) return false;
            node.AddChild(new OmniLight3D { LightColor = new Color(0.45f, 0.75f, 1f), LightEnergy = 0.7f, OmniRange = 3f, Position = new Vector3(0, 0.9f, 0), ShadowEnabled = false });
            return true;
        }
        return false;
    }

    /// <summary>
    /// MapView.AddObstacle, no começo do método (depois de `var h = ...`):
    /// <code>if (KitTapa.Obstaculo(this, Root, P(o.X, o.Y), o.Kind, (float)o.W, (float)o.H, h, (float)o.R, o.Solid)) return;</code>
    /// Muro/muralha/ruína/barricada viram módulos de 4 m (2 m na barricada) esticados para caber no tamanho do obstáculo.
    /// A colisão é a mesma de antes (caixa/cilindro do tamanho do obstáculo).
    /// </summary>
    public static bool Obstaculo(MapView v, Node3D root, Vector3 centro, string kind, float w, float d, float h, float r, bool solid)
    {
        switch (kind)
        {
            case "muro" or "muralha" or "ruina" or "barricada":
            {
                var mod = kind == "barricada" ? 2f : 4f;
                var alturaModelo = kind == "barricada" ? 2.2f : 3.2f;
                var espessuraModelo = kind == "barricada" ? 1.8f : 1.2f;
                var along = w >= d;
                var comprimento = along ? w : d; var espessura = along ? d : w;
                var n = Math.Max(1, (int)Math.Round(comprimento / mod));
                var seg = comprimento / n;
                var seed = (int)Math.Abs(centro.X * 13 + centro.Z * 7);
                for (var i = 0; i < n; i++)
                {
                    var off = -comprimento / 2f + seg * (i + 0.5f);
                    var node = new Node3D
                    {
                        Position = centro + (along ? new Vector3(off, 0, 0) : new Vector3(0, 0, off)),
                        RotationDegrees = new Vector3(0, (along ? 0 : 90) + (((seed + i) % 2 == 0) ? 0 : 180), 0),
                        Scale = new Vector3(seg / mod, h / alturaModelo, Math.Max(0.6f, espessura) / espessuraModelo),
                    };
                    root.AddChild(node);
                    var file = kind switch
                    {
                        "ruina" => "cenario/cenario_ruina_cal",
                        "barricada" => "cenario/cenario_barricada",
                        _ => (seed + i) % 3 == 0 ? "cenario/cenario_muro_cal_b" : "cenario/cenario_muro_cal",
                    };
                    if (!G(node, file)) { node.QueueFree(); return false; }
                }
                if (kind != "muralha" || solid)
                    v.Collider(centro + new Vector3(0, h / 2f, 0), new BoxShape3D { Size = new Vector3(w, h, d) });
                return true;
            }
            case "coluna":
            {
                var node = new Node3D { Position = centro, Scale = new Vector3(r / 0.7f, h / 4.1f, r / 0.7f) };
                root.AddChild(node);
                if (!G(node, "cenario/cenario_coluna")) { node.QueueFree(); return false; }
                v.Collider(centro + new Vector3(0, h / 2f, 0), new CylinderShape3D { Radius = r, Height = h });
                return true;
            }
        }
        return false;
    }
}
