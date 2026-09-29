using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Windows.Storage;

namespace TrayConverter.Services;

/// <summary>
/// Typed wrapper over <see cref="ApplicationData.LocalSettings"/>. Every read falls back to a
/// default so a missing or corrupt value never crashes the app. Not for secrets: see
/// <see cref="Secrets"/> (build-time app keys) and <see cref="CredentialStore"/> (user tokens).
/// </summary>
/// <remarks>
/// LocalSettings values must be small (8 KB each) WinRT primitives. Store enums as int, and lists
/// or records as JSON through a source-generated <c>JsonSerializerContext</c> (trim-safe) with
/// <see cref="GetJson"/> / <see cref="SetJson"/>. Anything larger belongs in a file under
/// <c>ApplicationData.Current.LocalFolder</c>.
/// </remarks>
internal static class SettingsService
{
    /// <summary>False until the first launch has shown the flyout.</summary>
    public static bool HasLaunchedBefore
    {
        get => Get(nameof(HasLaunchedBefore), false);
        set => Set(nameof(HasLaunchedBefore), value);
    }

    public static RoundingMode Rounding
    {
        get => Enum.IsDefined((RoundingMode)Get(nameof(Rounding), 0)) ? (RoundingMode)Get(nameof(Rounding), 0) : RoundingMode.Automatic;
        set => Set(nameof(Rounding), (int)value);
    }

    /// <summary>Decimal places or significant figures, depending on <see cref="Rounding"/>.</summary>
    public static int RoundingDigits
    {
        get => Math.Clamp(Get(nameof(RoundingDigits), 4), 1, 15);
        set => Set(nameof(RoundingDigits), Math.Clamp(value, 1, 15));
    }

    /// <summary>Whether copied results keep thousands separators ("1,234.5" vs "1234.5").</summary>
    public static bool CopyThousandsSeparators
    {
        get => Get(nameof(CopyThousandsSeparators), false);
        set => Set(nameof(CopyThousandsSeparators), value);
    }

    public static string? LastQuantity
    {
        get => Get<string?>(nameof(LastQuantity), null);
        set => Set(nameof(LastQuantity), value);
    }

    public static string? LastFrom
    {
        get => Get<string?>(nameof(LastFrom), null);
        set => Set(nameof(LastFrom), value);
    }

    public static string? LastTo
    {
        get => Get<string?>(nameof(LastTo), null);
        set => Set(nameof(LastTo), value);
    }

    public static T? GetJson<T>(string key, JsonTypeInfo<T> typeInfo)
    {
        if (Get<string?>(key, null) is not { } json)
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize(json, typeInfo);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public static void SetJson<T>(string key, T? value, JsonTypeInfo<T> typeInfo) =>
        Set(key, value is null ? null : JsonSerializer.Serialize(value, typeInfo));

    private static T Get<T>(string key, T defaultValue)
    {
        try
        {
            return ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out object? value) && value is T typed
                ? typed
                : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private static void Set(string key, object? value)
    {
        try
        {
            if (value is null)
            {
                ApplicationData.Current.LocalSettings.Values.Remove(key);
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values[key] = value;
            }
        }
        catch
        {
            // Settings are best-effort; ignore storage failures.
        }
    }
}
