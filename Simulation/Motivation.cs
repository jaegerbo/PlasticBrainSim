using System.Numerics;

namespace PlasticBrainSim.Simulation;

public abstract class Motivation(string name)
{
    public string Name { get; } = name;
    public float LastActivity { get; private set; }

    public float MeasureActivity(Agent agent)
    {
        LastActivity = Math.Max(0f, CalculateActivity(agent));
        return LastActivity;
    }

    protected abstract float CalculateActivity(Agent agent);
    public abstract IEnumerable<TargetCandidate> ProposeTargets(Agent agent);
}

public sealed class CuriosityMotivation(Values values) : Motivation("Neugier")
{
    protected override float CalculateActivity(Agent agent) => values.CuriosityBaseActivity;

    public override IEnumerable<TargetCandidate> ProposeTargets(Agent agent)
    {
        var target = agent.ExplorationMap.FindLeastKnownTarget(agent.Body.Position);
        if (target is not null)
            yield return new TargetCandidate(Name, target.Value.Position, target.Value.Utility, null);
    }
}

public sealed class MaximizeLifePointMotivation(Values values)
    : Motivation("Lebenspunkte maximieren")
{
    protected override float CalculateActivity(Agent agent)
    {
        if (agent.Body.LifePoint >= values.HealthMotivationThreshold) return 0f;
        return 1f + (values.HealthMotivationThreshold - agent.Body.LifePoint) /
            Math.Max(1f, values.HealthMotivationThreshold);
    }

    public override IEnumerable<TargetCandidate> ProposeTargets(Agent agent)
    {
        var missingLife = MathF.Max(0f, agent.MaximumLifePoint - agent.Body.LifePoint);
        foreach (var memory in agent.Memory.WorldObjects.Where(memory =>
                     memory.NetworkValence > 0f && memory.NetworkConfidence > 0f))
        {
            var freshness = memory.RemainingLifetime /
                            (float)Math.Max(1, values.WorldObjectMemoryLifetime);
            var distance = Vector2.Distance(agent.Body.Position, memory.Position);
            var utility = missingLife * memory.NetworkValence * memory.NetworkConfidence * freshness -
                          distance * values.PathDistanceCost;
            memory.EvaluatedUtility = utility;
            yield return new TargetCandidate(Name, memory.Position, utility, memory);
        }
    }
}

public sealed record TargetCandidate(
    string MotivationName,
    Vector2 Position,
    float Utility,
    WorldObjectMemory? Memory);
