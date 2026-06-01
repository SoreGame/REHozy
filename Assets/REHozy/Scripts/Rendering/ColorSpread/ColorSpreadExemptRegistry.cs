using System.Collections.Generic;
using UnityEngine;

namespace REHozy.Rendering
{
    /// <summary>
    /// Roots whose pixels should keep full scene color (skip ColorSpread desaturation).
    /// </summary>
    public static class ColorSpreadExemptRegistry
    {
        static readonly HashSet<GameObject> s_Roots = new();
        static readonly List<Renderer> s_Renderers = new();
        static readonly List<Renderer> s_Scratch = new();

        public static int RootCount => s_Roots.Count;

        public static void Register(GameObject root)
        {
            if (root != null)
            {
                s_Roots.Add(root);
            }
        }

        public static void Unregister(GameObject root)
        {
            if (root != null)
            {
                s_Roots.Remove(root);
            }
        }

        public static void Clear() => s_Roots.Clear();

        public static bool TryCollectActiveRenderers(List<Renderer> output)
        {
            output.Clear();
            if (s_Roots.Count == 0)
            {
                return false;
            }

            foreach (var root in s_Roots)
            {
                if (root == null || !root.activeInHierarchy)
                {
                    continue;
                }

                root.GetComponentsInChildren(true, s_Scratch);
                foreach (var renderer in s_Scratch)
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    output.Add(renderer);
                }
            }

            return output.Count > 0;
        }

        public static bool TryCollectActiveRenderers() => TryCollectActiveRenderers(s_Renderers);
    }
}
