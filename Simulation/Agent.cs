using System.Numerics;

namespace PlasticBrainSim.Simulation;

public enum AgentGender
{
    Female,
    Male,
    Unspecified
}

public sealed class Agent
{
    private readonly Values _values;
    private readonly List<WorldObject> _visibleObjects = [];
    private readonly MemoryPathPlanner _pathPlanner;
    private int _pathAge;
    private int _waypointIndex;

    public Agent(string name, AgentGender gender, Vector2 position, int hiddenNeuronCount, int seed, Values values)
    {
        _values = values;
        Name = name;
        Gender = gender;
        Body = new AgentBody(values) { Position = position };
        Networks = AgentNetworks.CreateDefault(hiddenNeuronCount, seed, values);
        Memory = new AgentMemory(values);
        Rules = new RuleMemory(values);
        ExplorationMap = new ExplorationMap(values);
        _pathPlanner = new MemoryPathPlanner(values);
        var random = new Random(seed ^ 0x5f3759df);
        Damage = random.Next(values.AgentDamageMinimum, values.AgentDamageMaximum + 1);
        VisionRange = RandomBetween(random, values.SensorRangeMinimum, values.SensorRangeMaximum);
        VisionAngleDegrees = RandomBetween(
            random,
            values.SensorFieldOfViewMinimumDegrees,
            values.SensorFieldOfViewMaximumDegrees);
    }

    public string Name { get; private set; }
    public AgentGender Gender { get; private set; }
    public AgentBody Body { get; }
    public AgentNetworks Networks { get; }
    public SemanticNetwork Brain => Networks.Meaning;
    public AgentMemory Memory { get; }
    public RuleMemory Rules { get; }
    public ExplorationMap ExplorationMap { get; }
    public List<Motivation> Motivations { get; } = [];
    public int Damage { get; private set; }
    public float VisionRange { get; private set; }
    public float VisionAngleDegrees { get; private set; }
    public float VisionAngleRadians => VisionAngleDegrees * MathF.PI / 180f;
    public float MaximumLifePoint => _values.AgentMaximumLifePoint;
    public bool IsAlive => Body.LifePoint > 0;
    public int FoodCollected { get; private set; }
    public int HazardHits { get; private set; }
    public float LastNovelty { get; private set; }
    public float LastReward { get; private set; }
    public string MotivationNames => string.Join(", ", Motivations
        .OrderByDescending(motivation => motivation.LastActivity)
        .Select(motivation => $"{motivation.Name} ({motivation.LastActivity:F2})"));
    public string ActiveMotivationName { get; private set; } = "-";
    public string MotivationDisplayText => ActiveMotivationName switch
    {
        "Neugier" => "ist neugierig",
        "Lebenspunkte maximieren" => "hat Hunger",
        _ => ""
    };
    public string LastRuleAction { get; private set; } = "-";
    public SemanticTrainingResult? LastTraining { get; private set; }
    public long MovementDecisionCount { get; private set; }
    public long RuleInfluencedDecisionCount { get; private set; }
    public long NetworkOnlyDecisionCount
    {
        get => MovementDecisionCount - RuleInfluencedDecisionCount;
        set { }
    }
    public double RuleDecisionPercent => MovementDecisionCount == 0
        ? 0d
        : RuleInfluencedDecisionCount * 100d / MovementDecisionCount;
    public double NetworkDecisionPercent => 100d - RuleDecisionPercent;
    public PathPlan? CurrentPlan { get; private set; }
    public string CurrentTargetText => CurrentPlan is null
        ? "-"
        : $"{CurrentPlan.Target.MotivationName}, Nutzen {CurrentPlan.Target.Utility:F2}";

