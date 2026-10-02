using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class P1PlayModeSmokeTests
{
    readonly List<GameObject> createdObjects = new List<GameObject>();

    [TestCase(1366, 768)]
    [TestCase(1920, 1080)]
    [TestCase(2560, 1440)]
    public void UiScaleKeepsOneTimesLowerBound(int width, int height)
    {
        var scaleController = RuntimeType("UiScaleController");
        float effectiveScale = (float)scaleController.GetMethod("CalculateEffectiveScale", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { width, height, 1f });

        Assert.That(effectiveScale, Is.GreaterThanOrEqualTo(1f));
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var gameObject in createdObjects)
            if (gameObject != null) UnityEngine.Object.Destroy(gameObject);
        createdObjects.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator FixedDesignRemainsInsideCanvasAfterLegacyStyling()
    {
        var canvasObject = Track(new GameObject("DesignCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var design = new GameObject("SkillSyncDesign", typeof(RectTransform));
        design.transform.SetParent(canvasObject.transform, false);
        design.AddComponent(RuntimeType("SkillSyncDesignView"));
        var rect = (RectTransform)design.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.sizeDelta = new Vector2(2560f / 1.44f, 1000f);
        rect.localScale = Vector3.one * 1.44f;
        var legacyPanel = new GameObject("LegacyPanel", typeof(RectTransform));
        legacyPanel.transform.SetParent(canvasObject.transform, false);

        var controllerType = RuntimeType("UiScaleController");
        var controller = controllerType.GetMethod("Ensure").Invoke(null, new object[] { canvasObject.transform });
        foreach (float setting in new[] { 1f, 1.4f, 0.8f })
        {
            controllerType.GetMethod("Apply").Invoke(controller, new object[] { setting });
            RuntimeType("DesignTokenApplier").GetMethod("ApplyCatalogPanel").Invoke(null, new object[] { legacyPanel.transform });
            yield return null;
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(-1f));
            Assert.That(corners[0].y, Is.GreaterThanOrEqualTo(-1f));
            Assert.That(corners[2].x, Is.LessThanOrEqualTo(Screen.width + 1f));
            Assert.That(corners[2].y, Is.LessThanOrEqualTo(Screen.height + 1f));
        }
    }

    [UnityTest]
    public IEnumerator FixedDesignHitAreasKeepTheirAuthoredSpacing()
    {
        var design = Track(new GameObject("FixedDesign", typeof(RectTransform)));
        design.AddComponent(RuntimeType("SkillSyncDesignView"));
        design.transform.localScale = Vector3.one * 0.5f;
        var first = new GameObject("Previous", typeof(RectTransform), typeof(Image), typeof(Button));
        first.transform.SetParent(design.transform, false);
        var rect = (RectTransform)first.transform;
        rect.sizeDelta = new Vector2(40, 32);
        var helper = RuntimeType("UiAccessibilityMetrics");
        helper.GetMethod("EnsureButtonTarget").Invoke(null, new object[] { first.GetComponent<Button>() });
        Assert.That(rect.rect.width, Is.EqualTo(40f));
        Assert.That(rect.rect.height, Is.EqualTo(32f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator KeyboardFocusRingSharesTheSelectedControlsCenter()
    {
        var root = Track(new GameObject("FocusCanvas", typeof(RectTransform), typeof(Canvas)));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var button = new GameObject("FocusTarget", typeof(RectTransform), typeof(Image), typeof(Button));
        button.transform.SetParent(root.transform, false);
        ((RectTransform)button.transform).anchoredPosition = new Vector2(140, -80);
        var type = RuntimeType("KeyboardAccessibilityController");
        var controller = type.GetMethod("Ensure").Invoke(null, new object[] { root.transform });
        yield return null;
        Canvas.ForceUpdateCanvases();
        type.GetMethod("UpdateFocusRing", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, new object[] { button });
        var ring = (RectTransform)root.transform.Find("KeyboardFocusRing");
        Assert.That(Vector3.Distance(ring.position, button.transform.position), Is.LessThan(1f));
        Assert.That(ring.GetComponent<Image>(), Is.Null, "Focus indicator must not fill the control interior");
        Assert.That(ring.GetComponentsInChildren<Image>().Length, Is.EqualTo(4));
        Assert.That(ring.GetComponentsInChildren<Image>().All(edge => !edge.raycastTarget), Is.True);
    }

    [UnityTest]
    public IEnumerator ButtonTargetExpandsToFortyFourPixels()
    {
        var buttonObject = Track(new GameObject("TouchTarget", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement)));
        var layout = buttonObject.GetComponent<LayoutElement>();
        layout.minWidth = 24f;
        layout.minHeight = 32f;

        var helper = RuntimeType("UiAccessibilityMetrics");
        helper.GetMethod("EnsureButtonTarget", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { buttonObject.GetComponent<Button>() });

        Assert.That(layout.minWidth, Is.GreaterThanOrEqualTo(44f));
        Assert.That(layout.minHeight, Is.GreaterThanOrEqualTo(44f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator FixedButtonTargetAccountsForParentScaleAndCaptionToken()
    {
        var canvasObject = Track(new GameObject("LayoutCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaledParent = Track(new GameObject("ScaledParent", typeof(RectTransform)));
        scaledParent.transform.SetParent(canvasObject.transform, false);
        scaledParent.transform.localScale = Vector3.one * 0.5f;
        var buttonObject = Track(new GameObject("FixedTouchTarget", typeof(RectTransform), typeof(Image), typeof(Button)));
        var rect = (RectTransform)buttonObject.transform;
        rect.SetParent(scaledParent.transform, false);
        rect.sizeDelta = new Vector2(24f, 24f);

        var helper = RuntimeType("UiAccessibilityMetrics");
        helper.GetMethod("EnsureButtonTarget", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { buttonObject.GetComponent<Button>() });

        Assert.That(rect.rect.width * 0.5f * canvasObject.GetComponent<Canvas>().scaleFactor,
            Is.GreaterThanOrEqualTo(44f));
        Assert.That(rect.rect.height * 0.5f * canvasObject.GetComponent<Canvas>().scaleFactor,
            Is.GreaterThanOrEqualTo(44f));
        Assert.That((int)RuntimeType("DesignTokens").GetField("FontSizeCaption").GetRawConstantValue(), Is.EqualTo(14));
        yield return null;
    }

    [UnityTest]
    public IEnumerator SelectionServiceTracksAnEditablePlacedObject()
    {
        var placed = CreatePlacedCube("Selectable", Vector3.zero);
        var selectionObject = Track(new GameObject("SelectionServiceSmoke"));
        var selectionType = RuntimeType("SelectionService");
        var selection = selectionObject.AddComponent(selectionType);

        selectionType.GetMethod("Select", new[] { RuntimeType("PlacedObject") })
            .Invoke(selection, new[] { placed.GetComponent(RuntimeType("PlacedObject")) });

        Assert.That(selectionType.GetField("Current").GetValue(selection), Is.EqualTo(placed.GetComponent(RuntimeType("PlacedObject"))));
        Assert.That(((System.Collections.ICollection)selectionType.GetProperty("Selected").GetValue(selection)).Count, Is.EqualTo(1));
        yield return null;
    }

    [UnityTest]
    public IEnumerator TransformGestureSessionCommitsTheGizmoPosition()
    {
        var placed = CreatePlacedCube("GizmoTarget", Vector3.zero);
        var selectionObject = Track(new GameObject("GizmoSelectionService"));
        var selectionType = RuntimeType("SelectionService");
        var selection = selectionObject.AddComponent(selectionType);
        selectionType.GetMethod("Select", new[] { RuntimeType("PlacedObject") })
            .Invoke(selection, new[] { placed.GetComponent(RuntimeType("PlacedObject")) });

        var sessionType = RuntimeType("SelectionTransformSession");
        var session = Activator.CreateInstance(sessionType, new object[] { selection });
        placed.transform.position = Vector3.right;
        sessionType.GetMethod("Commit").Invoke(session, new object[] { "Test gizmo move" });

        Assert.That(placed.transform.position.x, Is.EqualTo(1f).Within(0.001f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator SettingsModalCapturesEditingAndKeyboardFocus()
    {
        var root = Track(new GameObject("ModalCanvas", typeof(RectTransform), typeof(Canvas)));
        var catalogType = RuntimeType("CatalogUI");
        var catalog = (Behaviour)root.AddComponent(catalogType);
        catalog.enabled = false;
        var modal = new GameObject("Settings", typeof(RectTransform));
        modal.transform.SetParent(root.transform, false);
        var button = new GameObject("Inside", typeof(RectTransform), typeof(Image), typeof(Button));
        button.transform.SetParent(modal.transform, false);
        var events = Track(new GameObject("Events", typeof(UnityEngine.EventSystems.EventSystem)))
            .GetComponent<UnityEngine.EventSystems.EventSystem>();
        var active = catalogType.GetField("activeCatalog", BindingFlags.Static | BindingFlags.NonPublic);
        var previous = active.GetValue(null);
        catalogType.GetField("settingsPanel", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(catalog, (RectTransform)modal.transform);
        active.SetValue(null, catalog);
        try
        {
            var workspace = RuntimeType("EditWorkspace");
            Assert.That(workspace.GetProperty("HasOpenModal").GetValue(null), Is.True);
            var keyboard = RuntimeType("KeyboardAccessibilityController");
            var controller = keyboard.GetMethod("Ensure").Invoke(null, new object[] { root.transform });
            keyboard.GetMethod("MoveFocus", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { events, false });
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(button));
            modal.SetActive(false);
            Assert.That(workspace.GetProperty("HasOpenModal").GetValue(null), Is.False);
        }
        finally { active.SetValue(null, previous); }
        yield return null;
    }

    [UnityTest]
    public IEnumerator PlacementDoesNotSnapAChildToItsOwnModel()
    {
        var parent = Track(new GameObject("Model"));
        parent.AddComponent(RuntimeType("PlacedObject"));
        var moving = CreatePlacedCube("MovingPart", Vector3.zero);
        moving.transform.SetParent(parent.transform, true);
        var sibling = CreatePlacedCube("Sibling", new Vector3(1.1f, 0, 0));
        sibling.transform.SetParent(parent.transform, true);
        Physics.SyncTransforms();
        object[] args = { moving.GetComponent(RuntimeType("PlacedObject")), 0.2f, Vector3.right, Vector3.zero };
        Assert.That(RuntimeType("PlacementObjectSnapper").GetMethod("TrySnap").Invoke(null, args), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator PlacementSnapsToNearbyObjectFace()
    {
        var moving = CreatePlacedCube("Moving", Vector3.zero);
        CreatePlacedCube("Target", new Vector3(1.1f, 0f, 0f));
        Physics.SyncTransforms();

        var snapper = RuntimeType("PlacementObjectSnapper");
        var method = snapper.GetMethod("TrySnap", BindingFlags.Public | BindingFlags.Static);
        object[] arguments = { moving.GetComponent(RuntimeType("PlacedObject")), 0.2f, Vector3.right, Vector3.zero };
        bool snapped = (bool)method.Invoke(null, arguments);

        Assert.That(snapped, Is.True);
        var result = (Vector3)arguments[3];
        Assert.That(result.x, Is.EqualTo(0.1f).Within(0.01f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator PlacementSurfaceRaycastFindsPlacedObjectTop()
    {
        CreatePlacedCube("Surface", Vector3.zero);
        var cameraObject = Track(new GameObject("SurfaceSnapCamera", typeof(Camera)));
        var camera = cameraObject.GetComponent<Camera>();
        camera.transform.position = new Vector3(0f, 3f, 0f);
        camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        camera.nearClipPlane = 0.1f;
        Physics.SyncTransforms();
        yield return null;

        var workspace = RuntimeType("EditWorkspace");
        object[] arguments = { camera, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), Vector3.zero };
        bool found = (bool)workspace.GetMethod("TryScreenToPlacedSurface", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, arguments);

        Assert.That(found, Is.True);
        var surfacePoint = (Vector3)arguments[2];
        Assert.That(surfacePoint.y, Is.EqualTo(0.5f).Within(0.02f));
    }

    [UnityTest]
    public IEnumerator GraphServiceAcceptsAValidStepConnection()
    {
        var graphObject = Track(new GameObject("GraphSmoke"));
        var graphType = RuntimeType("CurriculumGraphService");
        var graph = graphObject.AddComponent(graphType);
        var start = graphType.GetMethod("GetStartNode").Invoke(graph, null);
        var step = graphType.GetMethod("AddStep").Invoke(graph, null);
        string startId = (string)start.GetType().GetField("nodeId").GetValue(start);
        string stepId = (string)step.GetType().GetField("nodeId").GetValue(step);
        object[] arguments = { startId, stepId, null };

        bool connected = (bool)graphType.GetMethod("TryAddEdge").Invoke(graph, arguments);

        Assert.That(connected, Is.True);
        Assert.That(arguments[2], Is.Null);
        yield return null;
    }

    [UnityTest]
    public IEnumerator GraphServiceAddsTheFirstStepThroughItsCommandBoundary()
    {
        var graphObject = Track(new GameObject("FirstStepSmoke"));
        var graphType = RuntimeType("CurriculumGraphService");
        var graph = graphObject.AddComponent(graphType);
        object[] addArguments = { null, null };
        bool added = (bool)graphType.GetMethod("TryAddStepAtEnd").Invoke(graph, addArguments);

        Assert.That(added, Is.True);
        Assert.That(addArguments[0], Is.Not.Null);
        Assert.That(addArguments[1], Is.Null);
        object[] sequenceArguments = { null, null };
        bool linear = (bool)graphType.GetMethod("TryBuildLinearStepSequence").Invoke(graph, sequenceArguments);
        Assert.That(linear, Is.True);
        Assert.That(((System.Collections.ICollection)sequenceArguments[0]).Count, Is.EqualTo(1));
        yield return null;
    }

    GameObject CreatePlacedCube(string name, Vector3 position)
    {
        var cube = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        cube.name = name;
        cube.transform.position = position;
        cube.AddComponent(RuntimeType("PlacedObject"));
        return cube;
    }

    GameObject Track(GameObject gameObject)
    {
        createdObjects.Add(gameObject);
        return gameObject;
    }

    static Type RuntimeType(string typeName)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName, false))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, "Runtime type not found: " + typeName);
        return type;
    }
}
