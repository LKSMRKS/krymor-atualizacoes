using System.Linq;
using System.Text.Json;

namespace CinzasDaMare.Shared;

public static class ContentLoader
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ContentBundle Load(string directory)
    {
        return new ContentBundle(
            Index(Read<ClassDefinition>(directory, "classes.json"), x => x.Id, "classes.json"),
            Index(Read<SkillDefinition>(directory, "skills.json"), x => x.Id, "skills.json"),
            Index(Read<ItemDefinition>(directory, "items.json").Select(CinzasDaMare.Shared.Content.PisoDeNivel.Aplicar).ToList(), x => x.Id, "items.json"),   // 14/11: piso por nível
            Index(Read<MonsterDefinition>(directory, "monsters.json"), x => x.Id, "monsters.json"),
            Index(Read<ConsumableDefinition>(directory, "consumables.json"), x => x.Id, "consumables.json"));
    }

    public static BalanceConfig LoadBalance(string directory)
    {
        var path = Path.Combine(directory, "balance.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Arquivo de conteúdo ausente: {path}");
        return JsonSerializer.Deserialize<BalanceConfig>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("balance.json vazio ou inválido.");
    }

    private static List<T> Read<T>(string directory, string file)
    {
        var path = Path.Combine(directory, file);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Arquivo de conteúdo ausente: {path}");
        var list = JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path), JsonOptions)
                   ?? throw new InvalidDataException($"{file}: conteúdo vazio ou inválido.");
        list.AddRange(ReadExtras<T>(directory, file));
        return list;
    }

    /// <summary>
    /// Arquivos extras "nome_*.json" (ex.: items_mata.json, gerados por Tools/gerar_regioes.py) somam ao arquivo principal.
    /// Assim o conteúdo feito à mão fica intacto e o gerado pode ser recriado. IDs repetidos continuam sendo erro.
    /// </summary>
    public static List<T> ReadExtras<T>(string directory, string file)
    {
        var result = new List<T>();
        var stem = Path.GetFileNameWithoutExtension(file);
        if (!Directory.Exists(directory)) return result;
        foreach (var extra in Directory.GetFiles(directory, stem + "_*.json").OrderBy(f => f, StringComparer.Ordinal))
            result.AddRange(JsonSerializer.Deserialize<List<T>>(File.ReadAllText(extra), JsonOptions)
                            ?? throw new InvalidDataException($"{Path.GetFileName(extra)}: conteúdo vazio ou inválido."));
        return result;
    }

    private static Dictionary<string, T> Index<T>(List<T> items, Func<T, string> id, string file)
    {
        var map = new Dictionary<string, T>();
        foreach (var item in items)
        {
            var key = id(item);
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidDataException($"{file}: entrada sem id.");
            if (!map.TryAdd(key, item))
                throw new InvalidDataException($"{file}: id duplicado '{key}'.");
        }
        return map;
    }
}
