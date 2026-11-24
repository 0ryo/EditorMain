using System;
using System.Collections.Generic;
using UnityEngine;

// The ideal motion of one completion condition, planned from where the parts are now. The trial autoplay plays the
// pieces over time on the trial copies; the step editor applies them at once (SkillSyncStepPose), so a later step or
// condition shows the parts as the earlier ones left them. Both use the same plan, so they always agree.
// Directions never depend on the camera: a part is pulled out of the model it belongs to (a tire off the car),
// pushed into it, and turned as seen from that outside, where the learner stands.
public static class SkillSyncConditionMotion
{
    public const float Lift = .05f, Nudge = .03f, PullMin = .4f, PullMax = 1.2f, GroundSlack = .005f, GroundedSlack = .02f;

    public enum Kind { Move, Hold, Hide }

    // Move: translate by `move` while turning by `angle` about `axis` through `pivot` (the part's own center unless fixed).
    public sealed class Piece
    {
        public Kind kind;
        public Transform target;
        public Vector3 move, axis = Vector3.up, pivot;
        public float angle, duration;
        public bool fixedPivot;
    }

    public sealed class Plan
    {
        public readonly List<Piece> pieces = new();
        public string note = "";
        public bool judged, playable = true;
        public float hold;
    }

    // Finds the parts of the lesson and the whole model a part belongs to (the car of a nut).
    public sealed class Scene
    {
        public Func<string, Transform> find;
        public Func<Transform, Transform> rootOf;
    }

    public static Vector3 Center(Transform t) => PlacedObjectGrounding.TryGetRendererBounds(t, out var b) ? b.center : t.position;

