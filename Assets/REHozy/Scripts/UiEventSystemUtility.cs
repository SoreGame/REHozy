using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace REHozy
{
    /// <summary>
    /// Keeps UI clicks working even if EventSystem was parented under quest-hidden world objects.
    /// </summary>
    public static class UiEventSystemUtility
    {
        public static bool EnsureAvailable()
        {
            if (EventSystem.current != null)
                return false;

            var systems = Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (var es in systems)
            {
                var go = es.gameObject;
                if (go.transform.parent != null)
                    go.transform.SetParent(null, worldPositionStays: true);

                if (!go.activeSelf)
                    go.SetActive(true);

                if (!es.enabled)
                    es.enabled = true;

                return true;
            }

            var created = new GameObject("EventSystem");
            created.AddComponent<EventSystem>();
            created.AddComponent<InputSystemUIInputModule>();
            return true;
        }
    }
}
