using System;
using System.Collections.Generic;
using System.Linq;

// Rows of the "配置した部品" list: placement roots first, the parts inside a model nested below
// them. Parts stay collapsed unless expanded, or unless they (or a part below) match the search.
// Placement roots that share an editor group get one heading row above their members.
public static class SkillSyncPartsTree
{
    public const int MaxIndentLevels = 5;

    public sealed class Row
    {
        public PlacedObject part;      // for a group heading: the group's leading member
        public string groupId;         // set only on group heading rows
        public int depth;
        public string title, detail;
        public bool hasChildren, expanded;
        public bool IsGroup => groupId != null;
        // Rows are keyed by this, so a group heading and its leading member never collide.
        public object Key => groupId != null ? (object)("group:" + groupId) : part;
    }

    public static PlacedObject ParentOf(PlacedObject part)
    {
        var parent = part != null ? part.transform.parent : null;
        return parent != null ? parent.GetComponentInParent<PlacedObject>(true) : null;
    }

    public static List<Row> Build(IEnumerable<PlacedObject> all, ISet<PlacedObject> expanded, ISet<string> collapsedGroups, string filter, out int rootCount, out int matchCount)
    {
        var set = new HashSet<PlacedObject>(all.Where(p => p != null));
        var children = new Dictionary<PlacedObject, List<PlacedObject>>();
        var roots = new List<PlacedObject>();
        foreach (var part in set)
        {
            var parent = ParentOf(part);
            if (parent == null || !set.Contains(parent)) { roots.Add(part); continue; }
            if (!children.TryGetValue(parent, out var list)) children[parent] = list = new List<PlacedObject>();
            list.Add(part);
        }
        roots.Sort((a, b) => { int c = string.CompareOrdinal(a.Id ?? "", b.Id ?? ""); return c != 0 ? c : a.GetInstanceID().CompareTo(b.GetInstanceID()); });
        foreach (var list in children.Values) list.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

        filter = (filter ?? "").Trim();
        bool filtering = filter.Length > 0;
        var descendants = new Dictionary<PlacedObject, int>();
        var subtreeMatches = new Dictionary<PlacedObject, bool>();
        int matches = 0;
        int Count(PlacedObject p)
        {
            if (descendants.TryGetValue(p, out int cached)) return cached;
            int n = 0;
            if (children.TryGetValue(p, out var kids)) foreach (var kid in kids) n += 1 + Count(kid);
            return descendants[p] = n;
        }
        bool Matches(PlacedObject p) => filtering &&
            (p.GetDisplayName().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
             p.GetDescription().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
        bool SubtreeMatches(PlacedObject p)
        {
            if (subtreeMatches.TryGetValue(p, out bool cached)) return cached;
            bool found = Matches(p);
            if (found) matches++;
            if (children.TryGetValue(p, out var kids)) foreach (var kid in kids) found |= SubtreeMatches(kid);
            return subtreeMatches[p] = found;
        }

        var rows = new List<Row>();
        void Emit(PlacedObject p, int depth, string title)
        {
            if (filtering && !SubtreeMatches(p)) return;
            children.TryGetValue(p, out var kids);
            int kidCount = kids != null ? kids.Count : 0;
            bool kidMatches = filtering && kidCount > 0 && kids.Any(SubtreeMatches);
            bool open = kidCount > 0 && (kidMatches || expanded.Contains(p));
            string detail = p.GetDescription();
            if (string.IsNullOrWhiteSpace(detail) && kidCount > 0) detail = "内部の部品 " + Count(p) + "個";
            rows.Add(new Row { part = p, depth = depth, title = title, detail = detail, hasChildren = kidCount > 0, expanded = open });
            if (!open) return;
            var shown = kidMatches ? kids.Where(SubtreeMatches).ToList() : kids;
            var names = SiblingTitles(kids);
            foreach (var kid in shown) Emit(kid, depth + 1, names[kid]);
        }
        var groupRoots = new Dictionary<string, List<PlacedObject>>();
        foreach (var root in roots)
        {
            if (string.IsNullOrEmpty(root.editorGroupId)) continue;
            if (!groupRoots.TryGetValue(root.editorGroupId, out var members)) groupRoots[root.editorGroupId] = members = new List<PlacedObject>();
            members.Add(root);
        }
        var emittedGroups = new HashSet<string>();
        foreach (var root in roots)
        {
            string id = root.editorGroupId;
            if (!string.IsNullOrEmpty(id) && groupRoots[id].Count >= 2)
            {
                if (emittedGroups.Add(id)) EmitGroup(id, groupRoots[id]);
                continue;
            }
            Emit(root, 0, root.GetDisplayName());
        }
        void EmitGroup(string id, List<PlacedObject> members)
        {
            if (filtering && !members.Any(SubtreeMatches)) return;
            bool open = filtering || !collapsedGroups.Contains(id);
            var leader = SelectionGroups.Leader(members);
            rows.Add(new Row { part = leader, groupId = id, depth = 0, title = "グループ「" + leader.GetDisplayName() + "」",
                detail = "部品" + members.Count + "個", hasChildren = true, expanded = open });
            if (!open) return;
            foreach (var member in members) Emit(member, 1, member.GetDisplayName());
        }
        rootCount = roots.Count;
        matchCount = matches;
        return rows;
    }

    // Parts of one model often repeat a name; number the repeats the way ImportedModelParts.Label does.
    static Dictionary<PlacedObject, string> SiblingTitles(List<PlacedObject> siblings)
    {
        var totals = new Dictionary<string, int>();
        foreach (var s in siblings) { string n = s.GetDisplayName(); totals[n] = totals.TryGetValue(n, out int t) ? t + 1 : 1; }
        var seen = new Dictionary<string, int>();
        var titles = new Dictionary<PlacedObject, string>();
        foreach (var s in siblings)
        {
            string n = s.GetDisplayName();
            int ordinal = seen.TryGetValue(n, out int o) ? o + 1 : 1;
            seen[n] = ordinal;
            titles[s] = totals[n] > 1 ? n + " (" + ordinal + ")" : n;
        }
        return titles;
    }
}
