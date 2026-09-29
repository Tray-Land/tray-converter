using System.Text.Json.Serialization;

namespace TinyConverter.Services;

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
}
