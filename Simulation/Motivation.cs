namespace PlasticBrainSim.Simulation;

public abstract class Motivation(string name, float weight = 1f)
{
    public string Name { get; } = name;
    public float Weight { get; set; } = weight;

    public abstract float Evaluate(Agent agent, AgentStepOutcome outcome);
}

public sealed class MaximizeLifePointMotivation(float weight = 1f)
    : Motivation("Lebenspunkte maximieren", weight)
{
    public override float Evaluate(Agent agent, AgentStepOutcome outcome)
    {
        if (outcome.Died) return -1f;

        var lifePointChange = outcome.LifePointAfter - outcome.LifePointBefore;
        return Math.Clamp(lifePointChange / agent.MaximumLifePoint, -1f, 1f);
    }
}