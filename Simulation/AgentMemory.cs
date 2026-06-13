using System.Numerics;

namespace PlasticBrainSim.Simulation;

public sealed class AgentMemory(Values values)
{
    private readonly List<WorldObjectMemory> _worldObjects = [];

    public IReadOnlyList<WorldObjectMemory> WorldObjects => _worldObjects;

    public void BeginStep()
    {
        for (var i = _worldObjects.Count - 1; i >= 0; i--)
        {
            _worldObjects[i].RemainingLifetime--;
            if (_worldObjects[i].RemainingLifetime <= 0)
            {
                _worldObjects.RemoveAt(i);
            }
        }
    }

    public void Remember(WorldObject item, long stepNumber)
    {
        var memory = _worldObjects.FirstOrDefault(m => m.ObjectId == item.Id);
        if (memory is null)
        {
            memory = new WorldObjectMemory(item.Id, item.Kind);
            _worldObjects.Add(memory);
        }

        memory.Position = item.Position;
        memory.Food = item.Food;
        memory.LifePoint = item.LifePoint;
        memory.Damage = item.Damage;
        memory.Color = item.Color;
        memory.Size = item.Size;
        memory.LastSeenStep = stepNumber;
        memory.RemainingLifetime = values.WorldObjectMemoryLifetime;
    }

    public void Forget(Guid objectId) =>
        _worldObjects.RemoveAll(memory => memory.ObjectId == objectId);
}

public sealed class WorldObjectMemory(Guid objectId, ObjectKind kind)
{
    public Guid ObjectId { get; } = objectId;
    public ObjectKind Kind { get; } = kind;
    public Vector2 Position { get; set; }
    public int Food { get; set; }
    public int LifePoint { get; set; }
    public int Damage { get; set; }
    public ObjectColor Color { get; set; }
    public float Size { get; set; }
    public long LastSeenStep { get; set; }
    public int RemainingLifetime { get; set; }
    public float EvaluatedUtility { get; set; }
    public bool IsCurrentTarget { get; set; }
    public RuleBehavior? LearnedBehavior { get; set; }
    public float LearnedConfidence { get; set; }
    public string Description => LearnedBehavior switch
    {
        RuleBehavior.Approach => "Als nuetzlich erkanntes Objekt",
        RuleBehavior.Avoid => "Als gefaehrlich erkanntes Objekt",
        _ => "Objekt mit unbekannter Bedeutung"
    };
    public string TargetText => IsCurrentTarget ? "Aktuelles Ziel" : "";
    public string LearnedMeaningText => LearnedBehavior switch
    {
        RuleBehavior.Approach => $"Annaehern, Sicherheit {LearnedConfidence:F2}",
        RuleBehavior.Avoid => $"Meiden, Sicherheit {LearnedConfidence:F2}",
        _ => "Keine passende gelernte Regel"
    };
    public string ColorText => $"RGB {Color.Red}, {Color.Green}, {Color.Blue}";
    public string PositionText => $"X {Position.X:F3}, Y {Position.Y:F3}";
}
