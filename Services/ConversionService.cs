using System.Globalization;
using System.Text;
using NCalc;
using UnitsNet;

namespace TinyConverter.Services;

/// <summary>A unit inside a quantity, e.g. Length / Meter.</summary>
internal sealed record UnitOption(string Name, string Display, Enum Value)
{
    public override string ToString() => Display;
}

/// <summary>A physical quantity (Length, Mass, ...) and its units.</summary>
internal sealed record QuantityOption(string Name, string Display, IReadOnlyList<UnitOption> Units, string BaseUnit)
{
    public override string ToString() => Display;
}

/// <summary>
/// Unit catalog and conversion on top of UnitsNet, plus input evaluation through NCalc. No WinRT
/// dependency, so it can be covered by a plain test project.
/// </summary>
internal static class ConversionService
{
    private static IReadOnlyList<QuantityOption>? _quantities;

    /// <summary>Built on first use (UnitsNet has ~100 quantities); dropped by <see cref="Release"/>.</summary>
    public static IReadOnlyList<QuantityOption> Quantities => _quantities ??= BuildCatalog();

    public static void Release() => _quantities = null;

    public static QuantityOption? FindQuantity(string? name) =>
        Quantities.FirstOrDefault(q => q.Name == name);

    /// <summary>Everyday defaults for the common quantities; others start at the base unit.</summary>
    private static readonly Dictionary<string, (string From, string To)> Defaults = new()
    {
        ["Length"] = ("Meter", "Foot"),
        ["Mass"] = ("Kilogram", "Pound"),
        ["Temperature"] = ("DegreeCelsius", "DegreeFahrenheit"),
        ["Volume"] = ("Liter", "UsGallon"),
        ["Speed"] = ("KilometerPerHour", "MilePerHour"),
        ["Area"] = ("SquareMeter", "SquareFoot"),
        ["Duration"] = ("Hour", "Minute"),
        ["Energy"] = ("Kilocalorie", "Kilojoule"),
        ["Pressure"] = ("Bar", "PoundForcePerSquareInch"),
        ["Information"] = ("Gigabyte", "Gibibyte"),
    };

    public static (UnitOption From, UnitOption To) DefaultUnits(QuantityOption quantity)
    {
        UnitOption? from = null, to = null;
        if (Defaults.TryGetValue(quantity.Name, out var names))
        {
            from = FindUnit(quantity, names.From);
            to = FindUnit(quantity, names.To);
        }

        from ??= FindUnit(quantity, quantity.BaseUnit) ?? quantity.Units[0];
        to ??= quantity.Units.First(u => u != from);
        return (from, to);
    }

    public static UnitOption? FindUnit(QuantityOption quantity, string? name) =>
        quantity.Units.FirstOrDefault(u => u.Name == name);

    /// <summary>
    /// Parses a plain number in the current culture, otherwise evaluates it as an NCalc expression
    /// ("5 * 12", "sqrt(2)"). Returns null when the text is empty, invalid, or not finite.
    /// </summary>
    public static double? ParseInput(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out double plain))
        {
            return double.IsFinite(plain) ? plain : null;
        }

        try
        {
            object? result = new Expression(text.Trim()).Evaluate();
            double value = Convert.ToDouble(result, CultureInfo.InvariantCulture);
            return double.IsFinite(value) ? value : null;
        }
        catch
        {
            return null;
        }
    }

    public static double? TryConvert(double value, UnitOption from, UnitOption to)
    {
        try
        {
            double result = (double)Quantity.From(value, from.Value).ToUnit(to.Value).Value;
            return double.IsFinite(result) ? result : null;
        }
        catch
        {
            return null;
        }
    }

    public static string Format(double value)
    {
        double abs = Math.Abs(value);
        return abs != 0 && (abs >= 1e12 || abs < 1e-6)
            ? value.ToString("G8", CultureInfo.CurrentCulture)
            : value.ToString("#,0.##########", CultureInfo.CurrentCulture);
    }

    private static List<QuantityOption> BuildCatalog()
    {
        var list = new List<QuantityOption>();
        foreach (QuantityInfo info in Quantity.Infos)
        {
            var units = info.UnitInfos
                .Select(u => new UnitOption(u.Name, DisplayUnit(u.Name, u.Value), u.Value))
                .OrderBy(u => u.Display, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            if (units.Count > 1)
            {
                list.Add(new QuantityOption(info.Name, Humanize(info.Name), units, info.BaseUnitInfo.Name));
            }
        }

        return [.. list.OrderBy(q => q.Display, StringComparer.CurrentCultureIgnoreCase)];
    }

    private static string DisplayUnit(string name, Enum value)
    {
        string abbreviation = string.Empty;
        try
        {
            abbreviation = UnitAbbreviationsCache.Default.GetDefaultAbbreviation(
                value.GetType(), Convert.ToInt32(value, CultureInfo.InvariantCulture));
        }
        catch
        {
            // Some units have no abbreviation; fall back to the name alone.
        }

        return string.IsNullOrEmpty(abbreviation) ? Humanize(name) : $"{Humanize(name)} ({abbreviation})";
    }

    /// <summary>"ElectricPotentialDc" becomes "Electric Potential Dc".</summary>
    internal static string Humanize(string pascal)
    {
        var sb = new StringBuilder(pascal.Length + 4);
        for (int i = 0; i < pascal.Length; i++)
        {
            if (i > 0 && char.IsUpper(pascal[i]) && !char.IsUpper(pascal[i - 1]))
            {
                sb.Append(' ');
            }

            sb.Append(pascal[i]);
        }

        return sb.ToString();
    }
}
