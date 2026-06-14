using System.Globalization;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using PlasticBrainSim.Simulation;

namespace PlasticBrainSim;

public enum SimulationDisplayMode
{
    World,
    Brain
}

public sealed class SimulationView : FrameworkElement
{
    private static readonly Brush AgentBrush = new SolidColorBrush(Color.FromRgb(65, 155, 255));
    private static readonly Pen BorderPen = new(new SolidColorBrush(Color.FromRgb(55, 68, 82)), 1);

    public SimulationEngine? Engine { get; set; }
    public Agent? SelectedAgent { get; set; }
    public SimulationDisplayMode DisplayMode { get; set; } = SimulationDisplayMode.World;

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Engine is null || ActualWidth <= 0 || ActualHeight <= 0) return;

        var size = Math.Min(ActualWidth - 24, ActualHeight - 40);
        var rect = new Rect(
            Math.Max(12, (ActualWidth - size) / 2),
            28,
            Math.Max(100, size),
            Math.Max(100, size));

        if (DisplayMode == SimulationDisplayMode.World)
        {
            DrawTitle(dc, "Welt", rect.X, 4);
            DrawWorld(dc, rect);
        }
        else
        {
            DrawTitle(dc, SelectedAgent is null ? "Plastisches Netz" : $"Netz: {SelectedAgent.Name}", rect.X, 4);
            DrawBrain(dc, rect);
        }
    }

    private void DrawWorld(DrawingContext dc, Rect rect)
    {
        var engine = Engine!;
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(17, 24, 31)), BorderPen, rect);

        var selected = SelectedAgent ?? engine.Agents.FirstOrDefault();
        if (selected is not null)
        {
            DrawMemories(dc, rect, selected, engine.Values.WorldObjectMemoryLifetime);
            DrawPlannedPath(dc, rect, selected);
            DrawVisionCone(dc, rect, selected);
        }

        foreach (var item in engine.Objects)
        {
            var center = Map(item.Position, rect);
            var radius = item.Radius * rect.Width;
            dc.DrawEllipse(new SolidColorBrush(ToMediaColor(item.Color)), null, center, radius, radius);
        }

        foreach (var agent in engine.Agents)
        {
            var body = agent.Body;
            var agentCenter = Map(body.Position, rect);
            var agentRadius = body.Radius * rect.Width;
            var outline = ReferenceEquals(agent, selected) ? new Pen(Brushes.White, 2.2) : new Pen(Brushes.Gray, 1);
            dc.DrawEllipse(AgentBrush, outline, agentCenter, agentRadius, agentRadius);
            var nose = body.Position + new Vector2(MathF.Cos(body.Heading), MathF.Sin(body.Heading)) * body.Radius * 1.8f;
            dc.DrawLine(new Pen(Brushes.White, 2), agentCenter, Map(nose, rect));
            DrawTitle(dc, agent.Name, agentCenter.X + agentRadius + 3, agentCenter.Y - 8);
        }
    }

    private static void DrawPlannedPath(DrawingContext dc, Rect rect, Agent agent)
    {
        var plan = agent.CurrentPlan;
        if (plan is null || plan.Waypoints.Count == 0) return;

        var pen = new Pen(new SolidColorBrush(Color.FromArgb(190, 255, 215, 80)), 2)
        {
            DashStyle = DashStyles.Dot
        };
        var previous = Map(agent.Body.Position, rect);
        foreach (var waypoint in plan.Waypoints)
        {
            var next = Map(waypoint, rect);
            dc.DrawLine(pen, previous, next);
            previous = next;
        }

        var target = Map(plan.Target.Position, rect);
        dc.DrawEllipse(null, new Pen(Brushes.Gold, 2.5), target, 9, 9);
    }

    private static void DrawVisionCone(DrawingContext dc, Rect rect, Agent agent)
    {
        const int segments = 28;
        var center = Map(agent.Body.Position, rect);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(center, true, true);
            var startAngle = agent.Body.Heading - agent.VisionAngleRadians / 2f;
            for (var i = 0; i <= segments; i++)
            {
                var angle = startAngle + agent.VisionAngleRadians * i / segments;
                var edge = agent.Body.Position +
                           new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * agent.VisionRange;
                context.LineTo(Map(edge, rect), true, false);
            }
        }
        geometry.Freeze();

        var fill = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255));
        var outline = new Pen(new SolidColorBrush(Color.FromArgb(85, 255, 255, 255)), 1);
        dc.DrawGeometry(fill, outline, geometry);
    }

    private static void DrawMemories(
        DrawingContext dc,
        Rect rect,
        Agent agent,
        int maximumLifetime)
    {
        foreach (var memory in agent.Memory.WorldObjects)
        {
            var lifetime = memory.RemainingLifetime / (float)Math.Max(1, maximumLifetime);
            var alpha = (byte)Math.Clamp(20 + lifetime * 80, 20, 100);
            var color = Color.FromArgb(
                alpha,
                memory.Color.Red,
                memory.Color.Green,
                memory.Color.Blue);
            var pen = new Pen(new SolidColorBrush(color), 1.4) { DashStyle = DashStyles.Dash };
            var center = Map(memory.Position, rect);
            var radius = Math.Max(4, memory.Size * rect.Width / 2f);
            dc.DrawEllipse(null, pen, center, radius, radius);
        }
    }

    private void DrawBrain(DrawingContext dc, Rect rect)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(12, 17, 23)), BorderPen, rect);
        var brain = SelectedAgent?.Brain ?? Engine!.Agents.FirstOrDefault()?.Brain;
        if (brain is null) return;

        foreach (var source in brain.Neurons)
        {
            foreach (var synapse in source.Outgoing)
            {
                if (MathF.Abs(synapse.Weight) < 0.08f) continue;
                var alpha = (byte)Math.Clamp(18 + MathF.Abs(synapse.Weight) * 45, 18, 120);
                var color = synapse.Weight >= 0
                    ? Color.FromArgb(alpha, 80, 170, 255)
                    : Color.FromArgb(alpha, 255, 105, 100);
                dc.DrawLine(new Pen(new SolidColorBrush(color), 0.65),
                    Map(source.Position, rect), Map(brain.Neurons[synapse.Target].Position, rect));
            }
        }

        foreach (var neuron in brain.Neurons)
        {
            var magnitude = MathF.Abs(neuron.Activation);
            var color = neuron.Id < PlasticNetwork.InputCount
                ? Color.FromRgb(74, 205, 150)
                : neuron.Id >= brain.OutputStart
                    ? Color.FromRgb(255, 194, 86)
                    : Color.FromRgb((byte)(75 + magnitude * 160), (byte)(87 + magnitude * 120), 190);
            var radius = neuron.Id < PlasticNetwork.InputCount || neuron.Id >= brain.OutputStart ? 3.4 : 1.5 + magnitude * 2.1;
            dc.DrawEllipse(new SolidColorBrush(color), null, Map(neuron.Position, rect), radius, radius);
        }
    }

    private static Point Map(Vector2 value, Rect rect) =>
        new(rect.X + value.X * rect.Width, rect.Y + value.Y * rect.Height);

    private static Color ToMediaColor(ObjectColor color) =>
        Color.FromRgb(color.Red, color.Green, color.Blue);

    private static void DrawTitle(DrawingContext dc, string text, double x, double y)
    {
        dc.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI Semibold"), 14, Brushes.White, 1.0), new Point(x, y));
    }
}