using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public class CatalogUI : MonoBehaviour
{
    public PrefabRegistry registry;
    public RectTransform content;      // ScrollViewのContent
    public Button buttonTemplate;      // 1行のボタンプレハブ

    // Inspector から登録できる string イベント
    [System.Serializable]
    public class StringEvent : UnityEvent<string> { }

    // 選択時コールバック（Inspector に「On Select Type」として出てくる）
    public StringEvent onSelectType;

    void Start()
    {
        if (!registry)        { Debug.LogError("CatalogUI: registry not set");        return; }
        if (!content)         { Debug.LogError("CatalogUI: content not set");         return; }
        if (!buttonTemplate)  { Debug.LogError("CatalogUI: buttonTemplate not set");  return; }

        // 既存の子をクリア
        foreach (Transform c in content)
            Destroy(c.gameObject);

        // PrefabRegistry からボタンを生成
        foreach (var e in registry.entries)
        {
            var btn = Instantiate(buttonTemplate, content);
            btn.gameObject.SetActive(true);

            // ラベル設定（Text / TMP のどちらにも対応）
            var legacy = btn.GetComponentInChildren<Text>();
            if (legacy != null)
            {
                legacy.text = e.typeId;
            }
            else
            {
                var tmp = btn.GetComponentInChildren<TMP_Text>();
                if (tmp != null) tmp.text = e.typeId;
            }

            string id = e.typeId; // クロージャ用ローカルコピー

            // ボタンクリック時に UnityEvent<string> を発火
            btn.onClick.AddListener(() =>
            {
                if (onSelectType != null)
                {
                    onSelectType.Invoke(id);
                }
            });
        }
    }
}
