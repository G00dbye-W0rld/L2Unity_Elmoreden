using System.Collections.Generic;
using UnityEngine;

// Un restart recharge Menu.unity, qui recree LoadingCamera et UI alors que les
// precedents ont survecu : sans cette garde ils s'accumulent, et la seconde
// LoadingCamera reste active puisque GameManager ne connait que la premiere.
public class DontDestroyOnLoad : MonoBehaviour
{
    private static readonly Dictionary<string, GameObject> _kept = new Dictionary<string, GameObject>();

    void Awake()
    {
        string key = gameObject.name;

        if (_kept.TryGetValue(key, out GameObject existing) && existing != null && existing != gameObject)
        {
            Destroy(gameObject);
            return;
        }

        _kept[key] = gameObject;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (_kept.TryGetValue(gameObject.name, out GameObject kept) && kept == gameObject)
        {
            _kept.Remove(gameObject.name);
        }
    }
}