    public void Observe(IReadOnlyList<WorldObject> objects, long stepNumber)
    {
        Memory.BeginStep();
        _visibleObjects.Clear();
        _visibleObjects.AddRange(VisibleObjects(objects));
        ExplorationMap.Observe(Body.Position, stepNumber);

        foreach (var item in _visibleObjects)
        {
            ExplorationMap.Observe(item.Position, stepNumber);
            Memory.Remember(item, stepNumber);
            var memory = Memory.WorldObjects.First(entry => entry.ObjectId == item.Id);
            var network = Brain.Assess(item.Color, item.Shape, item.Size);
            var rule = Rules.Assess(item.Color, item.Shape, item.Size);
            memory.NetworkValence = network.Valence;
            memory.NetworkConfidence = network.Confidence;
            memory.MeaningVector = network.MeaningVector;
            memory.LearnedBehavior = rule?.Behavior;
            memory.LearnedConfidence = rule?.Strength ?? network.Confidence;
            Rules.ApplyNetworkHypothesis(
                item.Color,
                item.Shape,
                item.Size,
                network.Valence,
                network.Confidence,
                stepNumber);
        }

        LastNovelty = _visibleObjects.Count == 0
            ? 0f
            : _visibleObjects.Average(item =>
                1f - Brain.Assess(item.Color, item.Shape, item.Size).Confidence);
    }

    public void ThinkAndMove()
    {
        if (!IsAlive) return;

        UpdatePlan();
        ApplyPlannedMovement();
        var ruleInfluencedMovement = ApplyAvoidanceRule();
        MovementDecisionCount++;
        if (ruleInfluencedMovement) RuleInfluencedDecisionCount++;

        var direction = new Vector2(MathF.Cos(Body.Heading), MathF.Sin(Body.Heading));
        Body.LeftMotor = 1f;
        Body.RightMotor = 1f;
        Body.Position += direction * (_values.AgentBaseSpeed + _values.AgentMotorSpeedFactor);
        KeepInsideWorld();
    }

    public void LearnFromContact(WorldObject item, float experiencedEffect, long stepNumber)
    {
        Rules.LearnFromContact(item, experiencedEffect, stepNumber);
        var targetValence = Math.Clamp(experiencedEffect / MaximumLifePoint, -1f, 1f);
        LastTraining = Brain.Train(item.Color, item.Shape, item.Size, targetValence);

        foreach (var memory in Memory.WorldObjects)
        {
            var assessment = Brain.Assess(memory.Color, memory.Shape, memory.Size);
            memory.NetworkValence = assessment.Valence;
            memory.NetworkConfidence = assessment.Confidence;
            memory.MeaningVector = assessment.MeaningVector;
            Rules.ApplyNetworkHypothesis(
                memory.Color,
                memory.Shape,
                memory.Size,
                assessment.Valence,
                assessment.Confidence,
                stepNumber);
        }
        _pathAge = _values.PathRecalculationInterval;
    }

