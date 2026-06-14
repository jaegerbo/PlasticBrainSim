using System.Numerics;

namespace PlasticBrainSim.Simulation;

public enum ObjectKind
{
    Food,
    Hazard
}

public readonly record struct ObjectColor(byte Red, byte Green, byte Blue)
{
    public float DistanceTo(ObjectColor other)
    {
        var red = (Red - other.Red) / 255f;
        var green = (Green - other.Green) / 255f;
        var blue = (Blue - other.Blue) / 255f;
        return MathF.Sqrt(red * red + green * green + blue * blue) / MathF.Sqrt(3f);
    }
}

public sealed class WorldObject(
    ObjectKind kind,
    Vector2 position,
    int food,
    int lifePoint = 0,
    int damage = 0)
{
    public Guid Id { get; } = Guid.NewGuid();
    public ObjectKind Kind { get; } = kind;
    public Vector2 Position { get; set; } = position;
    public int Food { get; set; } = food;
    public int LifePoint { get; set; } = lifePoint;
    public int Damage { get; set; } = damage;
    public ObjectColor Color { get; set; }
    public float Size { get; set; }
    public float Radius => Size / 2f;
}