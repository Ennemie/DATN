Shader "Custom/BlackFlame"
{
    Properties
    {
        _FlameA ("Flame A (Shape)", 2D) = "white" {}
        _FlameB ("Flame B (Distortion / Flow)", 2D) = "gray" {}
        _FlameC ("Flame C (Detail / Embers)", 2D) = "gray" {}

        _Tint ("Tint", Color) = (0.22, 0.22, 0.22, 1.0)

        _ShapeScale ("Shape Scale", Range(0.05, 5)) = 1.0
        _DistortionScale ("Distortion Scale", Range(0.05, 8)) = 2.0
        _DetailScale ("Detail Scale", Range(0.05, 16)) = 6.0

        _ShapeSpeed ("Shape Speed (XY)", Vector) = (0.00, 0.35, 0, 0)
        _DistortionSpeed ("Distortion Speed (XY)", Vector) = (0.00, 0.80, 0, 0)
        _DetailSpeed ("Detail Speed (XY)", Vector) = (0.00, 1.60, 0, 0)

        _WarpStrength ("Warp Strength", Range(0, 0.25)) = 0.07
        _DistortionStrength ("Distortion Strength", Range(0, 1)) = 0.35
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.18

        _Brightness ("Brightness", Range(0, 3)) = 1.0
        _Alpha ("Alpha", Range(0, 1)) = 0.85

        _EdgeContrast ("Edge Contrast", Range(0.5, 6)) = 2.2
        _EdgeSoftness ("Edge Softness", Range(0.001, 1)) = 0.18
        _EdgeBoost ("Edge Boost", Range(0, 2)) = 0.55

        _FlickerSpeed ("Flicker Speed", Range(0, 20)) = 6.0
        _FlickerAmount ("Flicker Amount", Range(0, 1)) = 0.22

        _ScrollJitter ("Scroll Jitter", Range(0, 1)) = 0.08
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            sampler2D _FlameA;
            sampler2D _FlameB;
            sampler2D _FlameC;

            float4 _FlameA_ST;
            float4 _FlameB_ST;
            float4 _FlameC_ST;

            fixed4 _Tint;

            float _ShapeScale;
            float _DistortionScale;
            float _DetailScale;

            float4 _ShapeSpeed;
            float4 _DistortionSpeed;
            float4 _DetailSpeed;

            float _WarpStrength;
            float _DistortionStrength;
            float _DetailStrength;

            float _Brightness;
            float _Alpha;

            float _EdgeContrast;
            float _EdgeSoftness;
            float _EdgeBoost;

            float _FlickerSpeed;
            float _FlickerAmount;

            float _ScrollJitter;
            float _AlphaCutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 worldUV : TEXCOORD1;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise2d(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float luminance(fixed4 c)
            {
                return dot(c.rgb, float3(0.299, 0.587, 0.114));
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldUV = mul(unity_ObjectToWorld, v.vertex).xz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 baseUV = i.uv;

                float flicker = 1.0 + (noise2d(baseUV * 9.0 + _Time.y * _FlickerSpeed) - 0.5) * 2.0 * _FlickerAmount;
                float jitter = (noise2d(baseUV * 4.0 + _Time.y * (_FlickerSpeed * 0.35)) - 0.5) * _ScrollJitter;

                float2 shapeUV = baseUV * _ShapeScale;
                shapeUV += float2(_ShapeSpeed.x, _ShapeSpeed.y) * _Time.y;
                shapeUV += float2(jitter, jitter * 0.6);

                float2 distUV = baseUV * _DistortionScale;
                distUV += float2(_DistortionSpeed.x, _DistortionSpeed.y) * _Time.y;
                distUV += float2(-jitter * 0.9, jitter * 1.1);

                float2 detailUV = baseUV * _DetailScale;
                detailUV += float2(_DetailSpeed.x, _DetailSpeed.y) * _Time.y;
                detailUV += float2(jitter * 1.3, -jitter * 0.8);

                shapeUV = TRANSFORM_TEX(shapeUV, _FlameA);
                distUV = TRANSFORM_TEX(distUV, _FlameB);
                detailUV = TRANSFORM_TEX(detailUV, _FlameC);

                fixed4 shapeSample = tex2D(_FlameA, shapeUV);
                fixed4 distSample  = tex2D(_FlameB, distUV);
                fixed4 detailSample = tex2D(_FlameC, detailUV);

                float shapeMask = max(shapeSample.a, luminance(shapeSample));
                float distMask = max(distSample.a, luminance(distSample));
                float detailMask = max(detailSample.a, luminance(detailSample));

                float2 warpFromDist = (distSample.rg - 0.5) * 2.0;
                float2 warpFromDetail = (detailSample.rg - 0.5) * 2.0;

                float2 warpedUV = shapeUV;
                warpedUV += warpFromDist * _WarpStrength * _DistortionStrength;
                warpedUV += warpFromDetail * _WarpStrength * _DetailStrength;

                fixed4 warpedShape = tex2D(_FlameA, warpedUV);
                float warpedShapeMask = max(warpedShape.a, luminance(warpedShape));

                float alphaShape = smoothstep(_AlphaCutoff, 1.0, warpedShapeMask);
                float alphaDist = lerp(0.75, 1.0, distMask);
                float alphaDetail = lerp(0.85, 1.0, detailMask);

                float edge = pow(saturate(warpedShapeMask), _EdgeContrast);
                edge = smoothstep(_EdgeSoftness, 1.0, edge);
                edge = saturate(edge + (warpedShapeMask * _EdgeBoost));

                float ember = saturate(detailMask * 1.35 + distMask * 0.45);

                float intensity = edge;
                intensity *= alphaDist;
                intensity *= alphaDetail;
                intensity *= flicker;
                intensity = saturate(intensity);

                float3 baseDark = _Tint.rgb;
                float3 emberTint = lerp(baseDark, float3(0.95, 0.95, 0.95), ember * 0.35);
                float3 finalRgb = emberTint * intensity * _Brightness;

                float finalAlpha = alphaShape * _Alpha;
                finalAlpha *= lerp(0.72, 1.0, intensity);
                finalAlpha *= saturate(0.85 + ember * 0.25);

                return fixed4(finalRgb, finalAlpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
