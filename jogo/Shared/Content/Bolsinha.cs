namespace CinzasDaMare.Shared.Content;

/// <summary>
/// Bolsinha (pedido 14/11): aba da mochila só para itens pequenos (1×1 — poções, joias, materiais). As casas dela são
/// gravadas como slot <see cref="Base"/>+n, então não encostam na grade principal e nada muda no banco.
/// </summary>
public static class Bolsinha
{
    public const int Base = 1000;
    public const int Colunas = 8;
    public const int Linhas = 5;
    public const int Casas = Colunas * Linhas;

    public static bool EhDaBolsinha(int slot) => slot >= Base && slot < Base + Casas;
}
