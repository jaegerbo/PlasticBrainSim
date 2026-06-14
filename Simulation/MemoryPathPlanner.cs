using System.Numerics;

namespace PlasticBrainSim.Simulation;

public sealed class MemoryPathPlanner(Values values)
{
    private static readonly (int X, int Y)[] Directions =
    [
        (-1, -1), (0, -1), (1, -1),
        (-1, 0),           (1, 0),
        (-1, 1),  (0, 1),  (1, 1)
    ];

    public PathPlan? FindBestPlan(
        Vector2 start,
        float lifePoint,
        float maximumLifePoint,
        IReadOnlyList<WorldObjectMemory> memories,
        RuleMemory rules)
    {
        foreach (var memory in memories)
        {
            memory.EvaluatedUtility = 0f;
            memory.IsCurrentTarget = false;
            var assessment = rules.Assess(memory.Color, memory.Size);
            memory.LearnedBehavior = assessment?.Behavior;
            memory.LearnedConfidence = assessment?.Strength ?? 0f;
        }

        PathPlan? best = null;
        foreach (var target in memories.Where(memory =>
                     memory.LearnedBehavior == RuleBehavior.Approach))
        {
            var path = FindPath(start, target.Position, memories);
            if (path.Count == 0) continue;

            var pathLength = PathLength(start, path);
            var freshness = target.RemainingLifetime /
                            (float)Math.Max(1, values.WorldObjectMemoryLifetime);
            var missingLife = MathF.Max(0f, maximumLifePoint - lifePoint);
            var expectedBenefit = missingLife * target.LearnedConfidence;
            var utility = expectedBenefit * freshness - pathLength * values.PathDistanceCost;

            target.EvaluatedUtility = utility;
            if (utility <= values.PathMinimumTargetUtility) continue;
            if (best is null || utility > best.Utility)
            {
                best = new PathPlan(target, path, utility);
            }
        }

        if (best is not null) best.Target.IsCurrentTarget = true;
        return best;
    }

    private List<Vector2> FindPath(
        Vector2 startPosition,
        Vector2 targetPosition,
        IReadOnlyList<WorldObjectMemory> memories)
    {
        var size = Math.Max(8, values.PathGridSize);
        var start = ToCell(startPosition, size);
        var goal = ToCell(targetPosition, size);
        var blocked = BuildBlockedCells(size, memories, start, goal);
        var frontier = new PriorityQueue<GridCell, float>();
        var cameFrom = new Dictionary<GridCell, GridCell>();
        var cost = new Dictionary<GridCell, float> { [start] = 0f };
        frontier.Enqueue(start, 0f);

        while (frontier.TryDequeue(out var current, out _))
        {
            if (current == goal) return ReconstructPath(cameFrom, current, start, targetPosition, size);

            foreach (var (x, y) in Directions)
            {
                var next = new GridCell(current.X + x, current.Y + y);
                if (next.X < 0 || next.Y < 0 || next.X >= size || next.Y >= size) continue;
                if (blocked.Contains(next)) continue;
                if (x != 0 && y != 0 &&
                    (blocked.Contains(new GridCell(current.X + x, current.Y)) ||
                     blocked.Contains(new GridCell(current.X, current.Y + y)))) continue;

                var newCost = cost[current] + (x == 0 || y == 0 ? 1f : 1.41421356f);
                if (cost.TryGetValue(next, out var oldCost) && newCost >= oldCost) continue;

                cost[next] = newCost;
                cameFrom[next] = current;
                frontier.Enqueue(next, newCost + Heuristic(next, goal));
            }
        }

        return [];
    }

    private HashSet<GridCell> BuildBlockedCells(
        int size,
        IReadOnlyList<WorldObjectMemory> memories,
        GridCell start,
        GridCell goal)
    {
        var blocked = new HashSet<GridCell>();
        foreach (var hazard in memories.Where(memory =>
                     memory.LearnedBehavior == RuleBehavior.Avoid))
        {
            var radius = hazard.Size / 2f + values.AgentRadius + values.PathHazardSafetyMargin;
            var minimumX = Math.Max(0, (int)MathF.Floor((hazard.Position.X - radius) * size));
            var maximumX = Math.Min(size - 1, (int)MathF.Floor((hazard.Position.X + radius) * size));
            var minimumY = Math.Max(0, (int)MathF.Floor((hazard.Position.Y - radius) * size));
            var maximumY = Math.Min(size - 1, (int)MathF.Floor((hazard.Position.Y + radius) * size));

            for (var y = minimumY; y <= maximumY; y++)
            for (var x = minimumX; x <= maximumX; x++)
            {
                var cell = new GridCell(x, y);
                if (Vector2.Distance(CellCenter(cell, size), hazard.Position) <= radius)
                    blocked.Add(cell);
            }
        }

        blocked.Remove(start);
        blocked.Remove(goal);
        return blocked;
    }

    private static List<Vector2> ReconstructPath(
        Dictionary<GridCell, GridCell> cameFrom,
        GridCell current,
        GridCell start,
        Vector2 exactTarget,
        int size)
    {
        var cells = new List<GridCell> { current };
        while (current != start)
        {
            current = cameFrom[current];
            cells.Add(current);
        }

        cells.Reverse();
        var path = cells.Skip(1).Select(cell => CellCenter(cell, size)).ToList();
        path.Add(exactTarget);
        return Simplify(path);
    }

    private static List<Vector2> Simplify(List<Vector2> path)
    {
        if (path.Count < 3) return path;

        var result = new List<Vector2> { path[0] };
        var previousDirection = Vector2.Normalize(path[1] - path[0]);
        for (var i = 1; i < path.Count - 1; i++)
        {
            var direction = Vector2.Normalize(path[i + 1] - path[i]);
            if (Vector2.Dot(previousDirection, direction) < 0.999f)
            {
                result.Add(path[i]);
                previousDirection = direction;
            }
        }
        result.Add(path[^1]);
        return result;
    }

    private static float PathLength(Vector2 start, IReadOnlyList<Vector2> path)
    {
        var length = 0f;
        var previous = start;
        foreach (var point in path)
        {
            length += Vector2.Distance(previous, point);
            previous = point;
        }
        return length;
    }

    private static GridCell ToCell(Vector2 position, int size) => new(
        Math.Clamp((int)(position.X * size), 0, size - 1),
        Math.Clamp((int)(position.Y * size), 0, size - 1));

    private static Vector2 CellCenter(GridCell cell, int size) =>
        new((cell.X + 0.5f) / size, (cell.Y + 0.5f) / size);

    private static float Heuristic(GridCell from, GridCell to)
    {
        var dx = Math.Abs(from.X - to.X);
        var dy = Math.Abs(from.Y - to.Y);
        return Math.Max(dx, dy) + 0.41421356f * Math.Min(dx, dy);
    }

    private readonly record struct GridCell(int X, int Y);
}

public sealed record PathPlan(
    WorldObjectMemory Target,
    IReadOnlyList<Vector2> Waypoints,
    float Utility);