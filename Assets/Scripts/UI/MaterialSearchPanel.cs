using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MaterialSearchPanel : MonoBehaviour
{
    const string Caption = "欠損テクスチャを探して復元";
    [SerializeField] Button searchButton;
    PlacedObject selected;
    CancellationTokenSource cancellation;
    int revision;
    string lastDirectory;

    public static MaterialSearchPanel Ensure(Transform detail)
    {
        var content = detail.Find("Scroll_Detail/Viewport/Content");
        if (content == null) return null;
        var existing = content.Find("Row_MaterialSearch");
        MaterialSearchPanel ui;
        if (existing != null)
            ui = existing.GetComponent<MaterialSearchPanel>() ?? existing.gameObject.AddComponent<MaterialSearchPanel>();
        else
        {
            var row = new GameObject("Row_MaterialSearch", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(MaterialSearchPanel));
            row.transform.SetParent(content, false);
            var description = content.Find("Row_Description");
            if (description != null) row.transform.SetSiblingIndex(description.GetSiblingIndex() + 1);
            var layout = row.GetComponent<VerticalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(12,12,8,8);
            row.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ui = row.GetComponent<MaterialSearchPanel>();
        }
        ui.Prepare();
        return ui;
    }

    void Prepare()
    {
        // Upgrade both saved prefabs and already-existing runtime panels.
        foreach (string name in new[] { "SearchFolder", "ChooseFolder", "SearchResults", "CopyResults" })
        {
            var obsolete = transform.Find(name);
            if (obsolete == null) continue;
            obsolete.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(obsolete.gameObject);
            else DestroyImmediate(obsolete.gameObject);
        }
        if (searchButton == null) searchButton = transform.Find("Search")?.GetComponent<Button>();
        if (searchButton == null) searchButton = CreateButton(transform, "Search", Caption);
        searchButton.GetComponentInChildren<TMP_Text>().text = Caption;
        searchButton.onClick.RemoveListener(Search); searchButton.onClick.AddListener(Search);
        TopCenterNotification.Ensure(transform, searchButton.GetComponentInChildren<TMP_Text>());
    }

    static Button CreateButton(Transform parent, string name, string caption)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = DesignTokens.BgSecondary;
        go.GetComponent<LayoutElement>().minHeight = go.GetComponent<LayoutElement>().preferredHeight = DesignTokens.MinTouchTarget;
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        label.transform.SetParent(go.transform, false);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(8,4); label.rectTransform.offsetMax = new Vector2(-8,-4);
        label.fontSize = 14; label.color = DesignTokens.TextPrimary; label.alignment = TextAlignmentOptions.Center;
        label.text = caption; label.raycastTarget = false;
        UiAccessibilityMetrics.EnsureButtonTarget(go.GetComponent<Button>());
        return go.GetComponent<Button>();
    }

    void Start() => Prepare();
    public void Select(PlacedObject value)
    {
        if (selected == value) return;
        selected = value; revision++; cancellation?.Cancel();
        if (searchButton != null) searchButton.interactable = selected != null && cancellation == null;
    }

#if UNITY_EDITOR
    async void Search()
#else
    void Search()
#endif
    {
        if (cancellation != null || selected == null) return;
        int request = revision;
        CancellationTokenSource tokenSource = null;
        try
        {
#if UNITY_EDITOR
            // Selection is mandatory on every run. Cancel has no side effects.
            string directory = UnityEditor.EditorUtility.OpenFolderPanel("復元するテクスチャのフォルダーを選択", Directory.Exists(lastDirectory) ? lastDirectory : "", "");
            if (string.IsNullOrEmpty(directory) || this == null || revision != request) return;
            lastDirectory = directory;
#else
            throw new NotSupportedException("FBXテクスチャの復元はUnity Editorで実行してください。");
#endif
#if UNITY_EDITOR
            tokenSource = new CancellationTokenSource(); cancellation = tokenSource;
            searchButton.interactable = false;
            searchButton.GetComponentInChildren<TMP_Text>().text = "復元中…";
            var snapshot = SelectedMaterialDiagnostics.Capture(selected);
            if (!string.Equals(Path.GetExtension(snapshot.sourcePath), ".fbx", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("このモデル形式のテクスチャ復元には未対応です。");
            FbxTextureReferences.Document document = null;
            var result = await Task.Run(() =>
            {
                document = FbxTextureReferences.ReadDocument(snapshot.sourcePath, tokenSource.Token);
                foreach (var binding in document.bindings) snapshot.expectedNames.Add(FbxTextureReferences.FileName(binding.file));
                return MaterialFileSearch.Find(directory, snapshot.expectedNames, tokenSource.Token);
            }, tokenSource.Token);
            tokenSource.Token.ThrowIfCancellationRequested();
            if (this == null || revision != request || selected == null) return;
            var repair = FbxMaterialRepair.Apply(selected, snapshot.sourcePath, document, result);
            Debug.Log("[MaterialRepair] " + repair.details);
            Notify(repair.Message, repair.restored == 0 && (repair.missing > 0 || repair.already == 0));
#endif
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (this != null && revision == request) Notify("復元できませんでした: " + ex.Message, true);
            Debug.LogWarning("[MaterialRepair] " + ex);
        }
        finally
        {
            if (this != null && tokenSource != null)
            {
                cancellation = null;
                searchButton.GetComponentInChildren<TMP_Text>().text = Caption;
                searchButton.interactable = selected != null;
            }
            tokenSource?.Dispose();
        }
    }
    void Notify(string message, bool error) => TopCenterNotification.Ensure(transform, searchButton.GetComponentInChildren<TMP_Text>())?.Show(message, error, error ? 8f : 6f);
    void OnDisable() { revision++; cancellation?.Cancel(); }
    void OnDestroy() { revision++; cancellation?.Cancel(); }
}
