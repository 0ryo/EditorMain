using System;

public static class PlacementCategoryUtility
{
    public static readonly string[] Labels = { "車両", "工具", "環境", "追加", "その他" };

    public static string ForTypeId(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId)) return "その他";
        if (typeId.IndexOf("Vehicle", StringComparison.OrdinalIgnoreCase) >= 0 ||
            typeId.IndexOf("Tire", StringComparison.OrdinalIgnoreCase) >= 0) return "車両";
        if (typeId.IndexOf("Tool", StringComparison.OrdinalIgnoreCase) >= 0) return "工具";
        if (typeId.IndexOf("Env", StringComparison.OrdinalIgnoreCase) >= 0) return "環境";
        if (typeId.IndexOf("Imported", StringComparison.OrdinalIgnoreCase) >= 0) return "追加";
        return "その他";
    }
}
