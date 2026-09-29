using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrayConverter.Services;
using Windows.ApplicationModel;

namespace TrayConverter.Views;

/// <summary>Settings shown inside the flyout in place of the converter.</summary>
public sealed partial class SettingsPage : Page
{
    private readonly Action _goBack;
    private List<Favorite> _favorites = [];
    private bool _loading = true;

    public SettingsPage(Action goBack)
    {
        _goBack = goBack;
        InitializeComponent();
        VersionText.Text = $"{App.DisplayName} {GetVersion()}";
        OnShown();
    }

    /// <summary>Re-reads startup state; the user may have changed it in Windows Settings.</summary>
    public void OnShown()
    {
        _ = LoadStartupStateAsync();
        LoadRounding();
        RefreshFavorites();
        BackButton.Focus(FocusState.Programmatic);
    }

    private void LoadRounding()
    {
        _loading = true;
        RoundingBox.SelectedIndex = (int)SettingsService.Rounding;
        DigitsBox.Value = SettingsService.RoundingDigits;
        CopySeparatorsToggle.IsOn = SettingsService.CopyThousandsSeparators;
        ShowRoundingState();
        _loading = false;
    }

    private void CopySeparatorsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loading)
        {
            SettingsService.CopyThousandsSeparators = CopySeparatorsToggle.IsOn;
        }
    }

    private void ShowRoundingState()
    {
        RoundingMode mode = SettingsService.Rounding;
        DigitsBox.Visibility = mode == RoundingMode.Automatic ? Visibility.Collapsed : Visibility.Visible;
        DigitsBox.Header = mode == RoundingMode.SignificantFigures ? "Significant figures" : "Decimal places";
        RoundingDescription.Text = mode switch
        {
            RoundingMode.DecimalPlaces => "Always show the same number of digits after the decimal point.",
            RoundingMode.SignificantFigures => "Round to a set number of significant figures and keep trailing zeros.",
            _ => "Shows up to 6 significant figures and trims trailing zeros.",
        };
    }

    private void RoundingBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || RoundingBox.SelectedIndex < 0)
        {
            return;
        }

        SettingsService.Rounding = (RoundingMode)RoundingBox.SelectedIndex;
        ShowRoundingState();
    }

    private void DigitsBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading)
        {
            return;
        }

        if (double.IsNaN(args.NewValue))
        {
            sender.Value = SettingsService.RoundingDigits;
            return;
        }

        SettingsService.RoundingDigits = (int)args.NewValue;
    }

    private void RefreshFavorites()
    {
        _favorites = FavoritesService.Load();
        FavoritesList.ItemsSource = _favorites.Select(FavoritesService.Label).ToList();
        NoFavoritesText.Visibility = _favorites.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FavoritesList.Visibility = _favorites.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void DeleteFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string label })
        {
            int index = FavoritesList.Items.IndexOf(label);
            if (index >= 0 && index < _favorites.Count)
            {
                _favorites.RemoveAt(index);
                FavoritesService.Save(_favorites);
                RefreshFavorites();
            }
        }
    }

    private static string GetVersion()
    {
        try
        {
            PackageVersion v = Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }
        catch
        {
            return "(unpackaged)";
        }
    }

    private async Task LoadStartupStateAsync()
    {
        ShowStartupState(await StartupService.GetStateAsync());
    }

    private void ShowStartupState(StartupTaskState? state)
    {
        _loading = true;
        StartupToggle.IsOn = state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;

        // The user (Task Manager, Settings > Apps > Startup) or policy has the final say; the app
        // can't override it, so say where to change it instead of offering a dead toggle.
        StartupToggle.IsEnabled = state is StartupTaskState.Enabled or StartupTaskState.Disabled;
        StartupDescription.Text = state switch
        {
            StartupTaskState.DisabledByUser => "Turned off in Settings > Apps > Startup. Turn it on there.",
            StartupTaskState.DisabledByPolicy or StartupTaskState.EnabledByPolicy => "Managed by your organization.",
            null => "Only available when the app is installed.",
            _ => "Keep the tray icon ready after you sign in.",
        };
        _loading = false;
    }

    private async void StartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        ShowStartupState(await StartupService.SetEnabledAsync(StartupToggle.IsOn));
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => _goBack();
}
