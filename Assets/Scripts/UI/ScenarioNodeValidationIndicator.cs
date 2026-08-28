using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ScenarioNodeValidationIndicator : MonoBehaviour
{
    const string BadgeName = "Badge_Validation";

    [SerializeField] Outline issueOutline;
    [SerializeField] RectTransform badgeRoot;
    [SerializeField] TMP_Text badgeText;

    public void SetCounts(int errorCount, int warningCount)
    {
        int total = errorCount + warningCount;
        if (total <= 0)
        {
            if (issueOutline != null) issueOutline.enabled = false;
            if (badgeRoot != null) badgeRoot.gameObject.SetActive(false);
            return;
        }

        EnsureView();
        Color semanticColor = errorCount > 0 ? DesignTokens.Error : DesignTokens.Warning;
        if (issueOutline != null)
        {
            issueOutline.effectColor = semanticColor;
            issueOutline.effectDistance = new Vector2(2f, -2f);
            issueOutline.useGraphicAlpha = false;
            issueOutline.enabled = true;
        }

        string label = errorCount > 0 && warningCount > 0
            ? $"エラー {errorCount} / 注意 {warningCount}"
            : errorCount > 0
                ? $"エラー {errorCount}"
                : $"注意 {warningCount}";
        float width = errorCount > 0 && warningCount > 0 ? 132f : 76f;
        badgeRoot.sizeDelta = new Vector2(width, 24f);
        badgeRoot.gameObject.SetActive(true);
        badgeRoot.SetAsLastSibling();

        var image = badgeRoot.GetComponent<Image>();
        if (image != null)
        {
            image.color = DesignTokens.BadgeBg(semanticColor);
            image.raycastTarget = false;
        }
        if (badgeText != null)
        {
            badgeText.text = label;
            badgeText.color = semanticColor;
        }
        UiRoundedTheme.ApplyToHierarchy(badgeRoot, DesignTokens.CornerRadius);
    }

    void EnsureView()
    {
        if (issueOutline == null && GetComponent<Graphic>() != null)
        {
            issueOutline = gameObject.AddComponent<Outline>();
        }

        if (badgeRoot == null)
        {
            badgeRoot = transform.Find(BadgeName) as RectTransform;
        }
        if (badgeRoot == null)
        {
            var badgeObject = new GameObject(BadgeName, typeof(RectTransform), typeof(Image));
            badgeRoot = badgeObject.GetComponent<RectTransform>();
            badgeRoot.SetParent(transform, false);
            badgeRoot.anchorMin = Vector2.one;
            badgeRoot.anchorMax = Vector2.one;
            badgeRoot.pivot = new Vector2(0f, 1f);
            badgeRoot.anchoredPosition = new Vector2(8f, 0f);
            badgeObject.GetComponent<Image>().raycastTarget = false;
        }

        if (badgeText == null)
        {
            badgeText = badgeRoot.GetComponentInChildren<TMP_Text>(true);
        }
        if (badgeText == null)
        {
            var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var textRoot = textObject.GetComponent<RectTransform>();
            textRoot.SetParent(badgeRoot, false);
            textRoot.anchorMin = Vector2.zero;
            textRoot.anchorMax = Vector2.one;
            textRoot.offsetMin = new Vector2(8f, 0f);
            textRoot.offsetMax = new Vector2(-8f, 0f);
            badgeText = textObject.GetComponent<TMP_Text>();
            badgeText.fontSize = DesignTokens.FontSizeCaption;
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.enableWordWrapping = false;
            badgeText.raycastTarget = false;
        }
    }
}
