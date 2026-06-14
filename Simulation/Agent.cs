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
    private readonly Random _random;
    private readonly Values _values;
    private readonly float[] _inputs = new float[PlasticNetwork.InputCount];
    private readonly float[] _previousInputs = new float[PlasticNetwork.InputCount];
    private readonly List<WorldObject> _visibleObjects = [];
    private readonly MemoryPathPlanner _pathPlanner;
    private int _pathAge;
    private int _waypointIndex;

    public Agent(string name, AgentGender gender, Vector2 position, int neuronCount, int seed, Values values)
    {
        if (neuronCount < values.AgentMinimumNeuronCount)
            throw new ArgumentOutOfRangeException(nameof(neuronCount),
                $"An agent needs at least {values.AgentMinimumNeuronCount} neurons.");
        if (values.SensorRayCount * 2 + 3 != PlasticNetwork.InputCount)
            throw new ArgumentException("SensorRayCount does not match the fixed network inputs.", nameof(values));

        _values = values;
        Name = name;
        Gender = gender;
        Body = new AgentBody(values) { Position = position };
        Brain = new PlasticNetwork(neuronCount, seed, values);
        Memory = new AgentMemory(values);
        Rules = new RuleMemory(values);
        _pathPlanner = new MemoryPathPlanner(values);
        _random = new Random(seed ^ 0x5f3759df);
        Damage = _random.Next(values.AgentDamageMinimum, values.AgentDamageMaximum + 1);
        VisionRange = RandomBetween(values.SensorRangeMinimum, values.SensorRangeMaximum);
        VisionAngleDegrees = RandomBetween(
            values.SensorFieldOfViewMinimumDegrees,
            values.SensorFieldOfViewMaximumDegrees);
    }

    public string Name { get; }
    public AgentGender Gender { get; }
    public AgentBody Body { get; }
    public PlasticNetwork Brain { get; }
    public AgentMemory Memory { get; }
    public RuleMemory Rules { get; }
    public List<Motivation> Motivations { get; } = [];
    public int Damage { get; }
    public float VisionRange { get; }
    public float VisionAngleDegrees { get; }
    public float VisionAngleRadians => VisionAngleDegrees * MathF.PI / 180f;
    public float MaximumLifePoint => _values.AgentMaximumLifePoint;
    public bool IsAlive => Body.LifePoint > 0;
    public int FoodCollected { get; private set; }
    public int HazardHits { get; private set; }
    public float LastNovelty { get; private set; }
    public float LastReward { get; private set; }
    public string MotivationNames => string.Join(", ", Motivations.Select(m => m.Name));
    public string LastRuleAction { get; private set; } = "-";
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
        : $"Gelerntes Ziel, Nutzen {CurrentPlan.Utility:F2}";

    public void Observe(IReadOnlyList<WorldObject> objects, long stepNumber)
    {
        Memory.BeginStep();
        _visibleObjects.Clear();
        _visibleObjects.AddRange(VisibleObjects(objects));
        foreach (var item in _visibleObjects)
        {
            Memory.Remember(item, stepNumber);
        }

        Array.Clear(_inputs);
        var rayIndex = 0;
        foreach (var ray in SensorRays(objects))
        {
            var distance = Vector2.Distance(ray.start, ray.end);
            var strength = ray.hit is null ? 0f : 1f - distance / VisionRange;
            _inputs[rayIndex] = ray.hit == ObjectKind.Food ? strength : 0f;
            _inputs[_values.SensorRayCount + rayIndex] = ray.hit == ObjectKind.Hazard ? strength : 0f;
            rayIndex++;
        }

        var stateInput = _values.SensorRayCount * 2;
        _inputs[stateInput] = Body.LifePoint / _values.AgentMaximumLifePoint;
        _inputs[stateInput + 1] = Body.LeftMotor;
        _inputs[stateInput + 2] = Body.RightMotor;

        var difference = 0f;
        for (var i = 0; i < _values.SensorRayCount * 2; i++)
        {
            difference += MathF.Abs(_inputs[i] - _previousInputs[i]);
        }
        LastNovelty = Math.Clamp(difference / _values.NoveltyDivisor, 0f, 1f);
    }

    public void ThinkAndMove()
    {
        if (!IsAlive) return;

        var (left, right) = Brain.Step(_inputs, LastReward, LastNovelty);
        Body.LeftMotor = left;
        Body.RightMotor = right;

        UpdatePlan();
        var exploratoryTurn = ((float)_random.NextDouble() * 2f - 1f) * _values.AgentExplorationTurn;
        Body.Heading += (right - left) * _values.AgentMotorTurnFactor + exploratoryTurn;
        ApplyPlannedMovement();
        var ruleInfluencedMovement = ApplyRuleKnowledge();
        MovementDecisionCount++;
        if (ruleInfluencedMovement) RuleInfluencedDecisionCount++;

        var speed = _values.AgentBaseSpeed +
                    MathF.Max(0f, (left + right + 2f) * 0.5f) * _values.AgentMotorSpeedFactor;
        var direction = new Vector2(MathF.Cos(Body.Heading), MathF.Sin(Body.Heading));
        Body.Position += direction * speed;

        if (Body.Position.X < Body.Radius || Body.Position.X > 1f - Body.Radius)
        {
            Body.Heading = MathF.PI - Body.Heading;
            Body.Position = new Vector2(Math.Clamp(Body.Position.X, Body.Radius, 1f - Body.Radius), Body.Position.Y);
        }
        if (Body.Position.Y < Body.Radius || Body.Position.Y > 1f - Body.Radius)
        {
            Body.Heading = -Body.Heading;
            Body.Position = new Vector2(Body.Position.X, Math.Clamp(Body.Position.Y, Body.Radius, 1f - Body.Radius));
        }
    }

    private void UpdatePlan()
    {
        _pathAge++;
        var targetStillRemembered = CurrentPlan is not null &&
            Memory.WorldObjects.Contains(CurrentPlan.Target);
        if (_pathAge < _values.PathRecalculationInterval && targetStillRemembered) return;

        CurrentPlan = _pathPlanner.FindBestPlan(
            Body.Position,
            Body.LifePoint,
            MaximumLifePoint,
            Memory.WorldObjects,
            Rules);
        _pathAge = 0;
        _waypointIndex = 0;
    }

    private void ApplyPlannedMovement()
    {
        if (CurrentPlan is null || CurrentPlan.Waypoints.Count == 0) return;

        while (_waypointIndex < CurrentPlan.Waypoints.Count - 1 &&
               Vector2.Distance(Body.Position, CurrentPlan.Waypoints[_waypointIndex]) <=
               _values.PathWaypointReachedDistance)
        {
            _waypointIndex++;
        }

        var waypoint = CurrentPlan.Waypoints[_waypointIndex];
        var offset = waypoint - Body.Position;
        var targetAngle = MathF.Atan2(offset.Y, offset.X);
        var angleDifference = MathF.Atan2(
            MathF.Sin(targetAngle - Body.Heading),
            MathF.Cos(targetAngle - Body.Heading));
        Body.Heading += Math.Clamp(angleDifference, -_values.PathTurnFactor, _values.PathTurnFactor);
    }

    private bool ApplyRuleKnowledge()
    {
        RuleBehavior? requiredBehavior = CurrentPlan is null ? null : RuleBehavior.Avoid;
        var match = Rules.FindBestMatch(_visibleObjects, Body.Position, requiredBehavior);
        if (match is null)
        {
            LastRuleAction = "-";
            return false;
        }

        var target = match.Object.Position - Body.Position;
        var targetAngle = MathF.Atan2(target.Y, target.X);
        if (match.Rule.Behavior == RuleBehavior.Avoid) targetAngle += MathF.PI;

        var angleDifference = MathF.Atan2(
            MathF.Sin(targetAngle - Body.Heading),
            MathF.Cos(targetAngle - Body.Heading));
        var maximumTurn = _values.RuleTurnFactor * match.Rule.Strength;
        Body.Heading += Math.Clamp(angleDifference, -maximumTurn, maximumTurn);
        LastRuleAction = match.Rule.Behavior == RuleBehavior.Avoid ? "Meiden" : "Annaehern";
        return true;
    }

    public void CompleteStep(AgentStepOutcome outcome)
    {
        if (outcome.FoodCollected) FoodCollected++;
        if (outcome.HazardHit) HazardHits++;

        LastReward = Motivations.Count == 0
            ? 0f
            : Math.Clamp(Motivations.Sum(m => m.Evaluate(this, outcome) * m.Weight), -1f, 1f);

        Array.Copy(_inputs, _previousInputs, _inputs.Length);
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
            {
                yield return item;
            }
        }
    }

    private float RandomBetween(float minimum, float maximum) =>
        minimum + (float)_random.NextDouble() * (maximum - minimum);
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