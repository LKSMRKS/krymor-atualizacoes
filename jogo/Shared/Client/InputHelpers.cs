namespace CinzasDaMare.Shared.Client;

/// <summary>Entrada do jogador convertida em intenções. Tudo aqui é matemática pura (testável sem Godot).</summary>
public static class MoveInput
{
    /// <summary>Direção a partir das teclas, normalizada (diagonal não é mais rápida). "Cima" = y diminui.</summary>
    public static (double X, double Y) Direction(bool up, bool down, bool left, bool right)
    {
        double x = (right ? 1 : 0) - (left ? 1 : 0);
        double y = (down ? 1 : 0) - (up ? 1 : 0);
        var length = Math.Sqrt(x * x + y * y);
        return length > 0 ? (x / length, y / length) : (0, 0);
    }
}

public static class AimMath
{
    /// <summary>Interseção do raio da câmera com o chão (altura 0): devolve (x, z) ou null se o raio não desce.</summary>
    public static (double X, double Z)? GroundHit(double ox, double oy, double oz, double dx, double dy, double dz)
    {
        if (dy >= -1e-9) return null;
        var t = -oy / dy;
        if (t < 0) return null;
        return (ox + dx * t, oz + dz * t);
    }

    /// <summary>Vetor unitário de um ponto a outro; null se estiverem praticamente no mesmo lugar.</summary>
    public static (double X, double Y)? FacingTo(double fromX, double fromY, double toX, double toY)
    {
        var dx = toX - fromX;
        var dy = toY - fromY;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 0.05) return null;
        return (dx / length, dy / length);
    }
}

/// <summary>Só reenvia a orientação se passou tempo suficiente E ela mudou o bastante (evita inundar o servidor).</summary>
public sealed class FaceThrottle
{
    public const long MinIntervalMs = 100;
    public const double MinAngleDegrees = 3.0;

    private bool _hasSent;
    private double _lastX, _lastY;
    private long _lastSendMs;

    public void Reset() => _hasSent = false;

    public bool ShouldSend(double x, double y, long nowMs)
    {
        if (_hasSent)
        {
            if (nowMs - _lastSendMs < MinIntervalMs) return false;
            var dot = Math.Clamp(x * _lastX + y * _lastY, -1.0, 1.0);
            if (Math.Acos(dot) * 180.0 / Math.PI < MinAngleDegrees) return false;
        }
        _hasSent = true;
        _lastX = x;
        _lastY = y;
        _lastSendMs = nowMs;
        return true;
    }
}

/// <summary>Só envia MoveIntent quando a direção muda (o servidor já mantém a última direção).</summary>
public sealed class MoveSender
{
    private double _lastX, _lastY;

    public void Reset() { _lastX = 0; _lastY = 0; }

    public bool ShouldSend(double x, double y)
    {
        if (Math.Abs(x - _lastX) < 1e-9 && Math.Abs(y - _lastY) < 1e-9) return false;
        _lastX = x;
        _lastY = y;
        return true;
    }

    public const long MinIntervalMs = 50;
    public const double MinAngleDegrees = 3.0;
    private long _lastSendMs = long.MinValue / 2;

    /// <summary>
    /// Versão com limite (E8): com a câmera girando, a direção muda a cada quadro. Parar ou começar a andar sai na hora;
    /// mudança de direção só sai se passou 50 ms E virou pelo menos 3° (no máximo 20 por segundo, qualquer FPS).
    /// </summary>
    public bool ShouldSend(double x, double y, long nowMs)
    {
        var wasMoving = Math.Abs(_lastX) + Math.Abs(_lastY) > 1e-9;
        var isMoving = Math.Abs(x) + Math.Abs(y) > 1e-9;
        if (wasMoving != isMoving) return Accept(x, y, nowMs);
        if (!isMoving) return false;
        if (nowMs - _lastSendMs < MinIntervalMs) return false;
        var dot = Math.Clamp(x * _lastX + y * _lastY, -1.0, 1.0);
        if (Math.Acos(dot) * 180.0 / Math.PI < MinAngleDegrees) return false;
        return Accept(x, y, nowMs);
    }

    private bool Accept(double x, double y, long nowMs)
    {
        _lastX = x;
        _lastY = y;
        _lastSendMs = nowMs;
        return true;
    }
}

/// <summary>
/// E8: movimento relativo à câmera em 3ª pessoa. yaw = rotação da câmera em torno do eixo vertical (radianos,
/// convenção do Godot: yaw 0 olha para -Z, que é "norte" = y negativo no servidor).
/// </summary>
public static class CameraMove
{
    public static (double X, double Y) Forward(double yawRadians) => (-Math.Sin(yawRadians), -Math.Cos(yawRadians));

    public static (double X, double Y) Direction(bool up, bool down, bool left, bool right, double yawRadians)
    {
        double f = (up ? 1 : 0) - (down ? 1 : 0);
        double r = (right ? 1 : 0) - (left ? 1 : 0);
        if (f == 0 && r == 0) return (0, 0);
        var (fx, fy) = Forward(yawRadians);
        var (rx, ry) = (Math.Cos(yawRadians), -Math.Sin(yawRadians));
        var x = fx * f + rx * r;
        var y = fy * f + ry * r;
        var length = Math.Sqrt(x * x + y * y);
        return (x / length, y / length);
    }

    /// <summary>Yaw (radianos, convenção do Godot) que olha na direção (x,y) do servidor.</summary>
    public static double YawFor(double x, double y) => Math.Atan2(-x, -y);
}