    private void UpdatePlan()
    {
        _pathAge++;
        if (CurrentPlan is not null &&
            Vector2.Distance(Body.Position, CurrentPlan.Target.Position) <=
            _values.PathWaypointReachedDistance)
        {
            CurrentPlan.Target.Memory?.Let(memory => memory.IsCurrentTarget = false);
            CurrentPlan = null;
        }

        var currentMemoryExists = CurrentPlan?.Target.Memory is null ||
            Memory.WorldObjects.Contains(CurrentPlan.Target.Memory);
        if (_pathAge < _values.PathRecalculationInterval && CurrentPlan is not null && currentMemoryExists)
            return;

        foreach (var memory in Memory.WorldObjects)
        {
            memory.IsCurrentTarget = false;
            memory.EvaluatedUtility = 0f;
        }

        var activeMotivation = Motivations
            .Select(motivation => (motivation, activity: motivation.MeasureActivity(this)))
            .Where(entry => entry.activity > 0f)
            .OrderByDescending(entry => entry.activity)
            .FirstOrDefault();
        ActiveMotivationName = activeMotivation.motivation?.Name ?? "-";
        if (activeMotivation.motivation is null)
        {
            CurrentPlan = null;
            return;
        }

        var candidates = activeMotivation.motivation.ProposeTargets(this).ToList();
        var candidate = candidates.OrderByDescending(target => target.Utility).FirstOrDefault();
        if (candidate is null)
        {
            CurrentPlan = null;
            return;
        }

        var currentCandidate = CurrentPlan is null
            ? null
            : candidates.FirstOrDefault(target =>
                ReferenceEquals(target.Memory, CurrentPlan.Target.Memory) &&
                (target.Memory is not null || Vector2.Distance(target.Position, CurrentPlan.Target.Position) < 0.001f));
        var currentUtility = currentCandidate?.Utility ?? CurrentPlan?.Target.Utility ?? float.NegativeInfinity;
        var sameTarget = CurrentPlan is not null &&
            (candidate.Memory is not null
                ? ReferenceEquals(candidate.Memory, CurrentPlan.Target.Memory)
                : CurrentPlan.Target.Memory is null &&
                  Vector2.Distance(candidate.Position, CurrentPlan.Target.Position) < 0.001f);
        var motivationChanged = CurrentPlan?.Target.MotivationName != candidate.MotivationName;
        var currentInvalid = CurrentPlan is null || !currentMemoryExists;
        var betterTarget = CurrentPlan is not null && candidate.Utility > currentUtility &&
            !sameTarget;
        if (currentInvalid || motivationChanged || betterTarget)
        {
            var plan = _pathPlanner.FindPath(Body.Position, candidate, Memory.WorldObjects);
            if (plan is not null)
            {
                CurrentPlan = plan;
                _waypointIndex = 0;
            }
        }
        else if (currentCandidate is not null && CurrentPlan is not null)
        {
            CurrentPlan = new PathPlan(currentCandidate, CurrentPlan.Waypoints);
        }

        CurrentPlan?.Target.Memory?.Let(memory => memory.IsCurrentTarget = true);
        _pathAge = 0;
    }

    private void ApplyPlannedMovement()
    {
        if (CurrentPlan is null || CurrentPlan.Waypoints.Count == 0) return;
        while (_waypointIndex < CurrentPlan.Waypoints.Count - 1 &&
               Vector2.Distance(Body.Position, CurrentPlan.Waypoints[_waypointIndex]) <=
               _values.PathWaypointReachedDistance)
            _waypointIndex++;

        TurnTowards(CurrentPlan.Waypoints[_waypointIndex], _values.PathTurnFactor);
    }

    private bool ApplyAvoidanceRule()
    {
        var match = Rules.FindBestMatch(_visibleObjects, Body.Position, RuleBehavior.Avoid);
        if (match is null)
        {
            LastRuleAction = "-";
            return false;
        }
        var away = Body.Position - match.Object.Position;
        TurnTowards(Body.Position + away, _values.RuleTurnFactor * match.Rule.Strength);
        LastRuleAction = "Meiden";
        return true;
    }

    private void TurnTowards(Vector2 target, float maximumTurn)
    {
        var offset = target - Body.Position;
        var targetAngle = MathF.Atan2(offset.Y, offset.X);
        var difference = MathF.Atan2(
            MathF.Sin(targetAngle - Body.Heading),
            MathF.Cos(targetAngle - Body.Heading));
        Body.Heading += Math.Clamp(difference, -maximumTurn, maximumTurn);
    }

    private void KeepInsideWorld()
    {
        if (Body.Position.X < Body.Radius || Body.Position.X > 1f - Body.Radius)
        {
            Body.Heading = MathF.PI - Body.Heading;
            Body.Position = new Vector2(
                Math.Clamp(Body.Position.X, Body.Radius, 1f - Body.Radius),
                Body.Position.Y);
        }
        if (Body.Position.Y < Body.Radius || Body.Position.Y > 1f - Body.Radius)
        {
            Body.Heading = -Body.Heading;
            Body.Position = new Vector2(
                Body.Position.X,
                Math.Clamp(Body.Position.Y, Body.Radius, 1f - Body.Radius));
        }
    }

