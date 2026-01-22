using UnityEngine;

public class IdGenerator : MonoBehaviour {
    public static IdGenerator I { get; private set; }

    [Header("Runtime sequence (MVP-2)")]
    [SerializeField] private int seq = 0;

    private void Awake() {
        if (I != null && I != this) {
            Destroy(gameObject);
            return;
        }
        I = this;
    }

    public string NewObjectId() {
        seq++;
        return $"obj-{seq:D4}";
    }
}