    public static Plan Make(ConditionNodeData c, IReadOnlyList<ScenarioNode> stepConditions, Scene scene)
    {
        var plan = new Plan();
        var a = scene.find(c.objectAId);
        var b = scene.find(c.objectBId);
        string label = ConditionTypeCatalog.Find(c.type)?.label ?? c.type;
        float distance = ConditionTypeCatalog.GetNumber(c, ConditionTypeCatalog.DistanceKey, .1f);
        float hold = ConditionTypeCatalog.GetNumber(c, ConditionTypeCatalog.HoldSecondsKey, 0);
        float angle = ConditionTypeCatalog.GetNumber(c, ConditionTypeCatalog.AngleKey, 90);
        bool pair = a != null && b != null && !b.IsChildOf(a);
        switch (c.type)
        {
            case ConditionTypeCatalog.Proximity:
            case ConditionTypeCatalog.SnapHold:
            case ConditionTypeCatalog.Separation:
                if (!pair) return Unplayable(plan, label);
                {
                    bool apart = c.type == ConditionTypeCatalog.Separation;
                    bool tool = !apart && IsToolApproach(c, stepConditions);
                    float now = JudgeDistance(a, b, tool);
                    bool already = apart ? now >= distance : now <= distance * .5f;
                    if (!already && tool && TryToolShape(a, out var shape)) PutToolOn(plan, a, b, shape, scene);
                    else if (!already)
                    {
                        Vector3 from = Center(a), to = Center(b), offset = from - to;
                        // Parts both standing on the floor (a jack and its marker) are compared on the floor, not in height.
                        if (Grounded(a) && Grounded(b)) { offset.y = 0; to.y = from.y; }
                        Vector3 direction = offset.sqrMagnitude > 1e-8f ? offset.normalized : Horizontal(Outward(a, scene));
                        // Half the range inside, so the gauge visibly passes the line; Separation goes a little past it.
                        var target = to + direction * distance * (apart ? 1.2f : .5f);
                        if (!apart) target = AboveGround(a, from, target, to, distance * .5f);
                        Move(plan, a, target - from);
                    }
                }
                plan.judged = true; plan.hold = hold;
                plan.note = c.type == ConditionTypeCatalog.SnapHold ? $"{label}：近づける先まで動かし、{hold:0.##}秒とどめます" :
                    c.type == ConditionTypeCatalog.Separation ? $"{label}：{distance * 100:0.##} cm以上離します" : $"{label}：近づける先の範囲まで動かします";
                break;
            case ConditionTypeCatalog.RotationMatch:
                if (!pair) return Unplayable(plan, label);
                (b.rotation * Quaternion.Inverse(a.rotation)).ToAngleAxis(out float match, out var matchAxis);
                if (match > 180) match -= 360;
                if (Mathf.Abs(match) > .01f) Turn(plan, a, matchAxis, match);
                plan.judged = true; plan.hold = hold;
                plan.note = $"{label}：向きの基準にそろえ、{hold:0.##}秒とどめます";
                break;
            case ConditionTypeCatalog.Turn:
                if (a == null) return Unplayable(plan, label);
                {
                    bool counter = (int)ConditionTypeCatalog.GetNumber(c, ConditionTypeCatalog.TurnDirectionKey, 0) == ConditionTypeCatalog.TurnCounterclockwise;
                    // A tool brought to a part in the same step (a wrench to a nut) turns about that part's axis,
                    // whatever axis the tool's model was made with.
                    var partner = scene.find(TurnPartner(c.objectAId, stepConditions));
                    if (partner != null && (partner == a || partner.IsChildOf(a) || a.IsChildOf(partner))) partner = null;
                    var axis = TurnAxis(partner != null ? partner : a, scene);
                    // The axis points at the learner outside; a positive angle about it looks clockwise from there.
                    Turn(plan, a, axis, counter ? -angle : angle, partner);
                    plan.note = $"{label}：{angle:0.##}度、{(counter ? "反時計回り" : "時計回り")}に回します";
                }
                break;
            case ConditionTypeCatalog.Grabbed:
                if (a == null) return Unplayable(plan, label);
                Move(plan, a, Vector3.up * Lift, .35f); Hold(plan, a, .3f);
                plan.note = $"{label}：持ち上げる動きで示します";
                break;
            case ConditionTypeCatalog.Released:
                if (a == null) return Unplayable(plan, label);
                Move(plan, a, Vector3.up * Lift, .35f); Hold(plan, a, .3f); Move(plan, a, Vector3.down * Lift, .35f);
                plan.note = $"{label}：持ち上げて置く動きで示します";
                break;
            case ConditionTypeCatalog.Push:
                if (a == null) return Unplayable(plan, label);
                // Pushed in, and it stays in: the next steps start from there.
                Move(plan, a, -PullDirection(a, scene) * Nudge, .3f);
                plan.note = $"{label}：奥へ押す動きで示します";
                break;
            case ConditionTypeCatalog.Pull:
                if (a == null) return Unplayable(plan, label);
                {
                    var toward = PullDirection(a, scene);
                    float size = PlacedObjectGrounding.TryGetRendererBounds(a, out var bounds) ? bounds.size.magnitude : .3f;
                    if (ConditionTypeCatalog.PullsOff(c))
                    {
                        // Pulled well clear, then gone: the part is off for the following steps.
                        Move(plan, a, toward * Mathf.Clamp(size * 1.5f, PullMin, PullMax), 1.2f);
                        Hold(plan, a, .2f);
                        plan.pieces.Add(new Piece { kind = Kind.Hide, target = a });
                        plan.note = $"{label}：手前へ引き抜いて取り外す動きで示します";
                    }
                    else
                    {
                        // A short pull that stays: the next step starts from where the part now is.
                        Move(plan, a, toward * Mathf.Min(size * .3f, PullMin), .8f);
                        plan.note = $"{label}：手前へ少し引く動きで示します";
                    }
                }
                break;
            default:
                return Unplayable(plan, label);
        }
        return plan;
    }

    static Plan Unplayable(Plan plan, string label)
    {
        plan.playable = false;
        plan.note = $"{label}：PCでは再現できない動きです";
        return plan;
    }

