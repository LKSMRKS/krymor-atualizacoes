namespace CinzasDaMare.Client;

/// <summary>
/// Ponte para as classes de ambiente do pacote de arte (VidaAmbiente): a hora do jogo vem do servidor
/// (WorldTimeMessage → MapView.SetHour), que preenche estes valores. O CicloDoDia completo do Cal Lote 2 não é usado
/// (o MapView já faz céu, sol/lua, névoa e entardecer a partir do clima de cada tema).
/// </summary>
public static class CicloDoDia
{
    /// <summary>0 = dia pleno, 1 = noite fechada.</summary>
    public static float Noite { get; set; }
    /// <summary>Multiplicador das chamas e tochas (mais fortes à noite).</summary>
    public static float LuzDasTochas => 1f + 0.7f * Noite;
}
