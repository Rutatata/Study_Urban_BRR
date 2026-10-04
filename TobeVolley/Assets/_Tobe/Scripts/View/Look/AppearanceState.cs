// Per-model bookkeeping for CharacterAppearance (so Apply can be called repeatedly without stacking scales or gear).
using System.Collections.Generic;
using UnityEngine;

namespace Tobe.View
{
    public sealed class AppearanceState : MonoBehaviour
    {
        public bool hasBase;
        public Vector3 baseScale = Vector3.one;
        public readonly Dictionary<Transform, Vector3> boneScales = new Dictionary<Transform, Vector3>();
    }
}
