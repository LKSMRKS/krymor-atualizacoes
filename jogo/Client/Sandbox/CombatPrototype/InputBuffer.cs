using System.Collections.Generic;

namespace CinzasDaMare.Client.Sandbox.CombatPrototype;

/// <summary>
/// Fila de comandos com prazo (input buffering): "attack" apertado durante a recuperação do golpe anterior não se perde —
/// fica na fila até a máquina de estados abrir a janela de cancelamento, ou até vencer a janela (ms).
/// Fila de tamanho fixo (sem alocar por quadro); o mais antigo sai primeiro.
/// </summary>
public sealed class InputBuffer
{
    private readonly struct Entry
    {
        public readonly string Action;
        public readonly ulong AtMs;
        public Entry(string action, ulong atMs) { Action = action; AtMs = atMs; }
    }

    private const int Capacity = 6;
    private readonly Queue<Entry> _queue = new(Capacity);

    public int Count => _queue.Count;

    public void Push(string action, ulong nowMs)
    {
        if (_queue.Count >= Capacity) _queue.Dequeue();
        _queue.Enqueue(new Entry(action, nowMs));
    }

    /// <summary>Joga fora o que passou da janela.</summary>
    public void Expire(ulong nowMs, double windowMs)
    {
        while (_queue.Count > 0 && nowMs - _queue.Peek().AtMs > windowMs) _queue.Dequeue();
    }

    /// <summary>Consome o comando mais antigo se for um dos aceitos agora; senão deixa na fila.</summary>
    public bool TryConsume(ulong nowMs, double windowMs, out string action, params string[] accepted)
    {
        Expire(nowMs, windowMs);
        action = "";
        if (_queue.Count == 0) return false;
        var head = _queue.Peek().Action;
        for (var i = 0; i < accepted.Length; i++)
        {
            if (accepted[i] != head) continue;
            _queue.Dequeue();
            action = head;
            return true;
        }
        return false;
    }

    public string Peek() => _queue.Count > 0 ? _queue.Peek().Action : "";
    public void Clear() => _queue.Clear();

    /// <summary>Para o painel: "attack, dodge".</summary>
    public string Describe()
    {
        if (_queue.Count == 0) return "vazio";
        var parts = new List<string>(_queue.Count);
        foreach (var e in _queue) parts.Add(e.Action);
        return string.Join(", ", parts);
    }
}
