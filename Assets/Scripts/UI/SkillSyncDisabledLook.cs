using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Makes a control that cannot be used look unavailable and say why on hover. The dimming follows Selectable.interactable,
// so every place that already enables/disables a control is covered; the control's backdrop (a separate prefab graphic at the
// same place) is dimmed with it. The hover text is the control's normal caption while enabled and the reason while disabled.
public sealed class SkillSyncDisabledLook : MonoBehaviour
{
    public const float DimmedAlpha = .4f;
    Selectable control;
    Func<string> reason;
    Func<IEnumerable<GameObject>> backdropSource;
    CanvasGroup own;
    readonly List<CanvasGroup> backdrops = new();
    SkillSyncHoverTip tip;
    string enabledText;
    bool? wasInteractable;
    bool backdropsFound;
    int nextReasonFrame;

    public static SkillSyncDisabledLook Attach(Selectable control, TMP_FontAsset font, Func<string> reason, Func<IEnumerable<GameObject>> backdropSource)
    {
        if (control == null) return null;
        var look = control.GetComponent<SkillSyncDisabledLook>();
        if (look == null) look = control.gameObject.AddComponent<SkillSyncDisabledLook>();
        look.control = control; look.reason = reason; look.backdropSource = backdropSource;
        look.tip = control.GetComponent<SkillSyncHoverTip>();
        if (look.tip == null) { look.tip = control.gameObject.AddComponent<SkillSyncHoverTip>(); look.tip.font = font; }
        look.enabledText = look.tip.text;
        look.wasInteractable = null;
        return look;
    }

    // The caption shown while the control is usable (a disabled control shows its reason instead).
    public void SetHint(string text)
    {
        enabledText = text;
        if (tip != null && wasInteractable != false) tip.text = text;
    }

    void LateUpdate()
    {
        if (control == null) return;
        bool on = control.IsInteractable();
        if (on != wasInteractable)
        {
            if (tip != null)
            {
                if (on) tip.text = enabledText;
                else enabledText = tip.text;
            }
            wasInteractable = on;
            Dim(on ? 1f : DimmedAlpha);
            nextReasonFrame = 0;
        }
        if (!on && tip != null && Time.frameCount >= nextReasonFrame)
        {
            nextReasonFrame = Time.frameCount + 15;
            tip.text = reason?.Invoke() ?? "";
        }
    }

    void OnDisable()
    {
        // A backdrop is shared by whatever control sits there in another state, so it must not stay dim.
        if (wasInteractable == false)
        {
            Dim(1f);
            if (tip != null) tip.text = enabledText;
        }
        wasInteractable = null;
    }

    void Dim(float alpha)
    {
        if (own == null) { own = GetComponent<CanvasGroup>(); if (own == null) own = gameObject.AddComponent<CanvasGroup>(); }
        own.alpha = alpha;
        if (!backdropsFound && backdropSource != null)
        {
            backdropsFound = true;
            foreach (var go in backdropSource())
            {
                if (go == null || go == gameObject) continue;
                var group = go.GetComponent<CanvasGroup>();
                if (group == null) group = go.AddComponent<CanvasGroup>();
                backdrops.Add(group);
            }
        }
        foreach (var group in backdrops) if (group != null) group.alpha = alpha;
    }
}
