using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CatalogUI : MonoBehaviour {
    public PrefabRegistry registry;
    public RectTransform content; // ScrollViewのContent
    public Button buttonTemplate; // 1行のボタンプレハブ

    public System.Action<string> onSelectType; // 選択時コールバック

    void Start() {
        if (!registry) { Debug.LogError("CatalogUI: registry not set"); return; }
        if (!content)  { Debug.LogError("CatalogUI: content not set");  return; }
        if (!buttonTemplate){ Debug.LogError("CatalogUI: buttonTemplate not set"); return; }
        foreach (Transform c in content) Destroy(c.gameObject);
        foreach (var e in registry.entries) {
            var btn = Instantiate(buttonTemplate, content);
            btn.gameObject.SetActive(true);
            var legacy = btn.GetComponentInChildren<Text>();
            if (legacy != null) legacy.text = e.typeId;
            else {
                var tmp = btn.GetComponentInChildren<TMP_Text>();
                if (tmp != null) tmp.text = e.typeId;
            }
            string id = e.typeId;
            btn.onClick.AddListener(()=> onSelectType?.Invoke(id));
        }
    }
}