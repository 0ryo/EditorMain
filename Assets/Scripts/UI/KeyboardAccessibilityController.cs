using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class KeyboardAccessibilityController : MonoBehaviour
{
    const string FocusRingName = "KeyboardFocusRing";

    [SerializeField] Transform uiRoot;
    RectTransform focusRing;
    bool keyboardFocus;

    public static KeyboardAccessibilityController Ensure(Transform context)
    {
        if (context == null) return null;
        var canvas = context.GetComponentInParent<Canvas>(true);
        if (canvas == null) return null;
        canvas = canvas.rootCanvas;

        var controller = canvas.GetComponent<KeyboardAccessibilityController>();
        if (controller == null) controller = canvas.gameObject.AddComponent<KeyboardAccessibilityController>();
        controller.uiRoot = canvas.transform;
        controller.EnsureFocusRing();
        return controller;
    }

    void Update()
    {
        if (!Application.isFocused)
        {
            if (focusRing != null) focusRing.gameObject.SetActive(false);
            return;
        }

        var eventSystem = EventSystem.current;
        if (EditInput.LeftPressedThisFrame()) keyboardFocus = false;
        UpdateFocusRing(keyboardFocus && eventSystem != null ? eventSystem.currentSelectedGameObject : null);
        if (eventSystem == null || !EditInput.TabPressedThisFrame()) return;
        keyboardFocus = true;
        MoveFocus(eventSystem, EditInput.ShiftPressed());
    }

    void OnDisable()
    {
        if (focusRing != null) focusRing.gameObject.SetActive(false);
    }

    void EnsureFocusRing()
    {
        if (uiRoot == null) uiRoot = transform;
        if (focusRing == null)
        {
            var existing = uiRoot.Find(FocusRingName) as RectTransform;
            if (existing != null) focusRing = existing;
        }
        if (focusRing == null)
        {
            var ring = new GameObject(FocusRingName, typeof(RectTransform), typeof(LayoutElement));
            focusRing = ring.GetComponent<RectTransform>();
            focusRing.SetParent(uiRoot, false);
            focusRing.anchorMin = focusRing.anchorMax = ((RectTransform)uiRoot).pivot;
            focusRing.pivot = new Vector2(0.5f, 0.5f);
            ring.GetComponent<LayoutElement>().ignoreLayout = true;
            // Outline with useGraphicAlpha=false fills the transparent quad too.
            // Four strips keep the center empty.
            for (int i = 0; i < 4; i++)
            {
                var edge = new GameObject("Edge", typeof(RectTransform), typeof(Image));
                var rect = edge.GetComponent<RectTransform>();
                rect.SetParent(focusRing, false);
                bool horizontal = i < 2;
                float side = i % 2;
                rect.anchorMin = horizontal ? new Vector2(0, side) : new Vector2(side, 0);
                rect.anchorMax = horizontal ? new Vector2(1, side) : new Vector2(side, 1);
                rect.sizeDelta = horizontal ? new Vector2(0, 3) : new Vector2(3, 0);
                var image = edge.GetComponent<Image>();
                image.color = DesignTokens.Accent;
                image.raycastTarget = false;
            }
        }
        focusRing.gameObject.SetActive(false);
    }

    void UpdateFocusRing(GameObject selected)
    {
        if (focusRing == null) EnsureFocusRing();
        var modalRoot = EditWorkspace.GetOpenModalRoot(uiRoot);
        if (modalRoot != null && selected != null && selected.transform != modalRoot && !selected.transform.IsChildOf(modalRoot))
            selected = null;
        if (focusRing == null || selected == null || !selected.activeInHierarchy || selected.GetComponent<RectTransform>() == null)
        {
            if (focusRing != null) focusRing.gameObject.SetActive(false);
            return;
        }

        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(uiRoot, selected.transform);
        focusRing.anchoredPosition = bounds.center;
        focusRing.sizeDelta = new Vector2(bounds.size.x + 6f, bounds.size.y + 6f);
        if (focusRing.GetSiblingIndex() != focusRing.parent.childCount - 1)
            focusRing.SetAsLastSibling();
        focusRing.gameObject.SetActive(true);
    }

    void MoveFocus(EventSystem eventSystem, bool backwards)
    {
        if (uiRoot == null) return;
        var scope = EditWorkspace.GetOpenModalRoot(uiRoot);
        if (scope == null) scope = uiRoot;

        var candidates = scope.GetComponentsInChildren<Selectable>(false);
        var navigable = new List<Selectable>(candidates.Length);
        foreach (var selectable in candidates)
        {
            if (selectable == null || !selectable.isActiveAndEnabled || !selectable.IsInteractable()) continue;

            var navigation = selectable.navigation;
            if (navigation.mode == Navigation.Mode.None)
            {
                navigation.mode = Navigation.Mode.Automatic;
                selectable.navigation = navigation;
            }
            navigable.Add(selectable);
        }
        if (navigable.Count == 0) return;

        int currentIndex = navigable.FindIndex(item => item.gameObject == eventSystem.currentSelectedGameObject);
        int nextIndex;
        if (currentIndex < 0) nextIndex = backwards ? navigable.Count - 1 : 0;
        else nextIndex = (currentIndex + (backwards ? navigable.Count - 1 : 1)) % navigable.Count;
        eventSystem.SetSelectedGameObject(navigable[nextIndex].gameObject);
    }
}
