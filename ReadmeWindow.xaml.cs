using System.IO;
using System.Windows;

namespace PlasticBrainSim;

public partial class ReadmeWindow : Window
{
    public ReadmeWindow()
    {
        InitializeComponent();
        var path = Path.Combine(AppContext.BaseDirectory, "README.md");
        ReadmeText.Text = File.Exists(path)
            ? File.ReadAllText(path)
            : "README.md wurde im Ausgabeverzeichnis nicht gefunden.";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}