    // Applies a whole plan at once (the step editor's view of the parts after a condition). Hidden parts are left to
    // the caller: the step editor hides pulled-off parts itself. `touching` hears of each part before it moves.
    public static void ApplyAtOnce(Plan plan, Action<Transform> touching)
    {
        foreach (var piece in plan.pieces)
        {
            if (piece.kind != Kind.Move || piece.target == null) continue;
            touching?.Invoke(piece.target);
            var state = Begin(piece);
            Step(piece, state, 1);
        }
    }

    // Where a piece started; Step moves the target to fraction t of the piece from there.
    public struct Start
    {
        public Vector3 position, pivot;
        public Quaternion rotation;
        public bool onGround;
    }

    public static Start Begin(Piece piece)
    {
        var t = piece.target;
        return new Start
        {
            position = t.position, rotation = t.rotation,
            pivot = piece.fixedPivot ? piece.pivot : Center(t),
            onGround = PlacedObjectGrounding.TryGetRendererBounds(t, out var start) && start.min.y >= EditWorkspace.GroundY - GroundSlack
        };
    }

    public static void Step(Piece piece, Start start, float t)
    {
        var turn = Quaternion.AngleAxis(piece.angle * t, piece.axis);
        piece.target.SetPositionAndRotation(start.pivot + piece.move * t + turn * (start.position - start.pivot), turn * start.rotation);
        // Whatever the motion (a swing, a set-down), a part that started above the floor does not sink into it.
        if (start.onGround && PlacedObjectGrounding.TryGetRendererBounds(piece.target, out var now) && now.min.y < EditWorkspace.GroundY)
            piece.target.position += Vector3.up * (EditWorkspace.GroundY - now.min.y);
    }

    static void Move(Plan plan, Transform target, Vector3 move, float duration = -1)
    {
        if (duration < 0) duration = Mathf.Clamp(.5f + move.magnitude, .5f, 1.5f);
        plan.pieces.Add(new Piece { target = target, move = move, duration = duration });
    }

    // With a center, the target swings about that part (a wrench about the nut) instead of about its own middle.
    static void Turn(Plan plan, Transform target, Vector3 axis, float angle, Transform center = null) =>
        plan.pieces.Add(new Piece { target = target, axis = axis, angle = angle, duration = Mathf.Clamp(Mathf.Abs(angle) / 180f, .6f, 3f),
            fixedPivot = center != null, pivot = center != null ? Center(center) : Vector3.zero });

    static void Hold(Plan plan, Transform target, float duration) =>
        plan.pieces.Add(new Piece { kind = Kind.Hold, target = target, duration = duration });

    // ---- Measuring the distance of a pair (the trial's judgement and the distance shown while editing) ----

    // A tool to be turned on a part is measured at its tip (the socket of a wrench), not at the middle of its handle.
    // Two parts standing on the floor (a jack and the marker where it goes) are measured on the floor, ignoring height,
    // so a tall part can still reach a flat mark. Anything else is measured between the middles of the shapes.
    public static float JudgeDistance(Transform a, Transform b, bool tool) =>
        MeasurePoints(a, b, tool, out var from, out var to) ? Vector3.Distance(from, to) : float.PositiveInfinity;

    // The two points the distance is measured between (also where the editor draws the measuring line).
    public static bool MeasurePoints(Transform a, Transform b, bool tool, out Vector3 from, out Vector3 to)
    {
        from = to = Vector3.zero;
        if (a == null || b == null) return false;
        to = Center(b);
        if (tool && TryToolShape(a, out var shape)) { from = a.TransformPoint(shape.tip); return true; }
        from = Center(a);
        if (Grounded(a) && Grounded(b)) from.y = to.y;
        return true;
    }

    public static bool Grounded(Transform t) =>
        PlacedObjectGrounding.TryGetRendererBounds(t, out var bounds) && Mathf.Abs(bounds.min.y - EditWorkspace.GroundY) <= GroundedSlack;

