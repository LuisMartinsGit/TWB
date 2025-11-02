using UnityEngine;

public sealed class TechTreeDBAuthoring : MonoBehaviour
{
    [Tooltip("Human tech JSON to mount into a TechTreeDB singleton at runtime.")]
    public TextAsset humanTechJson;

    void Awake()
    {
        if (TechTreeDB.Instance == null)
        {
            var go = new GameObject("TechTreeDB");
            var db = go.AddComponent<TechTreeDB>();
            db.humanTechJson = humanTechJson;
            DontDestroyOnLoad(go);
        }
    }
}
