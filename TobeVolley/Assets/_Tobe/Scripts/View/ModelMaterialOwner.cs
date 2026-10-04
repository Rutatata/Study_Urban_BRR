// Destroys the per-clone material instances created by CharacterLibrary when the cloned model is destroyed.
using System.Collections.Generic;
using UnityEngine;

namespace Tobe.View
{
    public sealed class ModelMaterialOwner : MonoBehaviour
    {
        readonly List<Material> owned = new List<Material>();

        public void Add(Material m) { if (m != null) owned.Add(m); }

        void OnDestroy()
        {
            foreach (var m in owned) if (m != null) Destroy(m);
            owned.Clear();
        }
    }
}
