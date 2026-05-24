using System;
using UnityEngine;

namespace REHozy.Decoration
{
    [Serializable]
    public struct PropSpawnEntry
    {
        public GameObject prefab;

        [Min(0)]
        public int count;
    }
}
