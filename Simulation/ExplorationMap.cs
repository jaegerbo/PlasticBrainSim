using System.Numerics;

namespace PlasticBrainSim.Simulation;

public sealed class ExplorationMap(Values values)
{
    private readonly ExplorationCell[,] _cells = new ExplorationCell[
        values.ExplorationMapGridSize,
        values.ExplorationMapGridSize];

    public int GridSize => values.ExplorationMapGridSize;

    public void Observe(Vector2 position, long stepNumber)
    {
        var (x, y) = ToCell(position);
        var cell = _cells[x, y] ??= new ExplorationCell();
        cell.VisitCount++;
        cell.LastSeenStep = stepNumber;
    }

    public ExplorationTarget? FindLeastKnownTarget(Vector2 currentPosition)
    {
        ExplorationTarget? best = null;
        for (var y = 0; y < GridSize; y++)
        for (var x = 0; x < GridSize; x++)
        {
            var cell = _cells[x, y];
            var visits = cell?.VisitCount ?? 0;
            var position = CellCenter(x, y);
            var distance = Vector2.Distance(currentPosition, position);
            if (distance < 0.08f) continue;
            var utility = 1f / (1f + visits) + MathF.Min(distance, 0.5f) * 0.1f;
            if (best is null || utility > best.Value.Utility)
                best = new ExplorationTarget(position, utility);
        }
        return best;
    }

    public IEnumerable<(Vector2 Position, int VisitCount)> Cells()
    {
        for (var y = 0; y < GridSize; y++)
        for (var x = 0; x < GridSize; x++)
            yield return (CellCenter(x, y), _cells[x, y]?.VisitCount ?? 0);
    }

    public void RestoreCell(int x, int y, int visitCount, long lastSeenStep)
    {
        if (x < 0 || y < 0 || x >= GridSize || y >= GridSize) return;
        _cells[x, y] = new ExplorationCell
        {
            VisitCount = visitCount,
            LastSeenStep = lastSeenStep
        };
    }

    public IEnumerable<(int X, int Y, ExplorationCell Cell)> StoredCells()
    {
        for (var y = 0; y < GridSize; y++)
        for (var x = 0; x < GridSize; x++)
            if (_cells[x, y] is { } cell) yield return (x, y, cell);
    }

    private (int X, int Y) ToCell(Vector2 position) =>
        (Math.Clamp((int)(position.X * GridSize), 0, GridSize - 1),
         Math.Clamp((int)(position.Y * GridSize), 0, GridSize - 1));

    private Vector2 CellCenter(int x, int y) =>
        new((x + 0.5f) / GridSize, (y + 0.5f) / GridSize);
}

public sealed class ExplorationCell
{
    public int VisitCount { get; set; }
    public long LastSeenStep { get; set; }
}

public readonly record struct ExplorationTarget(Vector2 Position, float Utility);
