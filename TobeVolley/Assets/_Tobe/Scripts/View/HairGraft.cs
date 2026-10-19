// Пересадка причёски с одной VRoid-модели на другую вместе с физикой волос.
// Делается на клоне ДО его включения: Vrm10Instance соберёт пружины из списка SpringBone.Springs в Start, так что
// пересаженные пряди качаются так же, как у донора. Как это работает:
//  * свои волосы клона (сетки с материалом «hair») убираются вместе с их пружинами;
//  * кости прядей донора (всё, что не J_Bip_*, у сеток волос) копируются под «якорь» на голове клона; якорь масштабирован
//    по расстоянию между глазами, чтобы причёска села на голову другого размера;
//  * сетка волос донора ставится на клон: кости головы донора заменяются якорем, кости прядей — копиями. Бинд-позы сетки
//    остаются донорскими: вершина = кость клона × обратная донорская поза × вершина, то есть волосы переносятся жёстко
//    вместе с головой (проверено формулой скиннинга Unity);
//  * пружины донора по этим прядям добавляются клону с его коллайдерами (по имени группы, иначе — все).
using System.Collections.Generic;
using UnityEngine;
using UniVRM10;

namespace Tobe.View
{
    public static class HairGraft
    {
        static bool IsHairMesh(SkinnedMeshRenderer r)
        {
            // только сетка, у которой ВСЕ материалы волосяные («Hair001»): у тела VRoid тоже есть материал HairBack
            if (r == null || r.sharedMesh == null || r.sharedMaterials.Length == 0) return false;
            foreach (var m in r.sharedMaterials) if (m == null || !m.name.ToLowerInvariant().Contains("hair")) return false;
            return true;
        }
        static bool IsBody(Transform t) => t.name.StartsWith("J_Bip_");   // скелет VRoid; всё остальное у сеток волос — пряди

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++) { var r = Find(root.GetChild(i), name); if (r != null) return r; }
            return null;
        }

        public static void Apply(GameObject clone, GameObject donor)
        {
            var cv = clone.GetComponent<Vrm10Instance>(); var dv = donor.GetComponent<Vrm10Instance>();
            var cHead = Find(clone.transform, "J_Bip_C_Head"); var dHead = Find(donor.transform, "J_Bip_C_Head");
            if (cv == null || dv == null || cHead == null || dHead == null) { Debug.LogWarning("[Tobe] причёска: модель не VRoid — пропуск"); return; }

            var dHair = new List<SkinnedMeshRenderer>();
            foreach (var r in donor.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (IsHairMesh(r)) dHair.Add(r);
            if (dHair.Count == 0) return;

            // кости донора, нужные волосам: пряди копируем, кости тела ищем у клона по имени
            var strandRoots = new List<Transform>();
            foreach (var r in dHair)
                foreach (var b in r.bones)
                {
                    if (b == null || IsBody(b)) continue;
                    var top = b; while (top.parent != null && !IsBody(top.parent) && top.parent != donor.transform) top = top.parent;
                    if (!strandRoots.Contains(top)) strandRoots.Add(top);
                }
            foreach (var r in dHair)
                foreach (var b in r.bones)
                    if (b != null && IsBody(b) && b != dHead && Find(clone.transform, b.name) == null) { Debug.LogWarning("[Tobe] причёска: у модели нет кости " + b.name); return; }

            // убрать свои волосы клона и их пружины
            var ownBones = new HashSet<Transform>();
            foreach (var r in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (IsHairMesh(r)) { foreach (var b in r.bones) if (b != null && !IsBody(b)) ownBones.Add(b); Object.DestroyImmediate(r.gameObject); }
            cv.SpringBone.Springs.RemoveAll(s => s.Joints.Exists(j => j != null && ownBones.Contains(j.transform)));

            // якорь на голове клона: донорская голова → якорь (с подгонкой размера головы)
            float scale = 1f;
            var cl = Find(clone.transform, "J_Adj_L_FaceEye"); var cr = Find(clone.transform, "J_Adj_R_FaceEye");
            var dl = Find(donor.transform, "J_Adj_L_FaceEye"); var dr = Find(donor.transform, "J_Adj_R_FaceEye");
            if (cl != null && cr != null && dl != null && dr != null)
            {
                float a = (cl.position - cr.position).magnitude, b = (dl.position - dr.position).magnitude;
                if (a > 1e-4f && b > 1e-4f) scale = Mathf.Clamp(a / b, 0.85f, 1.18f);
            }
            var anchor = new GameObject("HairAnchor").transform;
            anchor.SetParent(cHead, false);
            anchor.localScale = Vector3.one * (scale * dHead.lossyScale.x / Mathf.Max(1e-4f, cHead.lossyScale.x));

            var map = new Dictionary<Transform, Transform> { [dHead] = anchor };
            foreach (var top in strandRoots)
            {
                var copy = Object.Instantiate(top.gameObject, anchor, false).transform;
                Vector3 lp = dHead.InverseTransformPoint(top.position); Quaternion lr = Quaternion.Inverse(dHead.rotation) * top.rotation;
                copy.localPosition = lp; copy.localRotation = lr; copy.localScale = top.lossyScale.x / Mathf.Max(1e-4f, dHead.lossyScale.x) * Vector3.one;
                MapTree(top, copy, map);
            }

            // сетки волос донора на клоне
            foreach (var r in dHair)
            {
                var go = new GameObject("Hair_" + donor.name.Replace("Template_", ""));
                go.transform.SetParent(clone.transform, false);
                var nr = go.AddComponent<SkinnedMeshRenderer>();
                var bones = r.bones; var nb = new Transform[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    var b = bones[i];
                    nb[i] = b == null ? null : map.TryGetValue(b, out var m) ? m : Find(clone.transform, b.name);
                }
                nr.sharedMesh = r.sharedMesh; nr.sharedMaterials = r.sharedMaterials; nr.bones = nb;
                nr.rootBone = anchor; nr.localBounds = new Bounds(Vector3.zero, Vector3.one * 1.2f); nr.updateWhenOffscreen = r.updateWhenOffscreen;
                nr.shadowCastingMode = r.shadowCastingMode; nr.receiveShadows = r.receiveShadows;
            }

            // пружины донора по этим прядям — клону, с его коллайдерами
            foreach (var s in dv.SpringBone.Springs)
            {
                if (s.Joints.Count == 0 || !s.Joints.TrueForAll(j => j != null && map.ContainsKey(j.transform))) continue;
                var ns = new Vrm10InstanceSpringBone.Spring(s.Name + "_graft");
                foreach (var j in s.Joints) { var cj = map[j.transform].GetComponent<VRM10SpringBoneJoint>(); if (cj != null) ns.Joints.Add(cj); }
                foreach (var g in s.ColliderGroups)
                {
                    var cg = g == null ? null : cv.SpringBone.ColliderGroups.Find(x => x != null && x.Name == g.Name);
                    if (cg != null && !ns.ColliderGroups.Contains(cg)) ns.ColliderGroups.Add(cg);
                }
                if (ns.ColliderGroups.Count == 0) ns.ColliderGroups.AddRange(cv.SpringBone.ColliderGroups);
                if (ns.Joints.Count > 0) cv.SpringBone.Springs.Add(ns);
            }
        }

        static void MapTree(Transform src, Transform dst, Dictionary<Transform, Transform> map)
        {
            map[src] = dst;
            dst.name = src.name;
            for (int i = 0; i < src.childCount && i < dst.childCount; i++) MapTree(src.GetChild(i), dst.GetChild(i), map);
        }
    }
}
