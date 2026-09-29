using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using TinyConverter.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI.ViewManagement;

namespace TinyConverter.Views;

/// <summary>
/// The converter: quantity, value, from/to units, and a favorites dropdown that jumps straight to a
/// saved conversion. Everything is local, so there is nothing to poll while hidden.
/// </summary>
public sealed partial class FlyoutPage : Page, IDisposable
{
    private List<Favorite> _favorites = FavoritesService.Load();
    private bool _loading;

    public FlyoutPage()
    {
        InitializeComponent();
        TitleText.Text = App.DisplayName;

        _loading = true;
        QuantityBox.ItemsSource = ConversionService.Quantities;
        Select(SettingsService.LastQuantity ?? "Length", SettingsService.LastFrom, SettingsService.LastTo);
        _loading = false;

        Recalculate();
    }

    /// <summary>The flyout opened: ready the input for typing.</summary>
    public void OnShown()
    {
        // Favorites may have been deleted on the settings page.
        _favorites = FavoritesService.Load();
        UpdateFavoriteButton();
        InputBox.Focus(FocusState.Programmatic);
        InputBox.SelectAll();
    }

    public void OnHidden()
    {
    }

    public void Dispose() => ConversionService.Release();

    private QuantityOption? SelectedQuantity => QuantityBox.SelectedItem as QuantityOption;
    private UnitOption? From => FromBox.SelectedItem as UnitOption;
    private UnitOption? To => ToBox.SelectedItem as UnitOption;

    private Favorite? Current => SelectedQuantity is { } q && From is { } f && To is { } t
        ? new Favorite(q.Name, f.Name, t.Name)
        : null;

    /// <summary>Selects a quantity and units, falling back to everyday defaults.</summary>
    private void Select(string quantityName, string? fromName, string? toName)
    {
        QuantityOption quantity = ConversionService.FindQuantity(quantityName) ?? ConversionService.Quantities[0];
        QuantityBox.SelectedItem = quantity;
        FromBox.ItemsSource = quantity.Units;
        ToBox.ItemsSource = quantity.Units;
        var defaults = ConversionService.DefaultUnits(quantity);
        FromBox.SelectedItem = ConversionService.FindUnit(quantity, fromName) ?? defaults.From;
        ToBox.SelectedItem = ConversionService.FindUnit(quantity, toName) ?? defaults.To;
    }

    private void Recalculate()
    {
        if (SelectedQuantity is not { } quantity || From is not { } from || To is not { } to)
        {
            return;
        }

        ResultText.Text = ConversionService.ParseInput(InputBox.Text) is { } value
            && ConversionService.TryConvert(value, from, to) is { } result
                ? ConversionService.Format(result)
                : "--";

        if (!_loading)
        {
            SettingsService.LastQuantity = quantity.Name;
            SettingsService.LastFrom = from.Name;
            SettingsService.LastTo = to.Name;
        }

        UpdateFavoriteButton();
    }

    private void UpdateFavoriteButton()
    {
        bool isFavorite = Current is { } current && _favorites.Contains(current);
        FavoriteIcon.Glyph = isFavorite ? "" : "";
        ToolTipService.SetToolTip(FavoriteButton, isFavorite ? "Remove from favorites" : "Add to favorites");
    }

    private void SaveFavorites()
    {
        FavoritesService.Save(_favorites);
        UpdateFavoriteButton();
    }