    public void CompleteStep(AgentStepOutcome outcome)
    {
        if (outcome.FoodCollected) FoodCollected++;
        if (outcome.HazardHit) HazardHits++;
        LastReward = Math.Clamp(
            (outcome.LifePointAfter - outcome.LifePointBefore) / MaximumLifePoint,
            -1f,
            1f);
    }

    public void RestoreScalarState(
        string name,
        AgentGender gender,
        int damage,
        float visionRange,
        float visionAngleDegrees,
        int foodCollected,
        int hazardHits,
        float lastNovelty,
        float lastReward,
        long movementDecisionCount,
        long ruleInfluencedDecisionCount)
    {
        Name = name;
        Gender = gender;
        Damage = damage;
        VisionRange = visionRange;
        VisionAngleDegrees = visionAngleDegrees;
        FoodCollected = foodCollected;
        HazardHits = hazardHits;
        LastNovelty = lastNovelty;
        LastReward = lastReward;
        MovementDecisionCount = movementDecisionCount;
        RuleInfluencedDecisionCount = ruleInfluencedDecisionCount;
    }

    public void RestorePlan(TargetCandidate target, IReadOnlyList<Vector2> waypoints)
    {
        CurrentPlan = new PathPlan(target, waypoints);
        _waypointIndex = 0;
        _pathAge = 0;
    }

    public IEnumerable<(Vector2 start, Vector2 end, ObjectKind? hit)> SensorRays(
        IReadOnlyList<WorldObject> objects)
    {
        for (var ray = 0; ray < _values.SensorRayCount; ray++)
        {
            var angle = Body.Heading +
                (ray - (_values.SensorRayCount - 1) / 2f) *
                (VisionAngleRadians / (_values.SensorRayCount - 1));
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var bestDistance = VisionRange;
            ObjectKind? kind = null;
            foreach (var item in objects)
            {
                var projection = Vector2.Dot(item.Position - Body.Position, direction);
                if (projection <= 0 || projection > bestDistance) continue;
                var closest = Body.Position + direction * projection;
                if (Vector2.Distance(closest, item.Position) <= item.Radius + _values.SensorCollisionTolerance)
                {
                    bestDistance = projection;
                    kind = item.Kind;
                }
            }
            yield return (Body.Position, Body.Position + direction * bestDistance, kind);
        }
    }

    private IEnumerable<WorldObject> VisibleObjects(IReadOnlyList<WorldObject> objects)
    {
        var facing = new Vector2(MathF.Cos(Body.Heading), MathF.Sin(Body.Heading));
        var minimumDot = MathF.Cos(VisionAngleRadians / 2f);
        foreach (var item in objects)
        {
            var offset = item.Position - Body.Position;
            var distance = offset.Length();
            if (distance > VisionRange + item.Radius) continue;
            if (distance <= item.Radius || Vector2.Dot(Vector2.Normalize(offset), facing) >= minimumDot)
                yield return item;
        }
    }

    private static float RandomBetween(Random random, float minimum, float maximum) =>
        minimum + (float)random.NextDouble() * (maximum - minimum);
}

public sealed class AgentBody(Values values)
{
    public Vector2 Position { get; set; } = new(0.5f, 0.5f);
    public float Heading { get; set; }
    public float LifePoint { get; set; } = values.AgentInitialLifePoint;
    public float Radius => values.AgentRadius;
    public float LeftMotor { get; set; }
    public float RightMotor { get; set; }
}

public readonly record struct AgentStepOutcome(
    float LifePointBefore,
    float LifePointAfter,
    bool FoodCollected,
    bool HazardHit,
    bool Died);

internal static class AgentExtensions
{
    public static void Let<T>(this T value, Action<T> action) where T : class => action(value);
}
