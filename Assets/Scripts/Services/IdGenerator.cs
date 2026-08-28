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

    public void ReserveExistingObjectId(string objectId) {
        if (string.IsNullOrWhiteSpace(objectId) || !objectId.StartsWith("obj-")) return;
        if (!int.TryParse(objectId.Substring(4), out int existingSequence)) return;
        if (existingSequence > seq) seq = existingSequence;
    }
}
