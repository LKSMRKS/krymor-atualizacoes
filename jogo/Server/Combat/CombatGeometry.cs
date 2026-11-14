namespace CinzasDaMare.Server.Combat;

public static class CombatGeometry
{
    /// <summary>
    /// Verdadeiro se o atacante está dentro do arco frontal do defensor: arco centrado na orientação,
    /// com abertura total de <paramref name="arcDegrees"/> (a borda exata conta como frontal).
    /// Sem orientação válida ou com os dois sobrepostos não há como medir o ângulo: conta como frontal.
    /// </summary>
    public static bool IsFrontal(double facingX, double facingY, double selfX, double selfY,
        double attackerX, double attackerY, double arcDegrees)
    {
        var facingLength = Math.Sqrt(facingX * facingX + facingY * facingY);
        var dx = attackerX - selfX;
        var dy = attackerY - selfY;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (facingLength < 1e-9 || distance < 1e-9) return true;

        var cosine = (facingX * dx + facingY * dy) / (facingLength * distance);
        var halfArc = arcDegrees * 0.5 * Math.PI / 180.0;
        return cosine >= Math.Cos(halfArc) - 1e-12;
    }
}
