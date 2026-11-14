namespace CinzasDaMare.Shared;

public sealed class ContentBundle
{
    public IReadOnlyDictionary<string, ClassDefinition> Classes { get; }
    public IReadOnlyDictionary<string, SkillDefinition> Skills { get; }
    public IReadOnlyDictionary<string, ItemDefinition> Items { get; }
    public IReadOnlyDictionary<string, MonsterDefinition> Monsters { get; }
    public IReadOnlyDictionary<string, ConsumableDefinition> Consumables { get; }

    public ContentBundle(
        IReadOnlyDictionary<string, ClassDefinition> classes,
        IReadOnlyDictionary<string, SkillDefinition> skills,
        IReadOnlyDictionary<string, ItemDefinition> items,
        IReadOnlyDictionary<string, MonsterDefinition> monsters,
        IReadOnlyDictionary<string, ConsumableDefinition> consumables)
    {
        Classes = classes;
        Skills = skills;
        Items = items;
        Monsters = monsters;
        Consumables = consumables;
    }
}
