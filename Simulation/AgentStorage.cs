using System.Numerics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlasticBrainSim.Simulation;

public static class AgentStorage
{
    public const int CurrentFormatVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Save(Agent agent, string? directory = null)
    {
        directory ??= GetStorageDirectory();
        Directory.CreateDirectory(directory);
        var safeName = string.Concat(agent.Name.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var path = Path.Combine(directory, $"{safeName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(CreateSnapshot(agent), JsonOptions));
        return path;
    }

    public static Agent Load(string path, Values values)
    {
        var snapshot = JsonSerializer.Deserialize<AgentSnapshot>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Die Agentendatei ist leer oder ungueltig.");
        if (snapshot.FormatVersion > CurrentFormatVersion)
            throw new InvalidDataException("Die Agentendatei stammt aus einer neueren Programmversion.");

        var agent = new Agent(
            snapshot.Name,
            snapshot.Gender,
            snapshot.Body.Position,
            snapshot.Network.HiddenNeuronCount,
            values.RandomSeed ^ snapshot.Name.GetHashCode(StringComparison.Ordinal),
            values);
        agent.Body.Position = snapshot.Body.Position;
        agent.Body.Heading = snapshot.Body.Heading;
        agent.Body.LifePoint = snapshot.Body.LifePoint;
        agent.Body.LeftMotor = snapshot.Body.LeftMotor;
        agent.Body.RightMotor = snapshot.Body.RightMotor;
        agent.RestoreScalarState(
            snapshot.Name,
            snapshot.Gender,
            snapshot.Damage,
            snapshot.VisionRange,
            snapshot.VisionAngleDegrees,
            snapshot.FoodCollected,
            snapshot.HazardHits,
            snapshot.LastNovelty,
            snapshot.LastReward,
            snapshot.MovementDecisionCount,
            snapshot.RuleInfluencedDecisionCount);

        RestoreNetwork(agent.Brain, snapshot.Network);
        agent.Memory.ReplaceAll(snapshot.Memories.Select(memory => memory.ToMemory()));
        agent.Rules.ReplaceAll(snapshot.Rules.Select(rule => rule.ToRule()));
        foreach (var cell in snapshot.ExplorationCells)
            agent.ExplorationMap.RestoreCell(cell.X, cell.Y, cell.VisitCount, cell.LastSeenStep);
        agent.Motivations.Add(new CuriosityMotivation(values));
        agent.Motivations.Add(new MaximizeLifePointMotivation(values));
        if (snapshot.CurrentPlan is { Waypoints.Count: > 0 } plan)
        {
            var targetMemory = plan.TargetObjectId is null
                ? null
                : agent.Memory.WorldObjects.FirstOrDefault(memory => memory.ObjectId == plan.TargetObjectId);
            agent.RestorePlan(
                new TargetCandidate(plan.MotivationName, plan.Position, plan.Utility, targetMemory),
                plan.Waypoints);
        }
        return agent;
    }

    public static string GetStorageDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "PlasticBrainSim",
        "Agents");

    private static AgentSnapshot CreateSnapshot(Agent agent) => new()
    {
        FormatVersion = CurrentFormatVersion,
        SavedAt = DateTimeOffset.Now,
        Name = agent.Name,
        Gender = agent.Gender,
        Damage = agent.Damage,
        VisionRange = agent.VisionRange,
        VisionAngleDegrees = agent.VisionAngleDegrees,
        FoodCollected = agent.FoodCollected,
        HazardHits = agent.HazardHits,
        LastNovelty = agent.LastNovelty,
        LastReward = agent.LastReward,
        MovementDecisionCount = agent.MovementDecisionCount,
        RuleInfluencedDecisionCount = agent.RuleInfluencedDecisionCount,
        Body = AgentBodySnapshot.From(agent.Body),
        Network = SemanticNetworkSnapshot.From(agent.Brain),
        Memories = agent.Memory.WorldObjects.Select(WorldObjectMemorySnapshot.From).ToList(),
        Rules = agent.Rules.Rules.Select(BehaviorRuleSnapshot.From).ToList(),
        ExplorationCells = agent.ExplorationMap.StoredCells()
            .Select(cell => new ExplorationCellSnapshot
            {
                X = cell.X,
                Y = cell.Y,
                VisitCount = cell.Cell.VisitCount,
                LastSeenStep = cell.Cell.LastSeenStep
            }).ToList(),
        CurrentPlan = agent.CurrentPlan is null ? null : new PathPlanSnapshot
        {
            MotivationName = agent.CurrentPlan.Target.MotivationName,
            Position = agent.CurrentPlan.Target.Position,
            Utility = agent.CurrentPlan.Target.Utility,
            TargetObjectId = agent.CurrentPlan.Target.Memory?.ObjectId,
            Waypoints = agent.CurrentPlan.Waypoints.ToList()
        }
    };

    private static void RestoreNetwork(SemanticNetwork network, SemanticNetworkSnapshot snapshot)
    {
        for (var h = 0; h < network.HiddenNeuronCount; h++)
        {
            Array.Copy(snapshot.InputWeights[h], network.InputWeights[h], network.InputNeuronCount);
            network.HiddenBiases[h] = snapshot.HiddenBiases[h];
            network.OutputWeights[h] = snapshot.OutputWeights[h];
        }
        network.OutputBias = snapshot.OutputBias;
        foreach (var experience in snapshot.Experiences)
            network.AddExperience(experience.Input, experience.TargetValence);
    }
}

public sealed class AgentSnapshot
{
    public int FormatVersion { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public string Name { get; set; } = "";
    public AgentGender Gender { get; set; }
    public int Damage { get; set; }
    public float VisionRange { get; set; }
    public float VisionAngleDegrees { get; set; }
    public int FoodCollected { get; set; }
    public int HazardHits { get; set; }
    public float LastNovelty { get; set; }
    public float LastReward { get; set; }
    public long MovementDecisionCount { get; set; }
    public long RuleInfluencedDecisionCount { get; set; }
    public AgentBodySnapshot Body { get; set; } = new();
    public SemanticNetworkSnapshot Network { get; set; } = new();
    public List<WorldObjectMemorySnapshot> Memories { get; set; } = [];
    public List<BehaviorRuleSnapshot> Rules { get; set; } = [];
    public List<ExplorationCellSnapshot> ExplorationCells { get; set; } = [];
    public PathPlanSnapshot? CurrentPlan { get; set; }
}

public sealed class AgentBodySnapshot
{
    public Vector2 Position { get; set; }
    public float Heading { get; set; }
    public float LifePoint { get; set; }
    public float LeftMotor { get; set; }
    public float RightMotor { get; set; }
    public static AgentBodySnapshot From(AgentBody body) => new()
    {
        Position = body.Position,
        Heading = body.Heading,
        LifePoint = body.LifePoint,
        LeftMotor = body.LeftMotor,
        RightMotor = body.RightMotor
    };
}

public sealed class SemanticNetworkSnapshot
{
    public int HiddenNeuronCount { get; set; }
    public float[][] InputWeights { get; set; } = [];
    public float[] HiddenBiases { get; set; } = [];
    public float[] OutputWeights { get; set; } = [];
    public float OutputBias { get; set; }
    public List<SemanticExperienceSnapshot> Experiences { get; set; } = [];
    public static SemanticNetworkSnapshot From(SemanticNetwork network) => new()
    {
        HiddenNeuronCount = network.HiddenNeuronCount,
        InputWeights = network.InputWeights.Select(row => (float[])row.Clone()).ToArray(),
        HiddenBiases = (float[])network.HiddenBiases.Clone(),
        OutputWeights = (float[])network.OutputWeights.Clone(),
        OutputBias = network.OutputBias,
        Experiences = network.Experiences.Select(experience => new SemanticExperienceSnapshot
        {
            Input = (float[])experience.Input.Clone(),
            TargetValence = experience.TargetValence
        }).ToList()
    };
}

public sealed class SemanticExperienceSnapshot
{
    public float[] Input { get; set; } = [];
    public float TargetValence { get; set; }
}

public sealed class WorldObjectMemorySnapshot
{
    public Guid ObjectId { get; set; }
    public ObjectKind Kind { get; set; }
    public Vector2 Position { get; set; }
    public int Food { get; set; }
    public int LifePoint { get; set; }
    public int Damage { get; set; }
    public ObjectColor Color { get; set; }
    public ObjectShape Shape { get; set; }
    public float Size { get; set; }
    public long LastSeenStep { get; set; }
    public int RemainingLifetime { get; set; }
    public float NetworkValence { get; set; }
    public float NetworkConfidence { get; set; }
    public float[] MeaningVector { get; set; } = [];
    public static WorldObjectMemorySnapshot From(WorldObjectMemory memory) => new()
    {
        ObjectId = memory.ObjectId,
        Kind = memory.Kind,
        Position = memory.Position,
        Food = memory.Food,
        LifePoint = memory.LifePoint,
        Damage = memory.Damage,
        Color = memory.Color,
        Shape = memory.Shape,
        Size = memory.Size,
        LastSeenStep = memory.LastSeenStep,
        RemainingLifetime = memory.RemainingLifetime,
        NetworkValence = memory.NetworkValence,
        NetworkConfidence = memory.NetworkConfidence,
        MeaningVector = (float[])memory.MeaningVector.Clone()
    };
    public WorldObjectMemory ToMemory() => new(ObjectId, Kind)
    {
        Position = Position,
        Food = Food,
        LifePoint = LifePoint,
        Damage = Damage,
        Color = Color,
        Shape = Shape,
        Size = Size,
        LastSeenStep = LastSeenStep,
        RemainingLifetime = RemainingLifetime,
        NetworkValence = NetworkValence,
        NetworkConfidence = NetworkConfidence,
        MeaningVector = MeaningVector
    };
}

public sealed class BehaviorRuleSnapshot
{
    public ObjectColor Color { get; set; }
    public ObjectShape Shape { get; set; }
    public float Size { get; set; }
    public float ColorTolerance { get; set; }
    public float SizeTolerance { get; set; }
    public RuleBehavior Behavior { get; set; }
    public RuleSource Source { get; set; }
    public float Strength { get; set; }
    public int EvidenceCount { get; set; }
    public long CreatedAtStep { get; set; }
    public long LastConfirmedStep { get; set; }
    public static BehaviorRuleSnapshot From(BehaviorRule rule) => new()
    {
        Color = rule.Color,
        Shape = rule.Shape,
        Size = rule.Size,
        ColorTolerance = rule.ColorTolerance,
        SizeTolerance = rule.SizeTolerance,
        Behavior = rule.Behavior,
        Source = rule.Source,
        Strength = rule.Strength,
        EvidenceCount = rule.EvidenceCount,
        CreatedAtStep = rule.CreatedAtStep,
        LastConfirmedStep = rule.LastConfirmedStep
    };
    public BehaviorRule ToRule() => new()
    {
        Color = Color,
        Shape = Shape,
        Size = Size,
        ColorTolerance = ColorTolerance,
        SizeTolerance = SizeTolerance,
        Behavior = Behavior,
        Source = Source,
        Strength = Strength,
        EvidenceCount = EvidenceCount,
        CreatedAtStep = CreatedAtStep,
        LastConfirmedStep = LastConfirmedStep
    };
}

public sealed class ExplorationCellSnapshot
{
    public int X { get; set; }
    public int Y { get; set; }
    public int VisitCount { get; set; }
    public long LastSeenStep { get; set; }
}

public sealed class PathPlanSnapshot
{
    public string MotivationName { get; set; } = "";
    public Vector2 Position { get; set; }
    public float Utility { get; set; }
    public Guid? TargetObjectId { get; set; }
    public List<Vector2> Waypoints { get; set; } = [];
}
