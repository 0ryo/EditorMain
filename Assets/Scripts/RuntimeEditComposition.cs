using UnityEngine;

public static class RuntimeEditComposition
{
    const string RuntimeSystemsName = "RuntimePlacementSystems";

    public static PlacementController ResolvePlacementController(
        PlacementController current,
        PrefabRegistry registry)
    {
        if (current != null)
        {
            EnsureEditServices(current, registry);
            return current;
        }

        current = UnityEngine.Object.FindFirstObjectByType<PlacementController>();
        if (current == null)
        {
            var controllers = UnityEngine.Object.FindObjectsByType<PlacementController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (controllers != null && controllers.Length > 0)
            {
                current = controllers[0];
                current.gameObject.SetActive(true);
                current.enabled = true;
                Debug.LogWarning($"[CatalogUI] Re-enabled inactive PlacementController: {current.name}");
            }
        }

        if (current == null)
        {
            var systems = GameObject.Find(RuntimeSystemsName);
            if (systems == null) systems = new GameObject(RuntimeSystemsName);

            current = systems.GetComponent<PlacementController>();
            if (current == null)
            {
                current = systems.AddComponent<PlacementController>();
                Debug.LogWarning("[CatalogUI] Created runtime PlacementController because none was found in the loaded scene.");
            }
        }

        EnsureEditServices(current, registry);

        if (registry != null && (current.registry == null || !current.registry.HasEntries))
        {
            current.registry = registry;
        }

        if (current.cam == null)
        {
            current.cam = EditWorkspace.ResolveCamera();
        }

        if (current.selection == null)
        {
            current.selection = UnityEngine.Object.FindFirstObjectByType<SelectionService>();
        }

        return current;
    }

    public static void EnsureEditServices(PlacementController placementController, PrefabRegistry registry)
    {
        var systems = placementController != null
            ? placementController.gameObject
            : GameObject.Find(RuntimeSystemsName);
        if (systems == null) systems = new GameObject(RuntimeSystemsName);

        var camera = EditWorkspace.ResolveCamera(placementController != null ? placementController.cam : null);

        var editMode = ResolveOrCreateService<EditModeService>(systems, "EditModeService");
        if (editMode != null)
        {
            editMode.enabled = true;
        }

        var commandService = ResolveOrCreateService<CommandService>(systems, "CommandService");
        if (commandService != null)
        {
            commandService.enabled = true;
        }

        var selectionOutline = ResolveOrCreateService<SelectionOutline>(systems, "SelectionOutline");
        if (selectionOutline != null)
        {
            selectionOutline.enabled = true;
        }

        var selection = ResolveOrCreateService<SelectionService>(systems, "SelectionService");
        if (selection != null)
        {
            selection.enabled = true;
            if (selection.cam == null) selection.cam = camera;
            if (selection.registry == null) selection.registry = registry;
            if (selection.placementController == null) selection.placementController = placementController;
            if (selection.outline == null) selection.outline = selectionOutline;
            if (selection.Current != null && selection.outline != null)
            {
                selection.outline.ShowFor(selection.Current.gameObject);
            }
        }

        var moveTool = ResolveOrCreateService<MoveTool>(systems, "MoveTool");
        if (moveTool != null)
        {
            moveTool.enabled = true;
            if (moveTool.cam == null) moveTool.cam = camera;
            if (moveTool.sel == null) moveTool.sel = selection;
        }

        if (selection != null && selection.moveTool == null)
        {
            selection.moveTool = moveTool;
        }

        if (placementController != null)
        {
            if (placementController.cam == null) placementController.cam = camera;
            if (placementController.selection == null) placementController.selection = selection;
        }
    }

    static T ResolveOrCreateService<T>(GameObject fallbackHost, string serviceName) where T : MonoBehaviour
    {
        var service = UnityEngine.Object.FindFirstObjectByType<T>();
        if (service == null)
        {
            var services = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (services != null && services.Length > 0)
            {
                service = services[0];
                service.gameObject.SetActive(true);
                service.enabled = true;
                Debug.LogWarning($"[CatalogUI] Re-enabled inactive {serviceName}: {service.name}");
            }
        }

        if (service != null) return service;

        service = fallbackHost.GetComponent<T>();
        if (service == null)
        {
            service = fallbackHost.AddComponent<T>();
            Debug.LogWarning($"[CatalogUI] Created runtime {serviceName} because none was found in the loaded scene.");
        }

        return service;
    }
}
