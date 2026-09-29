using System.Text.Json.Serialization;

namespace TrayConverter.Services;

/// <summary>A saved conversion. Units are stored by UnitsNet enum name so the list survives upgrades.</summary>
internal sealed record Favorite(string Quantity, string From, string To);

[JsonSerializable(typeof(List<Favorite>))]
internal sealed partial class FavoritesJsonContext : JsonSerializerContext;

/// <summary>Favorites, persisted as JSON in LocalSettings (a handful of small records).</summary>
internal static class FavoritesService
{
    private const string Key = "Favorites";

    public static List<Favorite> Load() =>
        SettingsService.GetJson(Key, FavoritesJsonContext.Default.ListFavorite) ?? [];

    public static void Save(List<Favorite> favorites) =>
        SettingsService.SetJson(Key, favorites, FavoritesJsonContext.Default.ListFavorite);

    /// <summary>"Length: Meter (m) → Foot (ft)"</summary>
    public static string Label(Favorite favorite)
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
}
