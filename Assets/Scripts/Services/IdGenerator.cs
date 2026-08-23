using UnityEngine;

public class IdGenerator : MonoBehaviour {
    public static IdGenerator I { get; private set; }

    [Header("Runtime sequence (MVP-2)")]
    [SerializeField] private int seq = 0;

    private void Awake() {
        if (I != null && I != this) {
            Destroy(this);
            return;
        }
        I = this;
    }

    private void OnDestroy() {
        if (I == this) I = null;
    }

    public string NewObjectId() {
        seq++;
        return $"obj-{seq:D4}";
    }
}
