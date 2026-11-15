using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.Text;
using Windows.Storage.Pickers;
using Windows.ApplicationModel.DataTransfer;
using WinRT.Interop;
using SandboxLauncher.Services;
using System.IO;

namespace SandboxLauncher;

public sealed partial class MainWindow : Window
{
    private string? _selectedFile;
    private string? _lastOutputPath;
    private readonly SandboxLauncherService _launcher = new();
    private readonly SessionHistoryService _historyService = new();
    private DispatcherTimer? _notificationTimer;

    public MainWindow()
    {
        InitializeComponent();
        ProfilePicker_SelectionChanged(ProfilePicker, null!);
        LoadHistory();
    }

    // ── File selection ───────────────────────────────────────────────────

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".exe");
        picker.FileTypeFilter.Add(".msi");
        picker.FileTypeFilter.Add(".bat");
        picker.FileTypeFilter.Add(".cmd");
        picker.FileTypeFilter.Add(".ps1");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var file = await picker.PickSingleFileAsync();
        if (file is not null)
            SetSelectedFile(file.Path);
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        var items = await e.DataView.GetStorageItemsAsync();
        var file = items.OfType<Windows.Storage.StorageFile>().FirstOrDefault();
        if (file is not null)
            SetSelectedFile(file.Path);
    }

    private void SetSelectedFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var supported = new[] { ".exe", ".msi", ".bat", ".cmd", ".ps1" };
        if (!supported.Contains(extension))
        {
            StatusText.Text = "Unsupported file type.";
            return;
        }

        _selectedFile = path;
        SelectedFileText.Text = path;
        LaunchButton.IsEnabled = true;
        OpenOutputButton.Visibility = Visibility.Collapsed;
        StatusText.Text = "File ready to launch.";
    }

    // ── Launch ───────────────────────────────────────────────────────────

    private async void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFile is null) return;

        var profile = GetSelectedProfile();
        var startupCommand = StartupCommandBox.Text?.Trim();

        // ── Confirmation dialog ──────────────────────────────────────────
        var summary = $"File: {Path.GetFileName(_selectedFile)}\n" +
                      $"Profile: {profile.Name}\n" +
                      $"Networking: {(profile.Networking ? "Enabled" : "Disabled")}\n" +
                      $"Virtual GPU: {(profile.VGpu ? "Enabled" : "Disabled")}\n" +
                      $"Memory: {(profile.MemoryInMB.HasValue ? $"{profile.MemoryInMB} MB" : "Auto")}\n" +
                      $"Audio input: {(profile.AudioInput ? "Enabled" : "Disabled")}";

        if (!string.IsNullOrEmpty(startupCommand))
            summary += $"\nStartup command: {startupCommand}";

        var confirm = new ContentDialog
        {
            Title = "Confirm Launch",
            Content = new TextBlock { Text = summary, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = "Launch",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            return;

        // ── Execute ──────────────────────────────────────────────────────
        LaunchButton.IsEnabled = false;
        StatusText.Text = "Preparing Windows Sandbox…";

        try
        {
            var outputPath = await _launcher.LaunchAsync(_selectedFile, profile, startupCommand);
            _lastOutputPath = outputPath;
            StatusText.Text = $"Sandbox started. Output → {outputPath}";
            OpenOutputButton.Visibility = Visibility.Visible;

            // Record in history
            _historyService.Add(new SessionEntry(
                _selectedFile, profile.Name, DateTime.Now, outputPath));
            LoadHistory();

            // In-app notification
            ShowNotification("Sandbox Started",
                $"{Path.GetFileName(_selectedFile)} launched with the {profile.Name} profile.");
        }
        catch (Exception ex)
        {
            StatusText.Text = "Launch failed.";
            await new ContentDialog
            {
                Title = "Could not launch Windows Sandbox",
                Content = ex.Message,
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            }.ShowAsync();
        }
        finally
        {
            LaunchButton.IsEnabled = _selectedFile is not null;
        }
    }

    // ── Output folder ────────────────────────────────────────────────────

    private void OpenOutputButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lastOutputPath is null || !Directory.Exists(_lastOutputPath)) return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = _lastOutputPath,
            UseShellExecute = true
        });
    }

    // ── Profile helpers ──────────────────────────────────────────────────

    private SandboxProfile GetSelectedProfile()
    {
        var tag = (ProfilePicker.SelectedItem as RadioButton)?.Tag?.ToString();
        var baseProfile = tag switch
        {
            "Strict"  => SandboxProfile.Strict,
            "Network" => SandboxProfile.Network,
            _         => SandboxProfile.Standard
        };

        if (MemoryBox is null || AudioToggle is null)
            return baseProfile;

        int? memory = double.IsNaN(MemoryBox.Value) ? null : (int)MemoryBox.Value;
        return baseProfile.WithMemory(memory).WithAudio(AudioToggle.IsOn);
    }

    private void ProfilePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NetworkDetail is null) return;          // UI not yet loaded
        var p = GetSelectedProfile();
        NetworkDetail.Text = $"Networking: {(p.Networking ? "Enabled" : "Disabled")}";
        GpuDetail.Text     = $"Virtual GPU: {(p.VGpu ? "Enabled" : "Disabled")}";
    }

    // ── Session history ──────────────────────────────────────────────────

    private void LoadHistory()
    {
        _historyService.Load();
        HistoryList.Items.Clear();

        if (_historyService.Entries.Count == 0)
        {
            EmptyHistoryText.Visibility = Visibility.Visible;
            ClearHistoryButton.Visibility = Visibility.Collapsed;
            return;
        }

        EmptyHistoryText.Visibility = Visibility.Collapsed;
        ClearHistoryButton.Visibility = Visibility.Visible;

        foreach (var entry in _historyService.Entries)
        {
            var panel = new StackPanel
            {
                Spacing = 2,
                Padding = new Thickness(4, 8, 4, 8),
                Tag = entry.OutputFolder
            };

            panel.Children.Add(new TextBlock
            {
                Text = entry.FileName,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14
            });
            panel.Children.Add(new TextBlock
            {
                Text = entry.Summary,
                FontSize = 12,
                Opacity = 0.6
            });
            panel.Children.Add(new TextBlock
            {
                Text = $"\uE838  {entry.OutputFolder}",
                FontSize = 11,
                Opacity = 0.5,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            HistoryList.Items.Add(panel);
        }
    }

    private void HistoryList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is StackPanel { Tag: string folder } && Directory.Exists(folder))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
    }

    private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        _historyService.Clear();
        LoadHistory();
    }

    // ── Notification ─────────────────────────────────────────────────────

    private void ShowNotification(string title, string message)
    {
        NotificationBar.Title   = title;
        NotificationBar.Message = message;
        NotificationBar.IsOpen  = true;

        _notificationTimer?.Stop();
        _notificationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _notificationTimer.Tick += (_, _) =>
        {
            NotificationBar.IsOpen = false;
            _notificationTimer.Stop();
        };
        _notificationTimer.Start();
    }
}
