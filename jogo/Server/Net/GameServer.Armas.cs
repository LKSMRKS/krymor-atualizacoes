using CinzasDaMare.Shared.Content;

namespace CinzasDaMare.Server.Net;

/// <summary>12/11: o golpe básico usa os atributos do tipo da arma (alcance, velocidade, poder) — ver <see cref="EstiloArma"/>.</summary>
public sealed partial class GameServer
{
    public static EstiloArma Estilo(string kind, string? id = null) => EstiloArma.De(kind, id);
}
