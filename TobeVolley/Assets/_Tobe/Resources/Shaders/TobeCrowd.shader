// Болельщики на трибунах: плоская аниме-фигурка, всегда развёрнута к камере (вокруг вертикали).
// Атлас: 8 колонок (причёски) x 3 строки (позы: руки вниз / одна рука вверх / обе вверх).
// Каналы атласа — маски: R футболка, G кожа, B волосы; A = непрозрачность (A=1 при нулевых масках = тёмный контур / глаза).
// Анимация целиком на видеокарте: прыжки и руки зависят от глобального «азарта» трибун _TobeExcite (CrowdAnimator).
Shader "Tobe/Crowd"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Tint;
            CBUFFER_END
            float _TobeExcite;

            struct Attributes
            {
                float3 pos : POSITION;    // точка опоры фигурки (у всех 4 вершин одна)
                float4 col : COLOR;       // цвет футболки
                float4 uv0 : TEXCOORD0;   // xy = угол квада (x -0.5..0.5, y 0..1), z = причёска 0..7, w = фаза 0..1
                float4 uv1 : TEXCOORD1;   // rgb = кожа, a = масштаб
                float4 uv2 : TEXCOORD2;   // rgb = волосы
            };
            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 shirt : TEXCOORD1;
                float3 skin : TEXCOORD2;
                float3 hair : TEXCOORD3;
                float shade : TEXCOORD4;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 c = TransformObjectToWorld(i.pos);
                float3 toCam = _WorldSpaceCameraPos - c; toCam.y = 0;
                float3 right = normalize(cross(toCam, float3(0, 1, 0)) + float3(1e-5, 0, 0));

                float phase = i.uv0.w;
                float e = saturate(_TobeExcite);
                float t = _Time.y * (1.6 + 4.5 * e) * (0.8 + 0.4 * frac(phase * 3.7)) + phase * 6.2832;
                float bounce = abs(sin(t)) * (0.012 + 0.12 * e);

                // поза: изредка кто-то машет рукой сам по себе; при азарте всё больше людей вскидывают руки
                float pose = 0;
                if (frac(phase * 13.1 + _Time.y * 0.07) < 0.06) pose = 1;
                if (e > 0.25 + 0.6 * frac(phase * 5.3)) pose = sin(t * 0.5) > 0 ? 2 : 1;

                float s = i.uv1.a;
                float3 w = c + right * (i.uv0.x * 0.9 * s) + float3(0, i.uv0.y * 0.95 * s + bounce, 0);
                o.pos = TransformWorldToHClip(w);
                o.uv = float2((i.uv0.z + i.uv0.x + 0.5) / 8.0, (pose + i.uv0.y) / 3.0);
                o.shirt = i.col.rgb; o.skin = i.uv1.rgb; o.hair = i.uv2.rgb;
                o.shade = lerp(0.7, 1.05, i.uv0.y);   // ниже к сиденью темнее
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 m = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(m.a - 0.5);
                float sum = m.r + m.g + m.b;
                float3 col = (m.r * i.shirt + m.g * i.skin + m.b * i.hair) / max(sum, 1e-3);
                col = lerp(float3(0.09, 0.08, 0.13), col, saturate(sum * 1.5));
                return half4(col * i.shade * _Tint.rgb, 1);
            }
            ENDHLSL
        }
    }
}
