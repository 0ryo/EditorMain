using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class CatalogCardState
{
    public string typeId;
    public string displayLabel;
    public string displayDescription;
    public GameObject root;
}

public sealed class CatalogCardCollection : IEnumerable<CatalogCardState>
{
    readonly List<CatalogCardState> items = new();
    readonly HashSet<string> removedTypeIds = new(StringComparer.OrdinalIgnoreCase);

    public void Add(CatalogCardState card)
    {
        items.Add(card);
    }

    public void Clear()
    {
        items.Clear();
    }

    public bool IsRemoved(string typeId)
    {
        return removedTypeIds.Contains(typeId);
    }

    public IReadOnlyList<CatalogCardState> Remove(string typeId)
    {
        removedTypeIds.Add(typeId);
        var removed = new List<CatalogCardState>();

        for (int i = items.Count - 1; i >= 0; i--)
        {
            var card = items[i];
            if (card == null || string.IsNullOrWhiteSpace(card.typeId))
            {
                items.RemoveAt(i);
                continue;
            }

            if (!string.Equals(card.typeId, typeId, StringComparison.OrdinalIgnoreCase)) continue;
            removed.Add(card);
            items.RemoveAt(i);
        }

        return removed;
    }

    public bool TryGetTypeInfo(string typeId, out string label, out string description)
    {
        if (!string.IsNullOrEmpty(typeId))
        {
            foreach (var card in items)
            {
                if (string.Equals(card.typeId, typeId, StringComparison.OrdinalIgnoreCase))
                {
                    label = card.displayLabel;
                    description = card.displayDescription ?? string.Empty;
                    return true;
                }
            }
        }

        label = typeId ?? string.Empty;
        description = string.Empty;
        return false;
    }

    public static bool MatchesQuery(CatalogCardState card, string normalizedQuery)
    {
        if (normalizedQuery.Length == 0) return true;

        bool matchesType = card.typeId.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) >= 0;
        bool matchesLabel = !string.IsNullOrWhiteSpace(card.displayLabel) &&
                            card.displayLabel.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) >= 0;
        bool matchesDescription = !string.IsNullOrWhiteSpace(card.displayDescription) &&
                                  card.displayDescription.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) >= 0;
        return matchesType || matchesLabel || matchesDescription;
    }

    public IEnumerator<CatalogCardState> GetEnumerator()
    {
        return items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

public static class CatalogCardText
{
    public static string BuildDisplayName(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId)) return string.Empty;

        if (typeId.Contains("Vehicle/Car", StringComparison.OrdinalIgnoreCase)) return "\u8ECA\u4E21";
        if (typeId.Contains("ToolBox", StringComparison.OrdinalIgnoreCase)) return "\u5DE5\u5177\u7BB1";
        if (typeId.Contains("Tire/Replacement", StringComparison.OrdinalIgnoreCase)) return "\u30BF\u30A4\u30E4\u4EA4\u63DB";
        if (typeId.Contains("Env/Wall", StringComparison.OrdinalIgnoreCase)) return "\u58C1";

        string tail = typeId;
        int slash = tail.LastIndexOf('/');
        if (slash >= 0 && slash < tail.Length - 1) tail = tail.Substring(slash + 1);
        tail = tail.Replace("_Proxy", string.Empty).Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(tail) ? typeId : tail;
    }

    public static string BuildCategoryLabel(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId)) return "\u305D\u306E\u4ED6";
        if (typeId.Contains("Vehicle", StringComparison.OrdinalIgnoreCase)) return "\u8ECA\u4E21";
        if (typeId.Contains("Tire", StringComparison.OrdinalIgnoreCase)) return "\u8ECA\u4E21";
        if (typeId.Contains("Tool", StringComparison.OrdinalIgnoreCase)) return "\u5DE5\u5177";
        if (typeId.Contains("Env", StringComparison.OrdinalIgnoreCase)) return "\u74B0\u5883";
        if (typeId.Contains("Imported", StringComparison.OrdinalIgnoreCase)) return "\u8FFD\u52A0";
        return "\u305D\u306E\u4ED6";
    }

    public static string BuildCategoryVisualLabel(string typeId)
    {
        string category = BuildCategoryLabel(typeId);
        return string.IsNullOrWhiteSpace(category) ? "?" : category.Substring(0, 1);
    }
}
