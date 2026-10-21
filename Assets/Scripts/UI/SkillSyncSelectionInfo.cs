using System.Collections.Generic;
using System.Linq;

// What the current selection is, in the words shown on the right panel: the unit (one part, a part of a model,
// a whole model/group, several loose parts) and what can be done with it.
public sealed class SkillSyncSelectionInfo
{
    public const string DrillHint = "もう一度クリックすると中の部品を選べます";

    public int count;                 // selected units (a model or flat group counts once per root)
    public string title = "";
    public string hint = "";
    public bool compactTitle;         // long summaries use a smaller heading
    public bool multiple;             // several units move together, so number fields mean "by how much"
    public bool canGroup, canUngroup;

    public static SkillSyncSelectionInfo Describe(SelectionService selection)
    {
        var info = new SkillSyncSelectionInfo();
        var items = selection == null ? new List<PlacedObject>() : selection.Selected.Where(SelectionService.CanEdit).ToList();
        info.count = items.Count;
        if (items.Count == 0) return info;
        info.canUngroup = items.Any(item => !string.IsNullOrEmpty(item.editorGroupId));

        if (items.Count == 1)
        {
            var part = items[0];
            int inside = part.GetComponentsInChildren<PlacedObject>(true).Length - 1;
            var parent = SkillSyncPartsTree.ParentOf(part);
            if (inside > 0)
            {
                info.title = "グループ「" + part.GetDisplayName() + "」（部品" + inside + "個）";
                info.hint = DrillHint;
                info.compactTitle = true;
            }
            else if (parent != null)
            {
                info.title = "「" + parent.GetDisplayName() + "」の部品「" + part.GetDisplayName() + "」";
                info.compactTitle = true;
            }
            else info.title = part.GetDisplayName();
            return info;
        }

        info.multiple = true;
        info.compactTitle = true;
        string id = items[0].editorGroupId;
        bool oneGroup = !string.IsNullOrEmpty(id) && items.All(item => item.editorGroupId == id);
        if (oneGroup)
        {
            info.title = "グループ「" + SelectionGroups.Leader(items).GetDisplayName() + "」（部品" + items.Count + "個）";
            info.hint = DrillHint;
        }
        else
        {
            info.title = items.Count + "個の部品を選択中";
            info.canGroup = true;
        }
        return info;
    }
}
