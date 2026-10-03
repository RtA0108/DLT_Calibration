// ComponentLister.cs
using UnityEngine;

public class ComponentLister : MonoBehaviour
{
    void Awake()
    {
        Debug.LogWarning($"========== RUNTIME COMPONENT LIST ==========");
        Debug.Log($"--- Components on PARENT '{gameObject.name}' ---", gameObject);
        foreach (Component component in GetComponents<Component>())
        {
            Debug.Log($"- {component.GetType().Name}");
        }

        if (transform.childCount > 0)
        {
            Transform child = transform.GetChild(0);
            Debug.Log($"--- Components on CHILD '{child.gameObject.name}' ---", child.gameObject);
            foreach (Component component in child.GetComponents<Component>())
            {
                Debug.Log($"- {component.GetType().Name}");
            }
        }
        Debug.LogWarning($"============================================");
    }
}