    // "近づける" A to B where the same step also turns A: A is a tool put on B (a wrench on a nut).
    public static bool IsToolApproach(ConditionNodeData c, IReadOnlyList<ScenarioNode> stepConditions)
    {
        if (c == null || stepConditions == null || string.IsNullOrEmpty(c.objectAId) || string.IsNullOrEmpty(c.objectBId)) return false;
        if (c.type != ConditionTypeCatalog.Proximity && c.type != ConditionTypeCatalog.SnapHold) return false;
        foreach (var node in stepConditions)
        {
            var other = node?.condition;
            if (other != null && other.type == ConditionTypeCatalog.Turn && other.objectAId == c.objectAId) return true;
        }
        return false;
    }

    // ---- Directions ----

    static Vector3 Horizontal(Vector3 v)
    {
        v.y = 0;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.back;
    }

    // Away from the middle of the whole model (a nut on the left of the car: to the left). A part that is a model of its
    // own has no inside; it faces the front of the default view.
    static Vector3 Outward(Transform part, Scene scene)
    {
        var root = scene.rootOf != null ? scene.rootOf(part) : part.root;
        if (root == null || root == part) return Vector3.back;
        var away = Center(part) - Center(root);
        return away.sqrMagnitude > 1e-8f ? away.normalized : Vector3.back;
    }

    // A round part (a tire, a nut) comes off along its axis; anything else straight out of its model, level.
    static Vector3 PullDirection(Transform part, Scene scene)
    {
        var outward = Outward(part, scene);
        if (TrySymmetryAxis(part, out var axis)) return Vector3.Dot(axis, outward) < 0 ? -axis : axis;
        return Horizontal(outward);
    }

    // The other part of a "近づける" condition on the same object in this step (the nut a wrench is put on).
    public static string TurnPartner(string objectId, IReadOnlyList<ScenarioNode> stepConditions)
    {
        if (string.IsNullOrEmpty(objectId) || stepConditions == null) return null;
        foreach (var node in stepConditions)
        {
            var other = node?.condition;
            if (other == null || (other.type != ConditionTypeCatalog.Proximity && other.type != ConditionTypeCatalog.SnapHold)) continue;
            if (other.objectAId == objectId && !string.IsNullOrEmpty(other.objectBId)) return other.objectBId;
            if (other.objectBId == objectId && !string.IsNullOrEmpty(other.objectAId)) return other.objectAId;
        }
        return null;
    }

    // A turning part is round about its axis: of its three sides (in its own orientation), two are about equal and
    // the axis is the remaining one (a nut, a wheel, a bolt). Otherwise the thinnest world side, preferring vertical.
    // The axis points out of the model, toward where the learner stands.
    static Vector3 TurnAxis(Transform part, Scene scene)
    {
        if (!TrySymmetryAxis(part, out var axis))
        {
            var size = PlacedObjectGrounding.TryGetRendererBounds(part, out var bounds) ? bounds.size : Vector3.one;
            axis = Vector3.up;
            float thinnest = size.y * .9f;
            if (size.x < thinnest) { axis = Vector3.right; thinnest = size.x; }
            if (size.z < thinnest) axis = Vector3.forward;
            if (axis == Vector3.up) return axis;
        }
        if (Vector3.Dot(axis, Outward(part, scene)) < 0) axis = -axis;
        return axis;
    }

