using System.Numerics;

namespace PlasticBrainSim.Simulation;

public sealed class PlasticNetwork
{
    public const int InputCount = 19;
    public const int OutputCount = 2;

    private readonly Random _random;
    private readonly Values _values;
    private readonly float[] _nextActivations;

    public PlasticNetwork(int neuronCount, int seed, Values values)
    {
        if (neuronCount < InputCount + OutputCount + values.NetworkMinimumHiddenNeuronCount)
            throw new ArgumentOutOfRangeException(nameof(neuronCount));

        _random = new Random(seed);
        _values = values;
        Neurons = new List<Neuron>(neuronCount);
        _nextActivations = new float[neuronCount];

        CreateNeurons(neuronCount);
        CreateInitialConnections();
    }

    public List<Neuron> Neurons { get; }
    public int SynapseCount => Neurons.Sum(n => n.Outgoing.Count);
    public int OutputStart => Neurons.Count - OutputCount;

    public (float left, float right) Step(ReadOnlySpan<float> inputs, float reward, float novelty)
    {
        for (var i = 0; i < InputCount; i++)
        {
            Neurons[i].Activation = Math.Clamp(inputs[i], -1f, 1f);
        }

        Array.Clear(_nextActivations);
        foreach (var source in Neurons)
        {
            foreach (var synapse in source.Outgoing)
            {
                _nextActivations[synapse.Target] += source.Activation * synapse.Weight;
            }
        }

        for (var i = InputCount; i < Neurons.Count; i++)
        {
            var neuron = Neurons[i];
            var raw = _nextActivations[i] + neuron.Bias;
            neuron.PreviousActivation = neuron.Activation;
            neuron.Activation = MathF.Tanh(raw);
            neuron.ActivityAverage = neuron.ActivityAverage * _values.NetworkActivitySmoothing +
                                     MathF.Abs(neuron.Activation) * (1f - _values.NetworkActivitySmoothing);

            // Homeostasis keeps neurons away from permanent silence or saturation.
            neuron.Bias += (_values.NetworkTargetActivity - neuron.ActivityAverage) *
                           _values.NetworkHomeostasisRate;
            neuron.Bias = Math.Clamp(
                neuron.Bias,
                _values.NetworkBiasMinimum,
                _values.NetworkBiasMaximum);
        }

        ApplyPlasticity(reward, novelty);
        if (novelty > _values.NetworkNoveltyGrowthThreshold &&
            _random.NextDouble() < novelty * _values.NetworkGrowthProbability)
        {
            GrowTrialConnection();
        }

        return (Neurons[OutputStart].Activation, Neurons[OutputStart + 1].Activation);
    }

    private void ApplyPlasticity(float reward, float novelty)
    {
        var modulation = Math.Clamp(
            reward * _values.NetworkRewardModulation + novelty * _values.NetworkNoveltyModulation,
            -1f,
            1f);

        foreach (var source in Neurons)
        {
            for (var i = source.Outgoing.Count - 1; i >= 0; i--)
            {
                var synapse = source.Outgoing[i];
                var target = Neurons[synapse.Target];
                var correlation = source.Activation * target.Activation;

                synapse.Weight += synapse.Plasticity * correlation * modulation;
                synapse.Weight *= 1f - _values.NetworkWeightDecay;
                synapse.Weight = Math.Clamp(
                    synapse.Weight,
                    -_values.NetworkWeightLimit,
                    _values.NetworkWeightLimit);
                synapse.Utility = synapse.Utility * _values.NetworkUtilitySmoothing +
                                  MathF.Abs(correlation * synapse.Weight) *
                                  (1f - _values.NetworkUtilitySmoothing);
                synapse.Age++;

                if (synapse.Age > _values.NetworkSynapseMinimumAge &&
                    MathF.Abs(synapse.Weight) < _values.NetworkSynapseMinimumWeight &&
                    synapse.Utility < _values.NetworkSynapseMinimumUtility)
                {
                    source.Outgoing.RemoveAt(i);
                }
            }
        }
    }

    private void GrowTrialConnection()
    {
        var sourceIndex = _random.Next(Neurons.Count - OutputCount);
        var source = Neurons[sourceIndex];
        if (source.Outgoing.Count >= _values.NetworkMaximumOutgoingConnections) return;

        var candidates = Neurons
            .Where(n => n.Id >= InputCount && n.Id != sourceIndex)
            .Where(n => Vector2.Distance(source.Position, n.Position) <= _values.NetworkConnectionRadius)
            .Where(n => source.Outgoing.All(s => s.Target != n.Id))
            .ToArray();

        if (candidates.Length == 0) return;
        var target = candidates[_random.Next(candidates.Length)];
        source.Outgoing.Add(new Synapse(
            target.Id,
            RandomWeight(_values.NetworkTrialWeightScale),
            _values.NetworkTrialPlasticity));
    }

    private void CreateNeurons(int count)
    {
        for (var i = 0; i < count; i++)
        {
            Vector2 position;
            if (i < InputCount)
            {
                position = new Vector2(0.04f, (i + 1f) / (InputCount + 1f));
            }
            else if (i >= count - OutputCount)
            {
                position = new Vector2(0.96f, (i - (count - OutputCount) + 1f) / 3f);
            }
            else
            {
                position = new Vector2(
                    0.1f + (float)_random.NextDouble() * 0.8f,
                    0.04f + (float)_random.NextDouble() * 0.92f);
            }

            Neurons.Add(new Neuron(i, position, RandomWeight(_values.NetworkInitialBiasScale)));
        }
    }

    private void CreateInitialConnections()
    {
        foreach (var source in Neurons.Take(Neurons.Count - OutputCount))
        {
            var nearby = Neurons
                .Where(n => n.Id >= InputCount && n.Id != source.Id && n.Position.X >= source.Position.X - 0.04f)
                .Select(n => (neuron: n, distance: Vector2.Distance(source.Position, n.Position)))
                .Where(x => x.distance <= _values.NetworkConnectionRadius)
                .OrderBy(_ => _random.Next())
                .Take(_values.NetworkInitialConnectionsPerNeuron);

            foreach (var candidate in nearby)
            {
                source.Outgoing.Add(new Synapse(
                    candidate.neuron.Id,
                    RandomWeight(_values.NetworkInitialWeightScale),
                    _values.NetworkInitialPlasticity));
            }
        }
    }

    private float RandomWeight(float scale) => ((float)_random.NextDouble() * 2f - 1f) * scale;
}

public sealed class Neuron(int id, Vector2 position, float bias)
{
    public int Id { get; } = id;
    public Vector2 Position { get; } = position;
    public float Activation { get; set; }
    public float PreviousActivation { get; set; }
    public float ActivityAverage { get; set; }
    public float Bias { get; set; } = bias;
    public List<Synapse> Outgoing { get; } = [];
}

public sealed class Synapse(int target, float weight, float plasticity)
{
    public int Target { get; } = target;
    public float Weight { get; set; } = weight;
    public float Plasticity { get; } = plasticity;
    public float Utility { get; set; }
    public int Age { get; set; }
}