    private void QuantityBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || SelectedQuantity is not { } quantity)
        {
            return;
        }

        _loading = true;
        Select(quantity.Name, null, null);
        _loading = false;
        Recalculate();
    }

    private void Unit_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading)
        {
            Recalculate();
        }
    }

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e) => Recalculate();

    private void SwapButton_Click(object sender, RoutedEventArgs e)
    {
        object? from = FromBox.SelectedItem;
        _loading = true;
        FromBox.SelectedItem = ToBox.SelectedItem;
        ToBox.SelectedItem = from;
        _loading = false;
        Recalculate();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (ResultText.Text == "--")
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(ResultText.Text);
        Clipboard.SetContent(package);
    }

    private void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (Current is not { } current)
        {
            return;
        }

        bool added = !_favorites.Remove(current);
        if (added)
        {
            _favorites.Add(current);
        }

        SaveFavorites();
        if (added)
        {
            PlayAddedAnimation();
        }
    }

    /// <summary>
    /// A star lifts off the result's star button and flies up to the favorites button, which then
    /// pulses, so it's clear that is where favorites live.
    /// </summary>
    private void PlayAddedAnimation()
    {
        if (!new UISettings().AnimationsEnabled || FlyStar.Parent is not UIElement layer)
        {
            return;
        }

        Vector3 start = CenterIn(FavoriteButton, layer);
        Vector3 end = CenterIn(FavoritesMenuButton, layer);
        Vector3 half = new((float)FlyStar.ActualWidth / 2, (float)FlyStar.ActualHeight / 2, 0);

        Visual star = ElementCompositionPreview.GetElementVisual(FlyStar);
        Compositor compositor = star.Compositor;
        star.CenterPoint = half;

        CubicBezierEasingFunction ease = compositor.CreateCubicBezierEasingFunction(new(0.3f, 0f), new(0.2f, 1f));
        TimeSpan duration = TimeSpan.FromMilliseconds(600);

        // An arc: Y travels up steadily while X swings out to the left and back, so the star
        // follows a bowed path instead of a straight line.
        const float bow = 90f;
        ScalarKeyFrameAnimation offsetX = compositor.CreateScalarKeyFrameAnimation();
        offsetX.Duration = duration;
        offsetX.InsertKeyFrame(0f, start.X - half.X);
        offsetX.InsertKeyFrame(0.5f, ((start.X + end.X) / 2) - bow - half.X,
            compositor.CreateCubicBezierEasingFunction(new(0.2f, 0.6f), new(0.4f, 1f)));
        offsetX.InsertKeyFrame(1f, end.X - half.X,
            compositor.CreateCubicBezierEasingFunction(new(0.5f, 0f), new(0.8f, 0.4f)));

        ScalarKeyFrameAnimation offsetY = compositor.CreateScalarKeyFrameAnimation();
        offsetY.Duration = duration;
        offsetY.InsertKeyFrame(0f, start.Y - half.Y);
        offsetY.InsertKeyFrame(1f, end.Y - half.Y, compositor.CreateCubicBezierEasingFunction(new(0.4f, 0f), new(0.3f, 1f)));

        Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
        scale.Duration = duration;
        scale.InsertKeyFrame(0f, new Vector3(0.6f));
        scale.InsertKeyFrame(0.3f, new Vector3(1.7f), ease);
        scale.InsertKeyFrame(1f, new Vector3(0.9f), ease);

        ScalarKeyFrameAnimation opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.Duration = duration;
        opacity.InsertKeyFrame(0f, 0f);
        opacity.InsertKeyFrame(0.1f, 1f);
        opacity.InsertKeyFrame(0.85f, 1f);
        opacity.InsertKeyFrame(1f, 0f);

        CompositionScopedBatch batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        star.StartAnimation("Offset.X", offsetX);
        star.StartAnimation("Offset.Y", offsetY);
        star.StartAnimation("Scale", scale);
        star.StartAnimation("Opacity", opacity);
        batch.End();
        batch.Completed += (_, _) => DispatcherQueue.TryEnqueue(PulseFavoritesButton);
    }

    private void PulseFavoritesButton()
    {
        Visual icon = ElementCompositionPreview.GetElementVisual(FavoritesMenuIcon);
        Compositor compositor = icon.Compositor;
        icon.CenterPoint = new Vector3((float)FavoritesMenuIcon.ActualWidth / 2, (float)FavoritesMenuIcon.ActualHeight / 2, 0);

        Vector3KeyFrameAnimation pulse = compositor.CreateVector3KeyFrameAnimation();
        pulse.Duration = TimeSpan.FromMilliseconds(350);
        pulse.InsertKeyFrame(0f, Vector3.One);
        pulse.InsertKeyFrame(0.4f, new Vector3(1.5f), compositor.CreateCubicBezierEasingFunction(new(0.2f, 0.8f), new(0.2f, 1f)));
        pulse.InsertKeyFrame(1f, Vector3.One, compositor.CreateCubicBezierEasingFunction(new(0.3f, 0f), new(0.6f, 1f)));
        icon.StartAnimation("Scale", pulse);
    }

    private static Vector3 CenterIn(FrameworkElement element, UIElement layer)
    {
        Point p = element.TransformToVisual(layer).TransformPoint(new Point(element.ActualWidth / 2, element.ActualHeight / 2));
        return new Vector3((float)p.X, (float)p.Y, 0);
    }

    private void FavoritesMenu_Opening(object? sender, object e)
    {
        FavoritesMenu.Items.Clear();
        if (_favorites.Count == 0)
        {
            FavoritesMenu.Items.Add(new MenuFlyoutItem { Text = "No favorites yet", IsEnabled = false });
            return;
        }

        foreach (Favorite favorite in _favorites)
        {
            var item = new MenuFlyoutItem { Text = FavoritesService.Label(favorite), Tag = favorite };
            item.Click += FavoriteItem_Click;
            FavoritesMenu.Items.Add(item);
        }
    }

    private void FavoriteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: Favorite favorite })
        {
            return;
        }

        _loading = true;
        Select(favorite.Quantity, favorite.From, favorite.To);
        _loading = false;
        Recalculate();
        InputBox.Focus(FocusState.Programmatic);
        InputBox.SelectAll();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => App.Current.ShowSettings();
}
