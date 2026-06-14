namespace PlasticBrainSim.Simulation;

public sealed class AgentNetworks
{
    private readonly Dictionary<string, SemanticNetwork> _networks = [];

    public IReadOnlyDictionary<string, SemanticNetwork> All => _networks;
    public SemanticNetwork Meaning => _networks["Meaning"];

    public static AgentNetworks CreateDefault(int hiddenNeuronCount, int seed, Values values)
    {
        var result = new AgentNetworks();
        result._networks["Meaning"] = new SemanticNetwork(
            SemanticNetwork.InputCount,
            hiddenNeuronCount,
            seed,
            values);
        return result;
    }

    public void Set(string name, SemanticNetwork network) => _networks[name] = network;
}

public sealed class SemanticNetwork
{
    public const int InputCount = 7;
    private readonly Values _values;
    private readonly List<SemanticExperience> _experiences = [];

    public SemanticNetwork(int inputCount, int hiddenCount, int seed, Values values)
    {
        _values = values;
        InputNeuronCount = inputCount;
        HiddenNeuronCount = hiddenCount;
        var random = new Random(seed);
        InputWeights = CreateMatrix(hiddenCount, inputCount, random);
        HiddenBiases = CreateVector(hiddenCount, random);
        OutputWeights = CreateVector(hiddenCount, random);
        OutputBias = RandomWeight(random);
    }

    public int InputNeuronCount { get; }
    public int HiddenNeuronCount { get; }
    public float[][] InputWeights { get; }
    public float[] HiddenBiases { get; }
    public float[] OutputWeights { get; }
    public float OutputBias { get; set; }
    public int ExperienceCount => _experiences.Count;
    public int NeuronCount => InputNeuronCount + HiddenNeuronCount + 1;
    public int SynapseCount => InputNeuronCount * HiddenNeuronCount + HiddenNeuronCount;
    public IReadOnlyList<SemanticExperience> Experiences => _experiences;

    public SemanticAssessment Assess(ObjectColor color, ObjectShape shape, float size)
    {
        var input = Encode(color, shape, size);
        var hidden = ForwardHidden(input);
        return new SemanticAssessment(ForwardOutput(hidden), CalculateConfidence(input), hidden);
    }

    public SemanticTrainingResult Train(
        ObjectColor color,
        ObjectShape shape,
        float size,
        float targetValence)
    {
        var input = Encode(color, shape, size);
        var before = Assess(color, shape, size);
        targetValence = Math.Clamp(targetValence, -1f, 1f);

        for (var epoch = 0; epoch < _values.SemanticTrainingEpochsPerExperience; epoch++)
        {
            var hidden = ForwardHidden(input);
            var output = ForwardOutput(hidden);
            var outputDelta = (targetValence - output) * (1f - output * output);
            var oldOutputWeights = (float[])OutputWeights.Clone();

            for (var h = 0; h < HiddenNeuronCount; h++)
                OutputWeights[h] += _values.SemanticLearningRate * outputDelta * hidden[h];
            OutputBias += _values.SemanticLearningRate * outputDelta;

            for (var h = 0; h < HiddenNeuronCount; h++)
            {
                var hiddenDelta = outputDelta * oldOutputWeights[h] * (1f - hidden[h] * hidden[h]);
                for (var i = 0; i < InputNeuronCount; i++)
                    InputWeights[h][i] += _values.SemanticLearningRate * hiddenDelta * input[i];
                HiddenBiases[h] += _values.SemanticLearningRate * hiddenDelta;
            }
        }

        _experiences.Add(new SemanticExperience(input, targetValence));
        var after = Assess(color, shape, size);
        return new SemanticTrainingResult(before.Valence, after.Valence, targetValence, after.Confidence);
    }

    public void AddExperience(float[] input, float targetValence) =>
        _experiences.Add(new SemanticExperience((float[])input.Clone(), targetValence));

    private float CalculateConfidence(float[] input)
    {
        if (_experiences.Count == 0) return 0f;
        var nearestDistance = _experiences.Min(experience => Distance(input, experience.Input));
        var proximity = MathF.Exp(-nearestDistance * _values.SemanticConfidenceDistanceFactor);
        var experienceFactor = Math.Clamp(
            _experiences.Count / (float)Math.Max(1, _values.SemanticConfidenceExperienceCount),
            0f,
            1f);
        return Math.Clamp(proximity * experienceFactor, 0f, 1f);
    }

    private float[] ForwardHidden(float[] input)
    {
        var hidden = new float[HiddenNeuronCount];
        for (var h = 0; h < HiddenNeuronCount; h++)
        {
            var sum = HiddenBiases[h];
            for (var i = 0; i < InputNeuronCount; i++) sum += InputWeights[h][i] * input[i];
            hidden[h] = MathF.Tanh(sum);
        }
        return hidden;
    }

    private float ForwardOutput(float[] hidden)
    {
        var sum = OutputBias;
        for (var h = 0; h < HiddenNeuronCount; h++) sum += OutputWeights[h] * hidden[h];
        return MathF.Tanh(sum);
    }

    public static float[] Encode(ObjectColor color, ObjectShape shape, float size)
    {
        var input = new float[InputCount];
        input[0] = color.Red / 127.5f - 1f;
        input[1] = color.Green / 127.5f - 1f;
        input[2] = color.Blue / 127.5f - 1f;
        input[3] = Math.Clamp(size / 0.05f, 0f, 1f);
        input[4 + (int)shape] = 1f;
        return input;
    }

    private static float Distance(float[] left, float[] right)
    {
        var sum = 0f;
        for (var i = 0; i < left.Length; i++)
        {
            var difference = left[i] - right[i];
            sum += difference * difference;
        }
        return MathF.Sqrt(sum / left.Length);
    }

    private static float[][] CreateMatrix(int rows, int columns, Random random) =>
        Enumerable.Range(0, rows)
            .Select(_ => Enumerable.Range(0, columns).Select(_ => RandomWeight(random)).ToArray())
            .ToArray();

    private static float[] CreateVector(int count, Random random) =>
        Enumerable.Range(0, count).Select(_ => RandomWeight(random)).ToArray();

    private static float RandomWeight(Random random) => ((float)random.NextDouble() * 2f - 1f) * 0.35f;
}

public sealed record SemanticAssessment(float Valence, float Confidence, float[] MeaningVector);
public sealed record SemanticTrainingResult(float Before, float After, float Target, float Confidence);
public sealed record SemanticExperience(float[] Input, float TargetValence);
