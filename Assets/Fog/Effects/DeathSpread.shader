Shader "UI/DeathSpread"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}

        _LargeCloudTex ("Large Cloud Noise", 2D) = "gray" {}
        _FineDetailTex ("Fine Detail Noise", 2D) = "gray" {}

        _DeathColor ("Death Color", Color) = (0, 0, 0, 1)

        _Spread ("Spread", Range(0, 1)) = 0

        _CloudScale ("Cloud Scale", Range(0.1, 10)) = 1.2
        _DetailScale ("Detail Scale", Range(0.1, 20)) = 10.0

        _CloudSpeed ("Cloud Speed", Vector) = (0.008, 0.004, 0, 0)
        _DetailSpeed ("Detail Speed", Vector) = (-0.012, 0.008, 0, 0)

        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.30
        _DistortionStrength ("Distortion Strength", Range(0, 1)) = 0.20

        _NoiseInfluence ("Boundary Noise", Range(0, 0.5)) = 0.18

        _CoreSoftness ("Core Edge Softness", Range(0.001, 0.15)) = 0.025

        _FringeWidth ("Fringe Width", Range(0.001, 0.5)) = 0.14
        _FringeOpacity ("Fringe Opacity", Range(0, 1)) = 0.65
        _FringeNoise ("Fringe Noise", Range(0, 1)) = 0.65

        _AspectCorrection ("Aspect Correction", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always

        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGBA

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _LargeCloudTex;
            sampler2D _FineDetailTex;

            fixed4 _DeathColor;

            float _Spread;

            float _CloudScale;
            float _DetailScale;

            float4 _CloudSpeed;
            float4 _DetailSpeed;

            float _DetailStrength;
            float _DistortionStrength;

            float _NoiseInfluence;

            float _CoreSoftness;

            float _FringeWidth;
            float _FringeOpacity;
            float _FringeNoise;

            float _AspectCorrection;

            v2f vert(appdata_t v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color;

                return o;
            }

            float SampleLargeCloud(float2 uv)
            {
                float2 cloudUV = uv * _CloudScale;
                cloudUV += _Time.y * _CloudSpeed.xy;

                return tex2D(_LargeCloudTex, cloudUV).r;
            }

            float SampleFineDetail(float2 uv)
            {
                float2 detailUV = uv * _DetailScale;
                detailUV += _Time.y * _DetailSpeed.xy;

                return tex2D(_FineDetailTex, detailUV).r;
            }

            float GetNormalizedRadialDistance(float2 uv)
            {
                float2 centered = uv - 0.5;

                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);

                float2 corrected = centered;

                corrected.x =
                    lerp(
                        corrected.x,
                        corrected.x * aspect,
                        _AspectCorrection
                    );

                float distanceFromCenter = length(corrected);

                float2 corner =
                    float2(
                        0.5 * lerp(1.0, aspect, _AspectCorrection),
                        0.5
                    );

                float maxRadius = max(length(corner), 0.0001);

                return distanceFromCenter / maxRadius;
            }

            float Saturate01(float value)
            {
                return saturate(value);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // --------------------------------------------------
                // 1. SAMPLE LARGE CLOUD
                // --------------------------------------------------

                float cloud = SampleLargeCloud(uv);

                // --------------------------------------------------
                // 2. USE LARGE CLOUD TO DISTORT FINE DETAIL
                // --------------------------------------------------

                float2 distortion;

                distortion.x =
                    (cloud - 0.5) *
                    _DistortionStrength;

                distortion.y =
                    (cloud - 0.5) *
                    _DistortionStrength;

                float fineDetail =
                    SampleFineDetail(uv + distortion);

                // --------------------------------------------------
                // 3. COMBINE NOISE
                // --------------------------------------------------

                float combinedNoise =
                    lerp(
                        cloud,
                        fineDetail,
                        _DetailStrength
                    );

                combinedNoise =
                    Saturate01(combinedNoise);

                // --------------------------------------------------
                // 4. RADIAL DISTANCE
                //
                // 0 = center
                // 1 = farthest screen corner
                // --------------------------------------------------

                float radialDistance =
                    GetNormalizedRadialDistance(uv);

                // --------------------------------------------------
                // 5. ORGANIC BOUNDARY
                //
                // Spread = 0:
                //      almost no black
                //
                // Spread = 1:
                //      everything is black
                //
                // Large cloud controls the big shapes.
                // Fine detail contributes small irregularities.
                // --------------------------------------------------

                float visibleRadius =
                    1.0 - _Spread;

                float boundaryNoise =
                    (combinedNoise - 0.5) *
                    _NoiseInfluence;

                float boundary =
                    visibleRadius +
                    boundaryNoise;

                // --------------------------------------------------
                // 6. CORE MASK
                //
                // Everything clearly outside the boundary becomes
                // fully opaque black.
                // --------------------------------------------------

                float coreMask =
                    smoothstep(
                        boundary - _CoreSoftness,
                        boundary + _CoreSoftness,
                        radialDistance
                    );

                // --------------------------------------------------
                // 7. FRINGE BAND
                //
                // The fringe exists immediately INSIDE the organic
                // boundary. It is semi-transparent so the game can
                // still be seen through it.
                // --------------------------------------------------

                float fringeStart =
                    boundary - _FringeWidth;

                float fringeMask =
                    1.0 -
                    smoothstep(
                        fringeStart,
                        boundary,
                        radialDistance
                    );

                // --------------------------------------------------
                // 8. BREAK THE FRINGE USING FINE DETAIL
                //
                // This creates transparent gaps and uneven patches
                // instead of one smooth circular haze.
                // --------------------------------------------------

                float fringeNoise =
                    lerp(
                        0.35,
                        1.0,
                        fineDetail
                    );

                fringeNoise =
                    lerp(
                        1.0,
                        fringeNoise,
                        _FringeNoise
                    );

                float fringeAlpha =
                    fringeMask *
                    fringeNoise *
                    _FringeOpacity;

                // --------------------------------------------------
                // 9. COMBINE CORE + FRINGE
                //
                // Core always wins and stays opaque.
                // Fringe fills the area just inside the boundary.
                // --------------------------------------------------

                float finalAlpha =
                    max(
                        coreMask,
                        fringeAlpha * (1.0 - coreMask)
                    );

                // --------------------------------------------------
                // 10. ZERO-SPREAD SAFETY
                // --------------------------------------------------

                float startFade =
                    smoothstep(
                        0.0,
                        0.02,
                        _Spread
                    );

                finalAlpha *= startFade;

                // --------------------------------------------------
                // 11. MAX-SPREAD GUARANTEE
                //
                // At 1.0 the entire screen MUST be black.
                // No noise or fringe can leave a hole.
                // --------------------------------------------------

                if (_Spread >= 0.999)
                {
                    finalAlpha = 1.0;
                }

                // --------------------------------------------------
                // 12. OUTPUT
                // --------------------------------------------------

                fixed4 finalColor =
                    _DeathColor;

                finalColor.a =
                    saturate(
                        finalAlpha *
                        _DeathColor.a *
                        i.color.a
                    );

                return finalColor;
            }

            ENDCG
        }
    }
}