// Material / procedural texture / primitive helpers for the presentation layer (URP).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VRM10.MToon10;

namespace Tobe.View
{
    public static class Mats
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();
        static Shader litSh, unlitSh, partSh, mtoonSh;
        public const string MToonShaderName = "VRM10/Universal Render Pipeline/MToon10";
        static Mesh quadMesh;
        static Font jpFont;

        static Shader Sh(ref Shader s, string name)
        {
            if (s == null) s = Shader.Find(name);
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Hidden/InternalErrorShader");
            return s;
        }
        static Shader LitSh => Sh(ref litSh, "Universal Render Pipeline/Lit");
        static Shader UnlitSh => Sh(ref unlitSh, "Universal Render Pipeline/Unlit");
        static Shader PartSh => Sh(ref partSh, "Universal Render Pipeline/Particles/Unlit");
        /// <summary>The VRM MToon10 URP shader, or null when it is not available (then Toon() falls back to URP Lit).</summary>
        public static Shader MToonSh
        {
            get
            {
                if (mtoonSh == null) mtoonSh = Shader.Find(MToonShaderName);
                return mtoonSh;
            }
        }

        static bool TryGet(string key, out Material m)
        {
            if (key != null && cache.TryGetValue(key, out m) && m != null) return true;
            m = null; return false;
        }

