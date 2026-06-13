namespace PlasticBrainSim.Simulation;

/// <summary>
/// Central collection of all numeric simulation parameters. Properties are
/// mutable so a later editor can bind directly to one Values instance.
/// </summary>
public sealed class Values
{
    // World
    public int RandomSeed { get; set; } = 271828;
    public int DefaultSimulationSpeed { get; set; } = 3;
    public float SpawnMargin { get; set; } = 0.08f;
    public float ObjectSpawnMargin { get; set; } = 0.045f;
    public int FoodObjectCount { get; set; } = 18;
    public int HazardObjectCount { get; set; } = 12;

    // Agent
    public float AgentInitialLifePoint { get; set; } = 100f;
    public float AgentMaximumLifePoint { get; set; } = 100f;
    public float AgentLifePointLossPerTick { get; set; } = 0.1f;
    public int AgentDamageMinimum { get; set; } = 40;
    public int AgentDamageMaximum { get; set; } = 80;
    public float AgentRadius { get; set; } = 0.018f;
    public int AgentMinimumNeuronCount { get; set; } = 240;
    public int AgentMaximumInitialNeuronCount { get; set; } = 420;
    public float AgentExplorationTurn { get; set; } = 0.012f;
    public float AgentMotorTurnFactor { get; set; } = 0.055f;
    public float AgentBaseSpeed { get; set; } = 0.0012f;
    public float AgentMotorSpeedFactor { get; set; } = 0.0021f;
    public float AgentHazardTurn { get; set; } = MathF.PI * 0.7f;

    // Sensors
    public int SensorRayCount { get; set; } = 8;
    public float SensorRangeMinimum { get; set; } = 0.20f;
    public float SensorRangeMaximum { get; set; } = 0.28f;
    public float SensorFieldOfViewMinimumDegrees { get; set; } = 90f;
    public float SensorFieldOfViewMaximumDegrees { get; set; } = 120f;
    public float SensorCollisionTolerance { get; set; } = 0.012f;
    public float NoveltyDivisor { get; set; } = 4f;

    // Memory
    public int WorldObjectMemoryLifetime { get; set; } = 1200;

    // Planning
    public int PathGridSize { get; set; } = 42;
    public int PathRecalculationInterval { get; set; } = 12;
    public float PathHazardSafetyMargin { get; set; } = 0.018f;
    public float PathDistanceCost { get; set; } = 24f;
    public float PathMinimumTargetUtility { get; set; } = 0.5f;
    public float PathTurnFactor { get; set; } = 0.12f;
    public float PathWaypointReachedDistance { get; set; } = 0.025f;

    // Food and hazard
    public int FoodValueMinimum { get; set; } = 10;
    public int FoodValueMaximum { get; set; } = 100;
    public int HazardFoodMinimum { get; set; } = 10;
    public int HazardFoodMaximum { get; set; } = 20;
    public int HazardLifePointMinimum { get; set; } = 20;
    public int HazardLifePointMaximum { get; set; } = 60;
    public int HazardDamageMinimum { get; set; } = 30;
    public int HazardDamageMaximum { get; set; } = 50;
    public float ObjectRadiusMinimum { get; set; } = 0.007f;
    public float ObjectRadiusMaximum { get; set; } = 0.019f;
    public byte FoodColorRed { get; set; } = 70;
    public byte FoodColorGreen { get; set; } = 200;
    public byte FoodColorBlue { get; set; } = 105;
    public byte HazardColorRed { get; set; } = 225;
    public byte HazardColorGreen { get; set; } = 75;
    public byte HazardColorBlue { get; set; } = 75;
    public int ObjectColorVariation { get; set; } = 22;

    // Rule memory
    public float RuleColorTolerance { get; set; } = 0.22f;
    public float RuleSizeTolerance { get; set; } = 0.006f;
    public float RuleInitialStrength { get; set; } = 0.85f;
    public float RuleReinforcement { get; set; } = 0.12f;
    public float RuleMinimumStrength { get; set; } = 0.25f;
    public float RuleTurnFactor { get; set; } = 0.18f;
    public float RuleInfluenceRange { get; set; } = 0.32f;

    // Plastic network
    public float NetworkConnectionRadius { get; set; } = 0.23f;
    public int NetworkMinimumHiddenNeuronCount { get; set; } = 20;
    public int NetworkMaximumOutgoingConnections { get; set; } = 18;
    public float NetworkWeightDecay { get; set; } = 0.00008f;
    public float NetworkActivitySmoothing { get; set; } = 0.995f;
    public float NetworkTargetActivity { get; set; } = 0.08f;
    public float NetworkHomeostasisRate { get; set; } = 0.0004f;
    public float NetworkBiasMinimum { get; set; } = -0.5f;
    public float NetworkBiasMaximum { get; set; } = 0.5f;
    public float NetworkNoveltyGrowthThreshold { get; set; } = 0.22f;
    public float NetworkGrowthProbability { get; set; } = 0.08f;
    public float NetworkRewardModulation { get; set; } = 0.8f;
    public float NetworkNoveltyModulation { get; set; } = 0.12f;
    public float NetworkWeightLimit { get; set; } = 2.5f;
    public float NetworkUtilitySmoothing { get; set; } = 0.999f;
    public int NetworkSynapseMinimumAge { get; set; } = 4000;
    public float NetworkSynapseMinimumWeight { get; set; } = 0.025f;
    public float NetworkSynapseMinimumUtility { get; set; } = 0.002f;
    public float NetworkTrialWeightScale { get; set; } = 0.08f;
    public float NetworkTrialPlasticity { get; set; } = 0.0045f;
    public int NetworkInitialConnectionsPerNeuron { get; set; } = 7;
    public float NetworkInitialWeightScale { get; set; } = 0.55f;
    public float NetworkInitialPlasticity { get; set; } = 0.003f;
    public float NetworkInitialBiasScale { get; set; } = 0.03f;
}
