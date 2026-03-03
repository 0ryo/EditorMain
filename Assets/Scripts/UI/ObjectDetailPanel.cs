using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ワールド上のオブジェクトを選択すると画面右側へスライドインして表示し、
/// 選択解除でスライドアウト後に非表示になるオブジェクト詳細パネル。
/// </summary>
public class ObjectDetailPanel : MonoBehaviour
{
    [SerializeField] Text textPrefabLabel;
    [SerializeField] InputField inputObjectName;  // 編集可能なオブジェクト表示名
    [SerializeField] Text textDescription;
    [SerializeField] GameObject rowDescription;   // 説明行全体（説明文が空のとき非表示）

    SelectionService selectionService;
    CatalogUI catalogUI;
    RectTransform rt;
    PlacedObject currentPo;

    // Start() でキャプチャするパネルの安静位置
    Vector2 restOffsetMin;
    Vector2 restOffsetMax;

    Coroutine slideCoroutine;

    const float SlideDuration = 0.2f; // 秒

    void Start()
    {
        rt = (RectTransform)transform;
        restOffsetMin = rt.offsetMin;
        restOffsetMax = rt.offsetMax;

        selectionService = FindFirstObjectByType<SelectionService>();
        catalogUI        = FindFirstObjectByType<CatalogUI>();

        if (selectionService != null)
            selectionService.OnSelectionChanged += OnSelectionChanged;

        if (inputObjectName != null)
            inputObjectName.onEndEdit.AddListener(OnNameInputEndEdit);

        DesignTokenApplier.ApplyDetailPanel(transform);
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (selectionService != null)
            selectionService.OnSelectionChanged -= OnSelectionChanged;
    }

    void OnSelectionChanged(PlacedObject po)
    {
        if (po == null)
        {
            currentPo = null;
            if (gameObject.activeSelf)
            {
                if (slideCoroutine != null) StopCoroutine(slideCoroutine);
                slideCoroutine = StartCoroutine(SlideOut());
            }
            return;
        }

        currentPo = po;
        Populate(po);

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            if (slideCoroutine != null) StopCoroutine(slideCoroutine);
            slideCoroutine = StartCoroutine(SlideIn());
        }
        // すでに表示中なら内容更新のみ
    }

    /// <summary>InputField の編集確定時に PlacedObject の表示名を更新する。</summary>
    void OnNameInputEndEdit(string value)
    {
        if (currentPo == null) return;
        currentPo.SetDisplayName(value);
        // 空入力の場合は id を表示に戻す（プレースホルダーではなく実値として）
        if (inputObjectName != null)
            inputObjectName.text = currentPo.GetDisplayName();
    }

    void Populate(PlacedObject po)
    {
        string label       = po.typeId ?? string.Empty;
        string description = string.Empty;

        if (catalogUI != null)
            catalogUI.TryGetTypeInfo(po.typeId, out label, out description);

        if (textPrefabLabel != null)
            textPrefabLabel.text = label;

        if (inputObjectName != null)
            inputObjectName.text = po.GetDisplayName();

        bool hasDescription = !string.IsNullOrWhiteSpace(description);
        if (textDescription != null)
            textDescription.text = hasDescription ? description : string.Empty;
        if (rowDescription != null)
            rowDescription.SetActive(hasDescription);
    }

    IEnumerator SlideIn()
    {
        float panelWidth = restOffsetMax.x - restOffsetMin.x;
        float elapsed    = 0f;

        rt.offsetMin = new Vector2(restOffsetMin.x + panelWidth, restOffsetMin.y);
        rt.offsetMax = new Vector2(restOffsetMax.x + panelWidth, restOffsetMax.y);

        while (elapsed < SlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t     = Mathf.Clamp01(elapsed / SlideDuration);
            float eased = 1f - (1f - t) * (1f - t); // ease-out quad

            float shift = Mathf.Lerp(panelWidth, 0f, eased);
            rt.offsetMin = new Vector2(restOffsetMin.x + shift, restOffsetMin.y);
            rt.offsetMax = new Vector2(restOffsetMax.x + shift, restOffsetMax.y);
            yield return null;
        }

        rt.offsetMin   = restOffsetMin;
        rt.offsetMax   = restOffsetMax;
        slideCoroutine = null;
    }

    IEnumerator SlideOut()
    {
        float panelWidth = restOffsetMax.x - restOffsetMin.x;
        float elapsed    = 0f;
        float startShift = rt.offsetMin.x - restOffsetMin.x;

        while (elapsed < SlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t     = Mathf.Clamp01(elapsed / SlideDuration);
            float eased = t * t; // ease-in quad

            float shift = Mathf.Lerp(startShift, panelWidth, eased);
            rt.offsetMin = new Vector2(restOffsetMin.x + shift, restOffsetMin.y);
            rt.offsetMax = new Vector2(restOffsetMax.x + shift, restOffsetMax.y);
            yield return null;
        }

        rt.offsetMin   = restOffsetMin;
        rt.offsetMax   = restOffsetMax;
        slideCoroutine = null;
        gameObject.SetActive(false);
    }
}
