// STUB (compile-check only): subset of the Volume framework (com.unity.render-pipelines.core) and the
// URP post-processing overrides (com.unity.render-pipelines.universal). Not functional.
using System;
using UnityEngine;

namespace UnityEngine.Rendering
{
    public abstract class VolumeParameter { public bool overrideState; }
    public class VolumeParameter<T> : VolumeParameter
    {
        public T value;
        public VolumeParameter() { }
        public VolumeParameter(T value, bool overrideState = false) { this.value = value; this.overrideState = overrideState; }
        public void Override(T x) { value = x; overrideState = true; }
        public static implicit operator T(VolumeParameter<T> p) => p.value;
    }
    public class FloatParameter : VolumeParameter<float> { public FloatParameter(float value, bool overrideState = false) : base(value, overrideState) { } }
    public class MinFloatParameter : FloatParameter { public float min; public MinFloatParameter(float value, float min, bool overrideState = false) : base(value, overrideState) { this.min = min; } }
    public class ClampedFloatParameter : FloatParameter { public float min, max; public ClampedFloatParameter(float value, float min, float max, bool overrideState = false) : base(value, overrideState) { } }
    public class IntParameter : VolumeParameter<int> { public IntParameter(int value, bool overrideState = false) : base(value, overrideState) { } }
    public class ClampedIntParameter : IntParameter { public ClampedIntParameter(int value, int min, int max, bool overrideState = false) : base(value, overrideState) { } }
    public class BoolParameter : VolumeParameter<bool> { public BoolParameter(bool value, bool overrideState = false) : base(value, overrideState) { } }
    public class ColorParameter : VolumeParameter<Color> { public ColorParameter(Color value, bool hdr = false, bool showAlpha = true, bool showEyeDropper = true, bool overrideState = false) : base(value, overrideState) { } }
    public class Vector2Parameter : VolumeParameter<Vector2> { public Vector2Parameter(Vector2 value, bool overrideState = false) : base(value, overrideState) { } }
    public class Vector3Parameter : VolumeParameter<Vector3> { public Vector3Parameter(Vector3 value, bool overrideState = false) : base(value, overrideState) { } }
    public class Vector4Parameter : VolumeParameter<Vector4> { public Vector4Parameter(Vector4 value, bool overrideState = false) : base(value, overrideState) { } }
    public class TextureParameter : VolumeParameter<Texture> { public TextureParameter(Texture value, bool overrideState = false) : base(value, overrideState) { } }
    public class NoInterpClampedFloatParameter : ClampedFloatParameter { public NoInterpClampedFloatParameter(float value, float min, float max, bool overrideState = false) : base(value, min, max, overrideState) { } }

    public interface IPostProcessComponent { bool IsActive(); }
    public abstract class VolumeComponent : ScriptableObject { public bool active = true; public void SetAllOverridesTo(bool state) { } }
    public sealed class VolumeProfile : ScriptableObject
    {
        public System.Collections.Generic.List<VolumeComponent> components = new System.Collections.Generic.List<VolumeComponent>();
        public T Add<T>(bool overrides = false) where T : VolumeComponent, new() => null;
        public VolumeComponent Add(Type type, bool overrides = false) => null;
        public void Remove<T>() where T : VolumeComponent { }
        public bool Has<T>() where T : VolumeComponent => false;
        public bool TryGet<T>(out T component) where T : VolumeComponent { component = null; return false; }
    }
    public class Volume : MonoBehaviour
    {
        public bool isGlobal = true; public float priority; public float blendDistance; [Range(0f, 1f)] public float weight = 1f;
        public VolumeProfile sharedProfile; public VolumeProfile profile;
    }
}

namespace UnityEngine.Rendering.Universal
{
    public enum TonemappingMode { None, Neutral, ACES }
    public sealed class TonemappingModeParameter : VolumeParameter<TonemappingMode> { public TonemappingModeParameter(TonemappingMode value, bool overrideState = false) : base(value, overrideState) { } }
    public enum BloomDownscaleMode { Half, Quarter }
    public sealed class Bloom : VolumeComponent, IPostProcessComponent
    {
        public MinFloatParameter threshold = new MinFloatParameter(0.9f, 0f); public MinFloatParameter intensity = new MinFloatParameter(0f, 0f);
        public ClampedFloatParameter scatter = new ClampedFloatParameter(0.7f, 0f, 1f); public MinFloatParameter clamp = new MinFloatParameter(65472f, 0f);
        public ColorParameter tint = new ColorParameter(Color.white); public BoolParameter highQualityFiltering = new BoolParameter(false);
        public ClampedIntParameter skipIterations = new ClampedIntParameter(1, 0, 16); public TextureParameter dirtTexture = new TextureParameter(null);
        public FloatParameter dirtIntensity = new FloatParameter(0f);
        public bool IsActive() => true;
    }
    public sealed class Tonemapping : VolumeComponent, IPostProcessComponent { public TonemappingModeParameter mode = new TonemappingModeParameter(TonemappingMode.None); public bool IsActive() => true; }
    public sealed class ColorAdjustments : VolumeComponent, IPostProcessComponent
    {
        public FloatParameter postExposure = new FloatParameter(0f); public ClampedFloatParameter contrast = new ClampedFloatParameter(0f, -100f, 100f);
        public ColorParameter colorFilter = new ColorParameter(Color.white); public ClampedFloatParameter hueShift = new ClampedFloatParameter(0f, -180f, 180f);
        public ClampedFloatParameter saturation = new ClampedFloatParameter(0f, -100f, 100f);
        public bool IsActive() => true;
    }
    public sealed class Vignette : VolumeComponent, IPostProcessComponent
    {
        public ColorParameter color = new ColorParameter(Color.black); public Vector2Parameter center = new Vector2Parameter(new Vector2(0.5f, 0.5f));
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f); public ClampedFloatParameter smoothness = new ClampedFloatParameter(0.2f, 0.01f, 1f);
        public BoolParameter rounded = new BoolParameter(false);
        public bool IsActive() => true;
    }
    public sealed class DepthOfField : VolumeComponent, IPostProcessComponent { public MinFloatParameter focusDistance = new MinFloatParameter(10f, 0.1f); public ClampedFloatParameter aperture = new ClampedFloatParameter(5.6f, 1f, 32f); public ClampedFloatParameter focalLength = new ClampedFloatParameter(50f, 1f, 300f); public bool IsActive() => true; }
    public sealed class MotionBlur : VolumeComponent, IPostProcessComponent { public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f); public bool IsActive() => true; }
    public sealed class ChromaticAberration : VolumeComponent, IPostProcessComponent { public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f); public bool IsActive() => true; }
    public sealed class FilmGrain : VolumeComponent, IPostProcessComponent { public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f); public bool IsActive() => true; }
    public sealed class WhiteBalance : VolumeComponent, IPostProcessComponent { public ClampedFloatParameter temperature = new ClampedFloatParameter(0f, -100f, 100f); public ClampedFloatParameter tint = new ClampedFloatParameter(0f, -100f, 100f); public bool IsActive() => true; }
    public class UniversalAdditionalCameraData : MonoBehaviour { public bool renderPostProcessing; public bool renderShadows = true; }
    public class UniversalAdditionalLightData : MonoBehaviour { }
}
