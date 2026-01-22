using System.Reflection;
using Microsoft.UI.Xaml.Controls;

namespace CodexBar.Views;

public sealed partial class AboutSettingsPage : Page
{
    public AboutSettingsPage()
    {
        this.InitializeComponent();

        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        VersionText.Text = !string.IsNullOrEmpty(version)
            ? $"Version {version}"
            : "Version unknown";
    }
}
