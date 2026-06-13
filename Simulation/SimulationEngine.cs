using System.Numerics;

namespace PlasticBrainSim.Simulation;

public sealed class SimulationEngine
{
    private readonly Random _random;

    public SimulationEngine(Values? values = null)
    {
        Values = values ?? new Values();
        _random = new Random(Values.RandomSeed);

        var adam = CreateAgent("Adam", AgentGender.Male);
        adam.Motivations.Add(new MaximizeLifePointMotivation());
        Agents.Add(adam);

        for (var i = 0; i < Values.FoodObjectCount; i++) Objects.Add(CreateObject(ObjectKind.Food));
        for (var i = 0; i < Values.HazardObjectCount; i++) Objects.Add(CreateObject(ObjectKind.Hazard));
    }

    public Values Values { get; }
    public List<Agent> Agents { get; } = [];
    public List<WorldObject> Objects { get; } = [];
    public long StepCount { get; private set; }

    public void Step()
    {
        foreach (var agent in Agents.Where(a => a.IsAlive))
        {
            var lifePointBefore = agent.Body.LifePoint;
            agent.Observe(Objects, StepCount);
            agent.ThinkAndMove();

            agent.Body.LifePoint = Math.Max(
                0f,
                agent.Body.LifePoint - Values.AgentLifePointLossPerTick);

            var (foodCollected, hazardHit) = agent.IsAlive
                ? ResolveInteractions(agent)
                : (false, false);
            var died = !agent.IsAlive;

            agent.CompleteStep(new AgentStepOutcome(
                lifePointBefore,
                agent.Body.LifePoint,
                foodCollected,
                hazardHit,
                died));
        }

        StepCount++;
    }

    private Agent CreateAgent(string name, AgentGender gender)
    {
        var neuronCount = _random.Next(
            Values.AgentMinimumNeuronCount,
            Values.AgentMaximumInitialNeuronCount + 1);
        return new Agent(name, gender, RandomPosition(Values.SpawnMargin), neuronCount, _random.Next(), Values)
        {
            Body = { Heading = (float)(_random.NextDouble() * Math.Tau) }
        };
    }

    private (bool foodCollected, bool hazardHit) ResolveInteractions(Agent agent)
    {
        var foodCollected = false;
        var hazardHit = false;

        foreach (var item in Objects)
        {
            if (Vector2.Distance(agent.Body.Position, item.Position) > agent.Body.Radius + item.Radius)
                continue;

            if (item.Kind == ObjectKind.Food)
            {
                var beforeFood = agent.Body.LifePoint;
                agent.Body.LifePoint = MathF.Min(
                    Values.AgentMaximumLifePoint,
                    agent.Body.LifePoint + item.Food);
                agent.Rules.LearnFromContact(
                    item,
                    agent.Body.LifePoint - beforeFood,
                    StepCount);
                foodCollected = true;
                agent.Memory.Forget(item.Id);
                ResetObject(item);
                continue;
            }

            hazardHit = true;

            // The hazard attacks first. A dead agent can no longer retaliate.
            var beforeDamage = agent.Body.LifePoint;
            agent.Body.LifePoint = MathF.Max(0f, agent.Body.LifePoint - item.Damage);
            agent.Rules.LearnFromContact(
                item,
                agent.Body.LifePoint - beforeDamage,
                StepCount);
            if (!agent.IsAlive)
            {
                item.Position = RandomPosition(Values.ObjectSpawnMargin);
                break;
            }

            item.LifePoint = Math.Max(0, item.LifePoint - agent.Damage);
            if (item.LifePoint == 0)
            {
                agent.Body.LifePoint = MathF.Min(
                    Values.AgentMaximumLifePoint,
                    agent.Body.LifePoint + item.Food);
                foodCollected = true;
                agent.Memory.Forget(item.Id);
                ResetObject(item);
                continue;
            }

            item.Position = RandomPosition(Values.ObjectSpawnMargin);
            agent.Body.Heading += Values.AgentHazardTurn;
        }

        return (foodCollected, hazardHit);
    }

    private WorldObject CreateObject(ObjectKind kind)
    {
        var item = new WorldObject(
            kind,
            RandomPosition(Values.ObjectSpawnMargin),
            food: Values.FoodValueMinimum);
        ResetObject(item, keepPosition: true);
        return item;
    }

    private void ResetObject(WorldObject item, bool keepPosition = false)
    {
        if (item.Kind == ObjectKind.Hazard)
        {
            item.Food = _random.Next(Values.HazardFoodMinimum, Values.HazardFoodMaximum + 1);
            item.LifePoint = _random.Next(
                Values.HazardLifePointMinimum,
                Values.HazardLifePointMaximum + 1);
            item.Damage = _random.Next(Values.HazardDamageMinimum, Values.HazardDamageMaximum + 1);
            item.Color = RandomColor(
                Values.HazardColorRed,
                Values.HazardColorGreen,
                Values.HazardColorBlue);
        }
        else
        {
            item.Food = _random.Next(Values.FoodValueMinimum, Values.FoodValueMaximum + 1);
            item.LifePoint = 0;
            item.Damage = 0;
            item.Color = RandomColor(
                Values.FoodColorRed,
                Values.FoodColorGreen,
                Values.FoodColorBlue);
        }

        item.Size = CalculateObjectSize(item);

        if (!keepPosition) item.Position = RandomPosition(Values.ObjectSpawnMargin);
    }

    private Vector2 RandomPosition(float margin) => new(
        margin + (float)_random.NextDouble() * (1f - 2f * margin),
        margin + (float)_random.NextDouble() * (1f - 2f * margin));

    private float CalculateObjectSize(WorldObject item)
    {
        var minimumFood = item.Kind == ObjectKind.Food
            ? Values.FoodValueMinimum
            : Values.HazardFoodMinimum;
        var maximumFood = item.Kind == ObjectKind.Food
            ? Values.FoodValueMaximum
            : Values.HazardFoodMaximum;
        var normalized = (item.Food - minimumFood) / (float)Math.Max(1, maximumFood - minimumFood);
        var radius = Values.ObjectRadiusMinimum +
                     normalized * (Values.ObjectRadiusMaximum - Values.ObjectRadiusMinimum);
        return radius * 2f;
    }

    private ObjectColor RandomColor(byte red, byte green, byte blue) => new(
        VaryColor(red),
        VaryColor(green),
        VaryColor(blue));

    private byte VaryColor(byte value) => (byte)Math.Clamp(
        value + _random.Next(-Values.ObjectColorVariation, Values.ObjectColorVariation + 1),
        byte.MinValue,
        byte.MaxValue);
}
