using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SkillSyncLibraryRemove : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public UnityEngine.UI.Button remove;
    void OnEnable() {remove.gameObject.SetActive(false);}
    public void OnPointerEnter(PointerEventData e) {remove.gameObject.SetActive(true);}
    public void OnPointerExit(PointerEventData e) {remove.gameObject.SetActive(false);}
}
