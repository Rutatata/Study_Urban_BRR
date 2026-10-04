// A throw-away T-pose humanoid skeleton (hips 1 m high, built from code) used to bake Unity HUMANOID AnimationClips from
// Resources/Anim into muscle arrays. Muscle values are avatar independent, so any humanoid rig works as the sampling target.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UniHumanoid;
using Object = UnityEngine.Object;

namespace Tobe.View
{
    internal sealed class HumanoidRig : IDisposable
    {
        public GameObject root;
        public Animator animator;
        public Avatar avatar;
        public HumanPoseHandler handler;
        public Transform hips;

        PlayableGraph graph;
        bool graphAlive;
        AnimationClipPlayable playable;
        bool useGraph;

        static Transform Node(Transform parent, string name, float x, float y, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, z);
            return go.transform;
        }

        public static HumanoidRig Build()
        {
            var r = new HumanoidRig();
            r.root = new GameObject("TobeHumanoidBakeRig");
            r.root.hideFlags = HideFlags.HideAndDontSave;
            var map = new List<(Transform, HumanBodyBones)>();
            Transform t = r.root.transform;
            Transform hips = Node(t, "Hips", 0f, 1f, 0f); map.Add((hips, HumanBodyBones.Hips));
            Transform spine = Node(hips, "Spine", 0f, 0.10f, 0f); map.Add((spine, HumanBodyBones.Spine));
            Transform chest = Node(spine, "Chest", 0f, 0.15f, 0f); map.Add((chest, HumanBodyBones.Chest));
            Transform neck = Node(chest, "Neck", 0f, 0.20f, 0f); map.Add((neck, HumanBodyBones.Neck));
            Transform head = Node(neck, "Head", 0f, 0.10f, 0f); map.Add((head, HumanBodyBones.Head));
            for (int s = 0; s < 2; s++)
            {
                float sg = s == 0 ? -1f : 1f;       // left = -x for a character facing +z
                string p = s == 0 ? "Left" : "Right";
                Transform ul = Node(hips, p + "UpperLeg", 0.09f * sg, -0.05f, 0f); map.Add((ul, s == 0 ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg));
                Transform ll = Node(ul, p + "LowerLeg", 0f, -0.42f, 0f); map.Add((ll, s == 0 ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg));
                Transform ft = Node(ll, p + "Foot", 0f, -0.42f, 0f); map.Add((ft, s == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot));
                Transform to = Node(ft, p + "Toes", 0f, -0.07f, 0.12f); map.Add((to, s == 0 ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes));
                Transform sh = Node(chest, p + "Shoulder", 0.03f * sg, 0.15f, 0f); map.Add((sh, s == 0 ? HumanBodyBones.LeftShoulder : HumanBodyBones.RightShoulder));
                Transform ua = Node(sh, p + "UpperArm", 0.12f * sg, 0f, 0f); map.Add((ua, s == 0 ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm));
                Transform la = Node(ua, p + "LowerArm", 0.28f * sg, 0f, 0f); map.Add((la, s == 0 ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm));
                Transform hd = Node(la, p + "Hand", 0.25f * sg, 0f, 0f); map.Add((hd, s == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand));
            }
            r.hips = hips;
            r.avatar = HumanoidLoader.BuildHumanAvatarFromMap(r.root.transform, map);
            if (r.avatar == null || !r.avatar.isValid || !r.avatar.isHuman) { r.Dispose(); throw new Exception("bake rig avatar is not a valid humanoid"); }
            r.avatar.name = "TobeBakeAvatar";
            r.animator = r.root.AddComponent<Animator>();
            r.animator.avatar = r.avatar;
            r.animator.applyRootMotion = false;
            r.handler = new HumanPoseHandler(r.avatar, r.root.transform);
            return r;
        }

        /// <summary>Chooses how to sample this clip: AnimationClip.SampleAnimation first, a manually evaluated PlayableGraph if that leaves the rig still.</summary>
        public void Bind(AnimationClip clip)
        {
            ReleaseGraph();
            useGraph = false;
            if (clip.length < 0.1f) return;
            var a = new HumanPose { muscles = new float[MotionLibrary.MC] };
            var b = new HumanPose { muscles = new float[MotionLibrary.MC] };
            try
            {
                Sample(clip, 0f); handler.GetHumanPose(ref a);
                Sample(clip, clip.length * 0.5f); handler.GetHumanPose(ref b);
            }
            catch (Exception) { }
            if (Diff(a, b) > 1e-3f) return;
            useGraph = true;
            graph = PlayableGraph.Create("TobeBake");
            graphAlive = true;
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "bake", animator);
            playable = AnimationClipPlayable.Create(graph, clip);
            output.SetSourcePlayable(playable);
        }

        static float Diff(HumanPose a, HumanPose b)
        {
            float d = 0f;
            for (int i = 0; i < a.muscles.Length; i++) d += Mathf.Abs(a.muscles[i] - b.muscles[i]);
            return d;
        }

        public void Sample(AnimationClip clip, float t)
        {
            if (useGraph && graphAlive)
            {
                playable.SetTime(t);
                graph.Evaluate(0f);
            }
            else clip.SampleAnimation(root, t);
        }

        void ReleaseGraph()
        {
            if (graphAlive) { try { graph.Destroy(); } catch (Exception) { } graphAlive = false; }
        }

        public void Dispose()
        {
            ReleaseGraph();
            handler?.Dispose(); handler = null;
            if (root != null) Object.DestroyImmediate(root);
            if (avatar != null) Object.Destroy(avatar);
            root = null; avatar = null;
        }
    }
}