        // ----------------------------------------------------------------- materials
        public static Material Lit(string key, Color color, float smoothness = 0.3f, float metallic = 0f,
            Color? emission = null, Texture tex = null, bool transparent = false, bool doubleSided = false)
        {
            if (TryGet(key, out var m)) return m;
            m = new Material(LitSh) { name = key };
            m.SetColor("_BaseColor", color);
            m.color = color;
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (tex != null) { m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            if (transparent) SetTransparent(m, false);
            if (doubleSided) m.SetFloat("_Cull", 0f);
            if (key != null) cache[key] = m;
            return m;
        }

        public static Material Unlit(string key, Color color, Texture tex = null, bool transparent = false,
            bool additive = false, bool doubleSided = false)
        {
            if (TryGet(key, out var m)) return m;
            m = new Material(UnlitSh) { name = key };
            m.SetColor("_BaseColor", color);
            m.color = color;
            if (tex != null) { m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
            if (transparent || additive) SetTransparent(m, additive);
            if (doubleSided) m.SetFloat("_Cull", 0f);
            if (key != null) cache[key] = m;
            return m;
        }

        public static Material Particle(string key, Texture tex, bool additive)
        {
            if (TryGet(key, out var m)) return m;
            m = new Material(PartSh) { name = key };
            m.SetColor("_BaseColor", Color.white);
            if (tex != null) { m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
            SetTransparent(m, additive);
            m.SetFloat("_Cull", 0f);
            if (key != null) cache[key] = m;
            return m;
        }


        // ----------------------------------------------------------------- toon (MToon10)
        public struct ToonOpts
        {
            public Color? shade;            // shade color (default: base * shadeMul)
            public float shadeMul;          // 0 -> 0.72
            public float shift;             // shading shift (default -0.05): higher = more lit area
            public float toony;             // 0..1 (default 0.9)
            public Color? rim;              // parametric rim color
            public float rimPower;          // default 4
            public Color? emission;         // linear HDR emission
            public Texture tex;
            public Texture shadeTex;
            public bool doubleSided;
            public bool transparent;        // alpha blend
            public Texture matcap; public Color? matcapColor;
            public float outlineWidth;      // meters (world), 0 = none
            public Color? outlineColor;
            public float gi;                // GI equalization, default 0.85
        }

        /// <summary>Cel-shaded material (VRM MToon10). Falls back to URP Lit when the shader is missing.</summary>
        public static Material Toon(string key, Color color, ToonOpts o = default)
        {
            if (TryGet(key, out var m)) return m;
            var sh = MToonSh;
            if (sh == null)
            {
                m = Lit(null, color, 0.15f, 0f, o.emission, o.tex, o.transparent, o.doubleSided);
                m.name = key ?? "toon";
                if (key != null) cache[key] = m;
                return m;
            }
            m = new Material(sh) { name = key ?? "toon" };
            ApplyToon(m, color, o);
            if (key != null) cache[key] = m;
            return m;
        }

        /// <summary>(Re)configures an MToon10 material.</summary>
        public static void ApplyToon(Material m, Color color, ToonOpts o)
        {
            var c = new MToon10Context(m);
            c.AlphaMode = o.transparent ? MToon10AlphaMode.Transparent : MToon10AlphaMode.Opaque;
            c.TransparentWithZWriteMode = MToon10TransparentWithZWriteMode.Off;
            c.DoubleSidedMode = o.doubleSided ? MToon10DoubleSidedMode.On : MToon10DoubleSidedMode.Off;
            c.BaseColorFactorSrgb = color;
            float mul = o.shadeMul > 0f ? o.shadeMul : 0.72f;
            c.ShadeColorFactorSrgb = o.shade ?? new Color(color.r * mul, color.g * mul * 0.97f, Mathf.Min(1f, color.b * mul * 1.06f), color.a);
            if (o.tex != null) { c.BaseColorTexture = o.tex; c.ShadeColorTexture = o.shadeTex != null ? o.shadeTex : o.tex; }
            c.ShadingShiftFactor = o.shift != 0f ? o.shift : -0.05f;
            c.ShadingToonyFactor = o.toony > 0f ? o.toony : 0.9f;
            c.GiEqualizationFactor = o.gi > 0f ? o.gi : 0.85f;
            c.ParametricRimColorFactorSrgb = o.rim ?? Color.black;
            c.ParametricRimFresnelPowerFactor = o.rimPower > 0f ? o.rimPower : 4f;
            c.ParametricRimLiftFactor = 0.05f;
            c.RimLightingMixFactor = 0.6f;
            if (o.matcap != null) { c.MatcapTexture = o.matcap; c.MatcapColorFactorSrgb = o.matcapColor ?? Color.white; }
            if (o.emission.HasValue) c.EmissiveFactorLinear = o.emission.Value;
            if (o.outlineWidth > 0f)
            {
                c.OutlineWidthMode = MToon10OutlineMode.World;
                c.OutlineWidthFactor = o.outlineWidth;
                c.OutlineColorFactorSrgb = o.outlineColor ?? new Color(color.r * 0.3f, color.g * 0.28f, color.b * 0.35f, 1f);
                c.OutlineLightingMixFactor = 0.5f;
            }
            else c.OutlineWidthMode = MToon10OutlineMode.None;
            c.Validate();
        }

        /// <summary>True when the MToon shader is present (outline renderer feature still has to be on the renderer).</summary>
        public static bool IsToon(Material m) => m != null && m.shader != null && m.shader.name == MToonShaderName;

        /// <summary>Shorthand for flat environment surfaces.</summary>
        public static Material Toon(string key, Color color, Color shade, bool doubleSided = false, Texture tex = null, float shift = 0f)
            => Toon(key, color, new ToonOpts { shade = shade, doubleSided = doubleSided, tex = tex, shift = shift });

        /// <summary>Additive unlit material that multiplies by vertex colors (light shafts, glows).</summary>
        public static Material AdditiveVertex(string key, Texture tex = null)
            => Particle(key, tex != null ? tex : SquareTex, true);

        /// <summary>Alpha blended unlit vertex color material (soft decals).</summary>
        public static Material AlphaVertex(string key, Texture tex = null)
            => Particle(key, tex != null ? tex : SquareTex, false);

        /// <summary>Resources.Load wrapper (null when missing).</summary>
        public static T Res<T>(string path) where T : UnityEngine.Object
        {
            try { return Resources.Load<T>(path); } catch (Exception) { return null; }
        }

        /// <summary>Opaque unlit material that multiplies by mesh vertex colors (crowd etc.).</summary>
        public static Material VertexColorOpaque(string key, Color tint)
        {
            if (TryGet(key, out var m)) return m;
            m = new Material(PartSh) { name = key };
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Cull", 0f);
            if (key != null) cache[key] = m;
            return m;
        }

        /// <summary>Unique copy (for materials whose color/alpha is animated per object).</summary>
        public static Material Instance(Material src) => new Material(src);

        public static void SetColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static void SetTransparent(Material m, bool additive)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        // ----------------------------------------------------------------- textures
        public static Texture2D MakeTex(string key, int w, int h, Func<float, float, Color> f,
            bool mips = true, TextureWrapMode wrap = TextureWrapMode.Clamp, FilterMode filter = FilterMode.Bilinear, bool linear = false)
        {
            if (key != null && texCache.TryGetValue(key, out var t) && t != null) return t;
            t = new Texture2D(w, h, TextureFormat.RGBA32, mips, linear) { name = key ?? "proc", wrapMode = wrap, filterMode = filter };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
            t.SetPixels32(px);
            t.Apply(mips, false);
            if (key != null) texCache[key] = t;
            return t;
        }

        public static Texture2D SoftDot => MakeTex("softdot", 64, 64, (u, v) =>
        {
            float r = Mathf.Sqrt((u - .5f) * (u - .5f) + (v - .5f) * (v - .5f)) * 2f;
            float a = Mathf.Clamp01(1f - r); a *= a;
            return new Color(1, 1, 1, a);
        });

        public static Texture2D RingTex => MakeTex("ring", 256, 256, (u, v) =>
        {
            float r = Mathf.Sqrt((u - .5f) * (u - .5f) + (v - .5f) * (v - .5f)) * 2f;
            float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.88f) / 0.07f);
            a = Mathf.Max(a, Mathf.Clamp01(1f - Mathf.Abs(r - 0.74f) / 0.18f) * 0.18f);
            if (r > 1f) a = 0;
            return new Color(1, 1, 1, a);
        });

        public static Texture2D DiscTex => MakeTex("disc", 64, 64, (u, v) =>
        {
            float r = Mathf.Sqrt((u - .5f) * (u - .5f) + (v - .5f) * (v - .5f)) * 2f;
            return new Color(1, 1, 1, Mathf.Clamp01((1f - r) * 6f));
        });

        public static Texture2D StarTex => MakeTex("star", 64, 64, (u, v) =>
        {
            float x = Mathf.Abs(u - .5f) * 2f, y = Mathf.Abs(v - .5f) * 2f;
            float a = Mathf.Clamp01((1f - (Mathf.Sqrt(x) + Mathf.Sqrt(y))) * 3f);
            return new Color(1, 1, 1, a);
        });

        /// <summary>Stores a finished texture in the cache (used by generators that fill pixels themselves).</summary>
        public static void CacheTex(string key, Texture2D t) { if (key != null) texCache[key] = t; }
        public static bool TryGetTex(string key, out Texture2D t) => texCache.TryGetValue(key, out t) && t != null;

        public static Texture2D SquareTex => MakeTex("square", 8, 8, (u, v) => Color.white, false);

        public static Texture2D ChevronTex => MakeTex("chevron", 64, 64, (u, v) =>
        {
            float x = (u - .5f) * 2f, y = (v - .5f) * 2f;      // pointing down
            float w = (y + 0.8f) * 0.6f;
            float a = Mathf.Clamp01((w - Mathf.Abs(x)) * 14f) * Mathf.Clamp01((0.8f - y) * 14f) * Mathf.Clamp01((y + 0.8f) * 14f);
            return new Color(1, 1, 1, a);
        });

        public static Texture2D DashTex => MakeTex("dash", 16, 4, (u, v) =>
            new Color(1, 1, 1, u < 0.5f ? 1f : 0f), true, TextureWrapMode.Repeat, FilterMode.Bilinear);

        // ----------------------------------------------------------------- geometry
        public static Mesh QuadMesh
        {
            get
            {
                if (quadMesh != null) return quadMesh;
                // Both faces are present (front looks along -Z with normal -Z, back looks along +Z), so the quad is visible
                // from either side no matter what the material's culling is.
                quadMesh = new Mesh { name = "TobeQuad" };
                quadMesh.vertices = new[]
                {
                    new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0),
                    new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0),
                };
                quadMesh.normals = new[]
                {
                    -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward,
                    Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
                };
                quadMesh.uv = new[]
                {
                    new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
                    new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
                };
                // front (clockwise seen from -Z): BL, TL, TR ; back: reversed
                quadMesh.triangles = new[] { 0, 3, 2, 0, 2, 1, 4, 6, 7, 4, 5, 6 };
                quadMesh.RecalculateBounds();
                return quadMesh;
            }
        }

        /// <summary>Quad whose visible face looks along -Z of the object (like Unity's built-in Quad).</summary>
        public static GameObject Quad(string name, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material m, bool shadows = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = QuadMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = shadows;
            return go;
        }

        /// <summary>Flat quad lying on the floor, facing up.</summary>
        public static GameObject FloorQuad(string name, Transform parent, Vector3 pos, float sizeX, float sizeZ, Material m)
            => Quad(name, parent, pos, new Vector3(90, 0, 0), new Vector3(sizeX, sizeZ, 1), m, false);

        public static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material m, bool castShadow = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = castShadow;
            return go;
        }

        // ----------------------------------------------------------------- text
        public static Font JpFont
        {
            get
            {
                if (jpFont == null)
                {
                    try
                    {
                        jpFont = Font.CreateDynamicFontFromOSFont(
                            new[] { "Yu Gothic", "Meiryo", "MS Gothic", "Noto Sans CJK JP", "Arial Unicode MS", "Arial" }, 64);
                    }
                    catch (Exception) { jpFont = null; }
                    if (jpFont == null) jpFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return jpFont;
            }
        }

        public static TextMesh Text(Transform parent, string s, float charSize, int fontSize, Color c, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMesh>();
            var f = JpFont;
            tm.font = f;
            tm.text = s;
            tm.characterSize = charSize;
            tm.fontSize = fontSize;
            tm.color = c;
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;
            var r = go.GetComponent<MeshRenderer>();
            if (f != null) r.sharedMaterial = f.material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return tm;
        }
    }
}
