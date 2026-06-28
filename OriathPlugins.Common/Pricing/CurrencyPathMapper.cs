namespace OriathPlugins.Common.Pricing;

using System.Collections.Frozen;

public static class CurrencyPathMapper
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
        "/Rings/",
        "/Amulets/",
        "/Belts/",
        "/Armours/",
        "/Weapons/",
        "/Gems/",
        "/Jewels/",
        "/Maps/",
        "/Flasks/",
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

    public static string? TryMapToNinjaId(string itemPath)
    {
        if (string.IsNullOrWhiteSpace(itemPath))
        {
            return null;
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

        return ToKebabCase(fileName);
    }

    public static IReadOnlyList<(string Path, string Id)> GetCurrencyOptionEntries()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<(string Path, string Id)>();
        foreach (var (suffix, id) in PathSuffixToNinjaId)
        {
            if (!seen.Add(id))
            {
                continue;
            }

            results.Add((GuessCurrencyPath(suffix), id));
        }

        return results;
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

    public static bool IsItemLootPath(string itemPath)
    {
        if (string.IsNullOrWhiteSpace(itemPath))
        {
            return false;
        }

        if (!itemPath.Contains("Metadata/Items/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
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

    public static bool IsTrackableCurrencyPath(string itemPath)
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

        return itemPath.Contains("/Currency/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/StackableCurrency/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Fragments/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Runes/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Rune/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Essences/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Essence/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/SoulCore/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/SoulCores/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Omen/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Omens/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Expedition2/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Verisium/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Abyss/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Splinter", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Catalysts/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Scarab/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Breach/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Incursion/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Delirium/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Ritual/", StringComparison.OrdinalIgnoreCase) ||
               itemPath.Contains("/Expedition/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsGroundLootPath(string itemPath) =>
        IsItemLootPath(itemPath) ||
        (!string.IsNullOrWhiteSpace(itemPath) && PathSuffixToNinjaId.ContainsKey(GetFileName(itemPath)));

    /// <summary>
    ///     True for currency tabs and gear/uniques that may appear in the main inventory after pickup.
    /// </summary>
    public static bool IsInventoryLootPath(string itemPath) => IsItemLootPath(itemPath);

    public static bool IsCurrencyPath(string itemPath) => IsItemLootPath(itemPath);

    private static string GuessCurrencyPath(string suffix) =>
        suffix switch
        {
            "CurrencyModValues" => "Metadata/Items/Currency/CurrencyModValues/DivineOrb",
            "CurrencyRerollRare" => "Metadata/Items/Currency/CurrencyRerollRare/ChaosOrb",
            "CurrencyAddModToRare" => "Metadata/Items/Currency/CurrencyAddModToRare/ExaltedOrb",
            "CurrencyIdentification" => "Metadata/Items/Currency/CurrencyIdentification/ScrollOfWisdom",
            _ => $"Metadata/Items/Currency/{suffix}",
        };

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
