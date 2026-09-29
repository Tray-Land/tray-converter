using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TinyConverter.Services;
using Windows.ApplicationModel.DataTransfer;

namespace TinyConverter.Views;

/// <summary>
/// The converter: quantity, value, from/to units, and a favorites list that jumps straight to a
/// saved conversion. Everything is local, so there is nothing to poll while hidden.
/// </summary>
public sealed partial class FlyoutPage : Page, IDisposable
{
    private readonly List<Favorite> _favorites = FavoritesService.Load();
    private bool _loading;

    public FlyoutPage()
    {
        InitializeComponent();
        TitleText.Text = App.DisplayName;

        _loading = true;
        QuantityBox.ItemsSource = ConversionService.Quantities;
        Select(SettingsService.LastQuantity ?? "Length", SettingsService.LastFrom, SettingsService.LastTo);
        _loading = false;

        RefreshFavorites();
        Recalculate();
    }

    /// <summary>The flyout opened: ready the input for typing.</summary>
    public void OnShown()
    {
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

    private void RefreshFavorites()
    {
        FavoritesList.ItemsSource = _favorites.Select(FavoriteLabel).ToList();
        FavoritesPanel.Visibility = _favorites.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string FavoriteLabel(Favorite favorite)
    {
        QuantityOption? quantity = ConversionService.FindQuantity(favorite.Quantity);
        if (quantity is null)
        {
            return $"{favorite.Quantity}: {favorite.From} → {favorite.To}";
        }

        string from = ConversionService.FindUnit(quantity, favorite.From)?.Display ?? favorite.From;
        string to = ConversionService.FindUnit(quantity, favorite.To)?.Display ?? favorite.To;
        return $"{quantity.Display}: {from} → {to}";
    }

    private void SaveFavorites()
    {
        FavoritesService.Save(_favorites);
        RefreshFavorites();
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

        if (!_favorites.Remove(current))
        {
            _favorites.Add(current);
        }

        SaveFavorites();
    }

    private void FavoritesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        int index = FavoritesList.Items.IndexOf(e.ClickedItem);
        if (index < 0 || index >= _favorites.Count)
        {
            return;
        }

        Favorite favorite = _favorites[index];
        _loading = true;
        Select(favorite.Quantity, favorite.From, favorite.To);
        _loading = false;
        Recalculate();
        InputBox.Focus(FocusState.Programmatic);
        InputBox.SelectAll();
    }

    private void RemoveFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string label })
        {
            int index = FavoritesList.Items.IndexOf(label);
            if (index >= 0 && index < _favorites.Count)
            {
                _favorites.RemoveAt(index);
                SaveFavorites();
            }
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => App.Current.ShowSettings();
}
