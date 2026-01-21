using System.Reflection;
using Microsoft.UI.Xaml.Controls;

namespace CodexBar.Views;

public sealed partial class AboutSettingsPage : Page
{
    public AboutSettingsPage()
    {
        this.InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version != null
            ? $"Version {version.Major}.{version.Minor}.{version.Build}"
            : "Version unknown";
    }
}
