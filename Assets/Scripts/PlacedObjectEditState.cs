using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PlacedObjectEditState : MonoBehaviour
{
    [SerializeField] bool locked;
    [SerializeField] bool hidden;
    [SerializeField] List<ColliderState> colliderStates = new();
    [SerializeField] List<RendererState> rendererStates = new();

    bool colliderLockApplied;
    bool visibilityApplied;

    [Serializable]
    struct ColliderState
    {
        public Collider collider;
        public bool enabled;
    }

    [Serializable]
    struct RendererState
    {
        public Renderer renderer;
        public bool enabled;
    }

    public bool Locked => locked;
    public bool Hidden => hidden;

    void OnEnable()
    {
        if (locked || hidden)
        {
            if (colliderStates.Count > 0)
            {
                foreach (var state in colliderStates)
                {
                    if (state.collider != null) state.collider.enabled = false;
                }
                colliderLockApplied = true;
            }
            else
            {
                ApplyColliderLock();
            }
        }

        if (hidden)
        {
            if (rendererStates.Count > 0)
            {
                foreach (var state in rendererStates)
                {
                    if (state.renderer != null) state.renderer.enabled = false;
                }
                visibilityApplied = true;
            }
            else
            {
                ApplyVisibility();
            }
        }
    }

    void OnDestroy()
    {
        RestoreColliders();
        RestoreRenderers();
    }

    public void SetLocked(bool value)
    {
        if (locked == value) return;
        locked = value;
        RefreshColliderLock();
    }

    public void SetVisible(bool value)
    {
        bool nextHidden = !value;
        if (hidden == nextHidden) return;
        hidden = nextHidden;
        if (hidden) ApplyVisibility();
        else RestoreRenderers();
        RefreshColliderLock();
    }

    void RefreshColliderLock()
    {
        if (locked || hidden) ApplyColliderLock();
        else RestoreColliders();
    }

    void ApplyColliderLock()
    {
        if (colliderLockApplied) return;

        colliderStates.Clear();
        var colliders = GetComponentsInChildren<Collider>(true);
        foreach (var collider in colliders)
        {
            if (collider == null) continue;
            colliderStates.Add(new ColliderState { collider = collider, enabled = collider.enabled });
            collider.enabled = false;
        }
        colliderLockApplied = true;
    }

    void RestoreColliders()
    {
        foreach (var state in colliderStates)
        {
            if (state.collider != null) state.collider.enabled = state.enabled;
        }
        colliderStates.Clear();
        colliderLockApplied = false;
    }

    void ApplyVisibility()
    {
        if (visibilityApplied) return;

        rendererStates.Clear();
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            rendererStates.Add(new RendererState { renderer = renderer, enabled = renderer.enabled });
            renderer.enabled = false;
        }
        visibilityApplied = true;
    }

    void RestoreRenderers()
    {
        foreach (var state in rendererStates)
        {
            if (state.renderer != null) state.renderer.enabled = state.enabled;
        }
        rendererStates.Clear();
        visibilityApplied = false;
    }
}
