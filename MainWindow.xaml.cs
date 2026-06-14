using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.IO;
using Microsoft.Win32;
using PlasticBrainSim.Simulation;

namespace PlasticBrainSim;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private SimulationEngine _engine = new();
    private bool _running;

    public MainWindow()
    {
        InitializeComponent();
        _timer.Tick += Timer_Tick;
        ConnectEngine();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var steps = Math.Max(1, (int)SpeedSlider.Value);
        for (var i = 0; i < steps; i++) _engine.Step();
        RefreshDisplay();
    }

    private void StartPause_Click(object sender, RoutedEventArgs e)
    {
        _running = !_running;
        StartPauseButton.Content = _running ? "Pause" : "Start";
        if (_running) _timer.Start(); else _timer.Stop();
    }

    private void SingleStep_Click(object sender, RoutedEventArgs e)
    {
        if (_running)
        {
            _running = false;
            _timer.Stop();
            StartPauseButton.Content = "Start";
        }

        _engine.Step();
        RefreshDisplay();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _engine = new SimulationEngine();
        ConnectEngine();
    }

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SpeedText is not null) SpeedText.Text = $"{(int)e.NewValue}x";
    }

    private void AgentGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AgentGrid.SelectedItem is Agent selected)
        {
            WorldView.SelectedAgent = selected;
            NetworkView.SelectedAgent = selected;
            RefreshDisplay();
        }
    }

    private void ShowReadme_Click(object sender, RoutedEventArgs e)
    {
        new ReadmeWindow { Owner = this }.Show();
    }

    private void SaveAgent_Click(object sender, RoutedEventArgs e)
    {
        if (AgentGrid.SelectedItem is not Agent selected) return;
        try
        {
            var path = AgentStorage.Save(selected);
            MessageBox.Show(this, $"{selected.Name} wurde gespeichert:\n{path}",
                "Agent speichern", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Speichern fehlgeschlagen",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadAgent_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AgentStorage.GetStorageDirectory());
        var dialog = new OpenFileDialog
        {
            Title = "Agent laden",
            InitialDirectory = AgentStorage.GetStorageDirectory(),
            Filter = "Agentendateien (*.json)|*.json|Alle Dateien (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var agent = AgentStorage.Load(dialog.FileName, _engine.Values);
            _engine.Agents.Add(agent);
            AgentGrid.Items.Refresh();
            AgentGrid.SelectedItem = agent;
            RefreshDisplay();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Laden fehlgeschlagen",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ConnectEngine()
    {
        SpeedSlider.Value = Math.Clamp(
            _engine.Values.DefaultSimulationSpeed,
            (int)SpeedSlider.Minimum,
            (int)SpeedSlider.Maximum);
        WorldView.Engine = _engine;
        NetworkView.Engine = _engine;
        AgentGrid.ItemsSource = _engine.Agents;
        AgentGrid.SelectedItem = _engine.Agents[0];
        WorldView.SelectedAgent = _engine.Agents[0];
        NetworkView.SelectedAgent = _engine.Agents[0];
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        AgentGrid.Items.Refresh();
        WorldView.InvalidateVisual();
        NetworkView.InvalidateVisual();
        var selected = NetworkView.SelectedAgent;
        AgentStatusPanel.DataContext = null;
        AgentStatusPanel.DataContext = selected;
        if (selected is not null)
        {
            AgentPositionText.Text = $"X {selected.Body.Position.X:F3} / Y {selected.Body.Position.Y:F3}";
            AgentExperienceText.Text = $"{selected.FoodCollected} / {selected.HazardHits}";
        }
        StatusText.Text = selected is null
            ? $"Schritt {_engine.StepCount:N0}"
            : $"Schritt {_engine.StepCount,8:N0}   Ausgewaehlt: {selected.Name}   " +
              $"Belohnung {selected.LastReward,6:F3}   Bedeutungsnetz {selected.Brain.NeuronCount:N0} Neuronen";
    }
}
