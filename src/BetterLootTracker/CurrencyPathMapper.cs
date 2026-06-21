namespace BetterLootTracker;

using System.Collections.Frozen;

internal static class CurrencyPathMapper
{
    private static readonly string[] ItemLootMarkers =
    [
        "/Currency/",
        "/StackableCurrency/",
        "/Fragments/",
        "/Essences/",
        "/Essence/",
        "/Runes/",
        "/Rune/",
        "/Splinters/",
        "/Splinter/",
        "/Omen/",
        "/Omens/",
        "/Catalysts/",
        "/Scarab/",
        "/SoulCore/",
        "/SoulCores/",
        "/Tablet/",
        "/Tablets/",
        "/Waystone/",
        "/Waystones/",
        "/UncutGem/",
        "/UncutGems/",
        "/Incursion/",
        "/Breach/",
        "/Delirium/",
        "/Ritual/",
        "/Expedition/",
        "/Expedition2/",
        "/Verisium/",
        "/Abyss/",
    ];

    private static readonly FrozenDictionary<string, string> PathSuffixToNinjaId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["CurrencyModValues"] = "divine",
        ["CurrencyAddModToRare"] = "exalted",
        ["CurrencyAddModToRare2"] = "greater-exalted-orb",
        ["CurrencyAddModToRare3"] = "perfect-exalted-orb",
        ["CurrencyRerollRare"] = "chaos",
        ["CurrencyRerollRare2"] = "greater-chaos-orb",
        ["CurrencyRerollRare3"] = "perfect-chaos-orb",
        ["CurrencyVaal"] = "vaal",
        ["CurrencyUpgradeToMagic"] = "alch",
        ["CurrencyUpgradeToRare"] = "regal",
        ["CurrencyRerollMagic"] = "aug",
        ["CurrencyRerollSocket"] = "lesser-jewellers-orb",
        ["CurrencyRerollSocketNumbers01"] = "lesser-jewellers-orb",
        ["CurrencyRerollSocketNumbers02"] = "greater-jewellers-orb",
        ["CurrencyRerollSocketNumbers03"] = "perfect-jewellers-orb",
        ["CurrencyAddSkillGemSocket1"] = "lesser-jewellers-orb",
        ["CurrencyAddSkillGemSocket2"] = "greater-jewellers-orb",
        ["CurrencyAddSkillGemSocket3"] = "perfect-jewellers-orb",
        ["CurrencyAddSkillGemSocket5"] = "perfect-jewellers-orb",
        ["CurrencyRerollSocketColours"] = "chrom",
        ["CurrencyRerollSocketLinks"] = "fusing",
        ["CurrencyIdentification"] = "wisdom",
        ["CurrencyRemoveMod"] = "annul",
        ["CurrencyPortal"] = "portal",
        ["CurrencyArmourQuality"] = "armourers",
        ["CurrencyWeaponQuality"] = "whetstone",
        ["CurrencyFlaskQuality"] = "glassblowers",
        ["CurrencyGemQuality"] = "gcp",
        ["CurrencyCorrupt"] = "vaal",
        ["CurrencyAddModToMagic"] = "aug",
        ["CurrencyAddModToMagic2"] = "greater-orb-of-augmentation",
        ["CurrencyAddModToMagic3"] = "perfect-orb-of-augmentation",
        ["CurrencyVerisiumMetal1"] = "verisium",
        ["CurrencyVerisium"] = "verisium",
        ["RefinedVerisium"] = "verisium",
        ["PerfectVerisium"] = "exceptional-verisium",
        ["CurrencyBreachShard"] = "breach-splinter",
        ["BreachstoneSplinter"] = "breach-splinter",
        ["OmenOnAbyssAddPrefixes"] = "omen-of-sinistral-necromancy",
        ["AbyssalBenchTicketWeapon"] = "preserved-jawbone",
        ["AbyssalBenchTicketJewel"] = "preserved-cranium",
        ["PreservedJawbone"] = "preserved-jawbone",
        ["PreservedCranium"] = "preserved-cranium",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static string? TryMapToNinjaId(string itemPath, NinjaPriceCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(itemPath))
        {
            return null;
        }

        if (catalog.TryGetIdForPath(itemPath, out var mappedId))
        {
            return mappedId;
        }

