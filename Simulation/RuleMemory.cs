using System.Numerics;

namespace PlasticBrainSim.Simulation;

public enum RuleBehavior
{
    Approach,
    Avoid
}

public sealed class RuleMemory(Values values)
{
    private readonly List<BehaviorRule> _rules = [];

    public IReadOnlyList<BehaviorRule> Rules => _rules;

    public void LearnFromContact(WorldObject item, float lifePointChange, long stepNumber)
    {
        if (lifePointChange == 0) return;

        var behavior = lifePointChange < 0 ? RuleBehavior.Avoid : RuleBehavior.Approach;
        var existing = _rules
            .Where(rule => rule.Behavior == behavior)
            .OrderBy(rule => MatchDistance(rule, item.Color, item.Size))
            .FirstOrDefault(rule => Matches(rule, item.Color, item.Size));

        if (existing is null)
        {
            _rules.Add(new BehaviorRule
            {
                Color = item.Color,
                Size = item.Size,
                ColorTolerance = values.RuleColorTolerance,
                SizeTolerance = values.RuleSizeTolerance,
                Behavior = behavior,
                Strength = values.RuleInitialStrength,
                CreatedAtStep = stepNumber,
                LastConfirmedStep = stepNumber,
                EvidenceCount = 1
            });
            return;
        }

        existing.Strength = Math.Min(1f, existing.Strength + values.RuleReinforcement);
        existing.LastConfirmedStep = stepNumber;
        existing.EvidenceCount++;
    }

    public RuleMatch? FindBestMatch(
        IEnumerable<WorldObject> visibleObjects,
        Vector2 agentPosition,
        RuleBehavior? requiredBehavior = null)
    {
        RuleMatch? best = null;
        foreach (var item in visibleObjects)
        {
            var distance = Vector2.Distance(agentPosition, item.Position);
            if (distance > values.RuleInfluenceRange) continue;

            foreach (var rule in _rules.Where(rule =>
                         rule.Strength >= values.RuleMinimumStrength &&
                         (requiredBehavior is null || rule.Behavior == requiredBehavior)))
            {
                if (!Matches(rule, item.Color, item.Size)) continue;
                var score = rule.Strength * (1f - distance / values.RuleInfluenceRange);
                if (best is null || score > best.Score)
                {
                    best = new RuleMatch(rule, item, score);
                }
            }
        }

        return best;
    }

    public RuleAssessment? Assess(ObjectColor color, float size)
    {
        return _rules
            .Where(rule => rule.Strength >= values.RuleMinimumStrength &&
                           Matches(rule, color, size))
            .Select(rule => new RuleAssessment(
                rule.Behavior,
                rule.Strength,
                rule.EvidenceCount,
                MatchDistance(rule, color, size)))
            .OrderByDescending(assessment => assessment.Strength)
            .ThenByDescending(assessment => assessment.EvidenceCount)
            .ThenBy(assessment => assessment.Behavior == RuleBehavior.Avoid ? 0 : 1)
            .ThenBy(assessment => assessment.MatchDistance)
            .FirstOrDefault();
    }

    private bool Matches(BehaviorRule rule, ObjectColor color, float size) =>
        rule.Color.DistanceTo(color) <= values.RuleColorTolerance &&
        MathF.Abs(rule.Size - size) <= values.RuleSizeTolerance;

    private float MatchDistance(BehaviorRule rule, ObjectColor color, float size) =>
        rule.Color.DistanceTo(color) + MathF.Abs(rule.Size - size);
}

public sealed class BehaviorRule
{
    public ObjectColor Color { get; init; }
    public float Size { get; init; }
    public float ColorTolerance { get; init; }
    public float SizeTolerance { get; init; }
    public RuleBehavior Behavior { get; init; }
    public float Strength { get; set; }
    public int EvidenceCount { get; set; }
    public long CreatedAtStep { get; init; }
    public long LastConfirmedStep { get; set; }
    public string Description => Behavior == RuleBehavior.Avoid
        ? "Objekte mit diesen Merkmalen meiden"
        : "Objekten mit diesen Merkmalen annaehern";
    public string ColorText => $"RGB {Color.Red}, {Color.Green}, {Color.Blue}";
    public string SizeRangeText =>
        $"{Math.Max(0f, Size - SizeTolerance):F3} bis {Size + SizeTolerance:F3}";
    public string ColorToleranceText => $"max. Abweichung {ColorTolerance:F2}";
}

public sealed record RuleMatch(BehaviorRule Rule, WorldObject Object, float Score);

public sealed record RuleAssessment(
    RuleBehavior Behavior,
    float Strength,
    int EvidenceCount,
    float MatchDistance);
