using Microsoft.UI.Xaml;
using SandboxLauncher.Services;

namespace SandboxLauncher;

public partial class App : Application
{
    private Window? _window;

    public App() => InitializeComponent();

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var commandLine = Environment.GetCommandLineArgs();
        var launchIndex = Array.FindIndex(commandLine, x => string.Equals(x, "--launch", StringComparison.OrdinalIgnoreCase));

        if (launchIndex >= 0 && launchIndex + 1 < commandLine.Length)
        {
            try
            {
                await new SandboxLauncherService().LaunchAsync(commandLine[launchIndex + 1], SandboxProfile.Standard);
            }
            catch (Exception ex)
            {
                var dialog = new Windows.UI.Popups.MessageDialog(ex.Message, "Sandbox Launcher");
                await dialog.ShowAsync();
            }
            Exit();
            return;
        }

        _window = new MainWindow();
        _window.Activate();
    }
}