        var fileName = GetFileName(itemPath);
        if (string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        if (PathSuffixToNinjaId.TryGetValue(fileName, out var suffixId))
        {
            return suffixId;
        }

        var kebab = ToKebabCase(fileName);
        if (catalog.HasId(kebab))
        {
            return kebab;
        }

        if (fileName.StartsWith("Currency", StringComparison.OrdinalIgnoreCase))
        {
            var trimmed = ToKebabCase(fileName["Currency".Length..]);
            if (catalog.HasId(trimmed))
            {
                return trimmed;
            }
        }

        if (fileName.StartsWith("Essence", StringComparison.OrdinalIgnoreCase))
        {
            var essenceId = "essence-of-" + ToKebabCase(fileName["Essence".Length..].TrimStart('O', 'f', ' '));
            if (catalog.HasId(essenceId))
            {
                return essenceId;
            }
        }

        if (fileName.Contains("Rune", StringComparison.OrdinalIgnoreCase))
        {
            var runeId = ToKebabCase(fileName) + (fileName.EndsWith("Rune", StringComparison.OrdinalIgnoreCase) ? string.Empty : "-rune");
            if (catalog.HasId(runeId))
            {
                return runeId;
            }
        }

        return kebab.Length > 0 && catalog.HasId(kebab) ? kebab : null;
    }

    public static string GetDisplayName(string itemPath, string? ninjaDisplayName)
    {
        if (!string.IsNullOrWhiteSpace(ninjaDisplayName))
        {
            return ninjaDisplayName;
        }

        var fileName = GetFileName(itemPath);
        if (fileName.StartsWith("Currency", StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName["Currency".Length..];
        }

        return HumanizeIdentifier(fileName);
    }

    public static bool IsItemLootPath(string itemPath, NinjaPriceCatalog? catalog = null)
    {
        if (string.IsNullOrWhiteSpace(itemPath))
        {
            return false;
        }

        if (!itemPath.Contains("Metadata/Items/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (catalog is not null && catalog.TryGetIdForPath(itemPath, out _))
        {
            return true;
        }

        foreach (var marker in ItemLootMarkers)
        {
            if (itemPath.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return itemPath.Contains("/Currency", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTrackableCurrencyPath(string itemPath, NinjaPriceCatalog? catalog = null)
    {
        if (string.IsNullOrWhiteSpace(itemPath) ||
            !itemPath.Contains("Metadata/Items/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = GetFileName(itemPath);
        if (PathSuffixToNinjaId.ContainsKey(fileName))
        {
            return true;
        }

        if (catalog is not null && catalog.TryGetIdForPath(itemPath, out _))
        {
            return true;
        }

        return itemPath.Contains("/Currency/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/StackableCurrency/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Fragments/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Runes/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Essences/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/SoulCore/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Omen/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Expedition2/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Verisium/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Abyss/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsGroundLootPath(string itemPath, NinjaPriceCatalog? catalog = null)
    {
        if (IsItemLootPath(itemPath, catalog))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(itemPath) || catalog is null)
        {
            return false;
        }

        var fileName = GetFileName(itemPath);
        return catalog.TryGetIdForPath(itemPath, out _) ||
               PathSuffixToNinjaId.ContainsKey(fileName);
    }

    public static bool IsCurrencyPath(string itemPath, NinjaPriceCatalog? catalog = null) =>
        IsItemLootPath(itemPath, catalog);

    private static string GetFileName(string itemPath) =>
        itemPath.Replace('\\', '/').Split('/').LastOrDefault() ?? itemPath;

    private static string ToKebabCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = new List<char>(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c is '_' or ' ' or '-')
            {
                if (chars.Count > 0 && chars[^1] != '-')
                {
                    chars.Add('-');
                }

                continue;
            }

            if (char.IsUpper(c) && i > 0 && !char.IsUpper(value[i - 1]) && chars.Count > 0 && chars[^1] != '-')
            {
                chars.Add('-');
            }

            chars.Add(char.ToLowerInvariant(c));
        }

        return new string(chars.ToArray()).Trim('-');
    }

    private static string HumanizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        var chars = new List<char>(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c is '_' or '-')
            {
                chars.Add(' ');
                continue;
            }

            if (i > 0 && char.IsUpper(c) && !char.IsUpper(value[i - 1]))
            {
                chars.Add(' ');
            }

            chars.Add(i == 0 ? char.ToUpperInvariant(c) : c);
        }

        return new string(chars.ToArray());
    }
}