    static bool TrySymmetryAxis(Transform part, out Vector3 axis)
    {
        axis = Vector3.up;
        bool any = false;
        var local = new Bounds();
        foreach (var filter in part.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = filter.sharedMesh;
            if (mesh == null) continue;
            var mb = mesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = part.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (!any) { local = new Bounds(p, Vector3.zero); any = true; }
                else local.Encapsulate(p);
            }
        }
        if (!any) return false;
        var size = Vector3.Scale(local.size, part.lossyScale);
        float[] s = { Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z) };
        int best = -1;
        float bestPair = 0;
        for (int i = 0; i < 3; i++)
        {
            float j = s[(i + 1) % 3], k = s[(i + 2) % 3];
            if (j <= 1e-6f || k <= 1e-6f || s[i] <= 1e-6f) continue;
            float pair = Mathf.Min(j, k) / Mathf.Max(j, k);
            float odd = s[i] / ((j + k) * .5f);
            if (pair < .8f || (odd > .87f && odd < 1.15f)) continue;
            if (pair > bestPair) { bestPair = pair; best = i; }
        }
        if (best < 0) return false;
        axis = part.TransformDirection(best == 0 ? Vector3.right : best == 1 ? Vector3.up : Vector3.forward).normalized;
        return true;
    }

    // A part standing on the floor is never planned into it: its center is raised until its bottom is on the ground,
    // still within range of the target if that is possible.
    static Vector3 AboveGround(Transform part, Vector3 from, Vector3 target, Vector3 to, float range)
    {
        if (!PlacedObjectGrounding.TryGetRendererBounds(part, out var bounds) || bounds.min.y < EditWorkspace.GroundY - GroundSlack) return target;
        float lowest = EditWorkspace.GroundY + from.y - bounds.min.y;
        if (target.y >= lowest) return target;
        var flat = target - to; flat.y = 0;
        float rise = lowest - to.y, room = range * range - rise * rise;
        if (range > 0) flat = room > 0 && flat.sqrMagnitude > 1e-8f ? flat.normalized * Mathf.Min(flat.magnitude, Mathf.Sqrt(room)) : Vector3.zero;
        return new Vector3(to.x + flat.x, lowest, to.z + flat.z);
    }

    // ---- A tool put on a part ----

    // In the tool's own space: the tip that goes on the part (the socket), the way the tip opens, and the way the
    // handle runs from the tip.
    public struct ToolShape
    {
        public Vector3 tip, opening, handle;
    }

    static readonly Dictionary<Transform, ToolShape?> Shapes = new();

    // A long tool: its handle is by far its longest side. Its head is the end with the wider cross-section, and the
    // tip sticks out of the head sideways (the socket of a ratchet): that side is the turning axis. Read from the
    // mesh points, so it works whatever axes the model was made with; meshes that cannot be read give no shape.
    public static bool TryToolShape(Transform tool, out ToolShape shape)
    {
        shape = default;
        if (tool == null) return false;
        if (!Shapes.TryGetValue(tool, out var cached))
        {
            if (Shapes.Count > 64) Shapes.Clear();
            Shapes[tool] = cached = MeasureTool(tool);
        }
        if (cached == null) return false;
        shape = cached.Value;
        return true;
    }

    static ToolShape? MeasureTool(Transform tool)
    {
        var points = new List<Vector3>();
        foreach (var filter in tool.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter.sharedMesh;
            if (mesh == null || !mesh.isReadable) return null;
            var vertices = mesh.vertices;
            int step = Mathf.Max(1, vertices.Length / 4000);
            for (int i = 0; i < vertices.Length; i += step) points.Add(tool.InverseTransformPoint(filter.transform.TransformPoint(vertices[i])));
        }
        if (points.Count < 8) return null;
        var min = points[0]; var max = points[0];
        foreach (var p in points) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
        var size = Vector3.Scale(max - min, Abs(tool.lossyScale));
        int l = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
        int i1 = (l + 1) % 3, i2 = (l + 2) % 3;
        if (size[l] < 2.5f * Mathf.Max(size[i1], size[i2])) return null;
        float lo = min[l], hi = max[l], quarter = (hi - lo) * .25f;
        Vector3 lowMin = Vector3.positiveInfinity, lowMax = Vector3.negativeInfinity, highMin = Vector3.positiveInfinity, highMax = Vector3.negativeInfinity;
        foreach (var p in points)
        {
            if (p[l] <= lo + quarter) { lowMin = Vector3.Min(lowMin, p); lowMax = Vector3.Max(lowMax, p); }
            if (p[l] >= hi - quarter) { highMin = Vector3.Min(highMin, p); highMax = Vector3.Max(highMax, p); }
        }
        var lowSize = lowMax - lowMin; var highSize = highMax - highMin;
        bool headLow = lowSize[i1] + lowSize[i2] >= highSize[i1] + highSize[i2];
        Vector3 headMin = headLow ? lowMin : highMin, headMax = headLow ? lowMax : highMax;
        Vector3 farMin = headLow ? highMin : lowMin, farMax = headLow ? highMax : lowMax;
        // The turning axis is the side where the head sticks out past the handle the most.
        float grow1 = (headMax[i1] - headMin[i1]) - (farMax[i1] - farMin[i1]);
        float grow2 = (headMax[i2] - headMin[i2]) - (farMax[i2] - farMin[i2]);
        if (Mathf.Max(grow1, grow2) <= 1e-6f) return null;
        int t = grow1 >= grow2 ? i1 : i2, other = t == i1 ? i2 : i1;
        float mid = (farMin[t] + farMax[t]) * .5f, half = (farMax[t] - farMin[t]) * .5f;
        float sign = headMax[t] - mid >= mid - headMin[t] ? 1 : -1;
        // The tip is the middle of the points sticking out on that side.
        var sum = Vector3.zero; int count = 0;
        foreach (var p in points)
        {
            bool inHead = headLow ? p[l] <= lo + quarter : p[l] >= hi - quarter;
            if (inHead && sign * (p[t] - mid) > half) { sum += p; count++; }
        }
        if (count == 0) return null;
        var tip = sum / count;
        tip[t] = sign > 0 ? headMax[t] : headMin[t];
        var opening = Vector3.zero; opening[t] = sign;
        var handle = Vector3.zero; handle[l] = headLow ? 1 : -1;
        return new ToolShape { tip = tip, opening = opening, handle = handle };
    }

    static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    // The tool turns so its tip opens onto the part along the part's axis and moves so the tip sits on the part's
    // middle. The handle keeps the way it already points across the axis (a wrench placed handle-down stays so);
    // a handle lying along the axis is turned up (or out of the model when the axis is upright).
    static void PutToolOn(Plan plan, Transform tool, Transform part, ToolShape shape, Scene scene)
    {
        var axis = TurnAxis(part, scene);
        var handle = tool.TransformDirection(shape.handle).normalized;
        var up = handle - axis * Vector3.Dot(handle, axis);
        if (up.sqrMagnitude < .25f) up = Vector3.up - axis * Vector3.Dot(Vector3.up, axis);
        if (up.sqrMagnitude < .1f)
        {
            var away = Outward(part, scene);
            up = away - axis * Vector3.Dot(away, axis);
            if (up.sqrMagnitude < 1e-4f) up = Vector3.Cross(axis, Vector3.right);
        }
        up.Normalize();
        var openingWorldLocal = Quaternion.LookRotation(shape.opening, shape.handle);
        var target = Quaternion.LookRotation(-axis, up) * Quaternion.Inverse(openingWorldLocal);
        (target * Quaternion.Inverse(tool.rotation)).ToAngleAxis(out float angle, out var turnAxis);
        if (angle > 180) angle -= 360;
        if (float.IsNaN(turnAxis.x) || Mathf.Abs(angle) < .01f) { angle = 0; turnAxis = Vector3.up; }
        var tip = tool.TransformPoint(shape.tip);
        var move = Center(part) - tip;
        plan.pieces.Add(new Piece { target = tool, move = move, axis = turnAxis, angle = angle, fixedPivot = true, pivot = tip,
            duration = Mathf.Clamp(.6f + move.magnitude + Mathf.Abs(angle) / 360f, .6f, 2f) });
    }
}
