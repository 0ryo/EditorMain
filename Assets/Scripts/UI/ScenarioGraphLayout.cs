using System.Collections.Generic;
using UnityEngine;

internal static class ScenarioGraphLayout
{
    public static int GetNodeSortOrder(ScenarioNode node)
    {
        return node.nodeType switch
        {
            ScenarioNodeType.Start => 0,
            ScenarioNodeType.Step => 1,
            ScenarioNodeType.Condition => 2,
            ScenarioNodeType.End => 3,
            _ => 9
        };
    }

    public static Vector2 GetLargestTemplateSize(IEnumerable<Component> templates, Vector2 fallback)
    {
        var size = fallback;
        foreach (var template in templates)
        {
            var rect = template != null ? template.transform as RectTransform : null;
            if (rect == null) continue;
            size.x = Mathf.Max(size.x, rect.rect.width, rect.sizeDelta.x);
            size.y = Mathf.Max(size.y, rect.rect.height, rect.sizeDelta.y);
        }

        return size;
    }

    public static Dictionary<string, Vector2> BuildDefaultNodePositions(
        List<ScenarioNode> flowNodes,
        List<ScenarioNode> unboundConditions,
        Vector2 flowSize,
        Vector2 conditionSize,
        float nodeLayoutGap,
        int maxLayoutColumns)
    {
        var defaults = new Dictionary<string, Vector2>();
        float flowSpacingX = flowSize.x + nodeLayoutGap;
        float flowSpacingY = flowSize.y + nodeLayoutGap;
        int flowRows = Mathf.Max(1, Mathf.CeilToInt(flowNodes.Count / (float)maxLayoutColumns));
        float flowCenterY = unboundConditions.Count > 0 ? 220f : 0f;
        float flowTopY = flowCenterY + ((flowRows - 1) * flowSpacingY * 0.5f);

        for (int row = 0; row < flowRows; row++)
        {
            int rowStartIndex = row * maxLayoutColumns;
            int rowCount = Mathf.Min(maxLayoutColumns, flowNodes.Count - rowStartIndex);
            float rowStartX = -((rowCount - 1) * flowSpacingX * 0.5f);
            for (int column = 0; column < rowCount; column++)
            {
                var node = flowNodes[rowStartIndex + column];
                defaults[node.nodeId] = new Vector2(
                    rowStartX + (column * flowSpacingX),
                    flowTopY - (row * flowSpacingY));
            }
        }

        if (unboundConditions.Count == 0) return defaults;

        float conditionSpacingX = conditionSize.x + nodeLayoutGap;
        float conditionSpacingY = conditionSize.y + nodeLayoutGap;
        float flowBottomY = flowTopY - ((flowRows - 1) * flowSpacingY);
        float conditionTopY = flowBottomY - (flowSize.y * 0.5f) - (conditionSize.y * 0.5f) - nodeLayoutGap;
        int conditionRows = Mathf.CeilToInt(unboundConditions.Count / (float)maxLayoutColumns);
        for (int row = 0; row < conditionRows; row++)
        {
            int rowStartIndex = row * maxLayoutColumns;
            int rowCount = Mathf.Min(maxLayoutColumns, unboundConditions.Count - rowStartIndex);
            float rowStartX = -((rowCount - 1) * conditionSpacingX * 0.5f);
            for (int column = 0; column < rowCount; column++)
            {
                var condition = unboundConditions[rowStartIndex + column];
                defaults[condition.nodeId] = new Vector2(
                    rowStartX + (column * conditionSpacingX),
                    conditionTopY - (row * conditionSpacingY));
            }
        }

        return defaults;
    }
}
