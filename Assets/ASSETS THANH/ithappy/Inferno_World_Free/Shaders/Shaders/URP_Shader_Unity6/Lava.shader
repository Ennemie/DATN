
Shader "Custom/Lava"
{
    Properties
    {
        // --- Original graph-compatible properties ---
        _Mask_ST("Mask_ST", Vector) = (1, 1, 0, 0)
        _Mask1_ST("Mask1_ST", Vector) = (0, 0, 0, 0)

        _Color_A("Color_A", Color) = (0.90, 0.20, 0.05, 1)
        _Color_B("Color_B", Color) = (0.95, 0.45, 0.10, 1)
        _Color_C("Color_C", Color) = (1.00, 0.75, 0.20, 1)

        // Use XYZ like the generated graph code did.
        _ColorGradient("ColorGradient", Vector) = (0, 0.24, 0.47, 0)
        _Gradient("Gradient", Vector) = (0, 0.44, 0, 0)

        _Height("Height", Float) = 0.10

        [NoScaleOffset]_Noise_0("Noise_0", 2D) = "white" {}
        [NoScaleOffset]_Noise_1("Noise_1", 2D) = "white" {}

        // --- Extra built-in enhancements ---
        _VertexNoiseStrength("Vertex Noise Strength", Range(0, 2)) = 1
        _FragmentNoiseStrength("Fragment Noise Strength", Range(0, 2)) = 1
        _NoiseContrast("Noise Contrast", Range(0.1, 4)) = 1
        _EmissionStrength("Emission Strength", Range(0, 4)) = 0.20
        _EdgeGlow("Edge Glow", Range(0, 2)) = 0.25
        _UVScrollMix("UV Scroll Mix", Range(0, 1)) = 0.50
        _DetailWarp("Detail Warp", Range(0, 1)) = 0.20
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "IgnoreProjector"="True"
            "DisableBatching"="False"
        }

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="Always" }

            Cull Back
            ZTest LEqual
            ZWrite On
            Blend One Zero

            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            sampler2D _Noise_0;
            sampler2D _Noise_1;

            float4 _Mask_ST;
            float4 _Mask1_ST;

            float4 _Color_A;
            float4 _Color_B;
            float4 _Color_C;

            float4 _ColorGradient;
            float4 _Gradient;
            float _Height;

            float _VertexNoiseStrength;
            float _FragmentNoiseStrength;
            float _NoiseContrast;
            float _EmissionStrength;
            float _EdgeGlow;
            float _UVScrollMix;
            float _DetailWarp;

            struct appdata
            {
                float4 vertex   : POSITION;
                float3 normal   : NORMAL;
                float4 tangent  : TANGENT;
                float2 uv       : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float2 uv       : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ------------------------------------------------------------
            // Utility helpers
            // ------------------------------------------------------------

            float2 GetWorldXZ(float3 worldPos)
            {
                return worldPos.xz;
            }

            float2 ApplyMaskST(float2 worldXZ, float4 st)
            {
                return worldXZ * st.xy + (_Time.y * st.zw);
            }

            float Remap01(float value, float2 minMax)
            {
                float denom = max(1e-5, minMax.y - minMax.x);
                return saturate((value - minMax.x) / denom);
            }

            float Contrast01(float value, float contrast)
            {
                // contrast = 1 => unchanged
                // contrast > 1 => sharper
                value = saturate(value);
                return saturate((value - 0.5) * contrast + 0.5);
            }

            float SampleNoiseLOD(sampler2D tex, float2 uv)
            {
                // Vertex texture sampling must use tex2Dlod in Built-in.
                return tex2Dlod(tex, float4(uv, 0.0, 0.0)).r;
            }

            float SampleNoise(sampler2D tex, float2 uv)
            {
                return tex2D(tex, uv).r;
            }

            float CombinedNoiseVertex(float2 worldXZ)
            {
                float2 uv0 = ApplyMaskST(worldXZ, _Mask_ST);
                float2 uv1 = ApplyMaskST(worldXZ, _Mask1_ST);

                // First pass noise
                float n0 = SampleNoiseLOD(_Noise_0, uv0);
                float n1 = SampleNoiseLOD(_Noise_1, uv1);

                // Soft merge like the graph's add * 0.5
                float n = (n0 + n1) * 0.5;

                // Optional extra shaping for stronger lava flow.
                n = Contrast01(n, _NoiseContrast);
                return n;
            }

            float CombinedNoiseFragment(float2 worldXZ)
            {
                float2 uv0 = ApplyMaskST(worldXZ, _Mask_ST);
                float2 uv1 = ApplyMaskST(worldXZ, _Mask1_ST);

                float n0 = SampleNoise(_Noise_0, uv0);
                float n1 = SampleNoise(_Noise_1, uv1);

                float n = (n0 + n1) * 0.5;
                n = Contrast01(n, _NoiseContrast);
                return n;
            }

            float3 BuildLavaColor(float n)
            {
                // Preserve the graph's idea:
                // - first blend A -> B
                // - then blend -> C
                // - use ColorGradient thresholds
                float t0 = _ColorGradient.x;
                float t1 = _ColorGradient.y;
                float t2 = _ColorGradient.z;

                float ab = smoothstep(t0, t1, n);
                float bc = smoothstep(t1, t2, n);

                // Graph-like emphasis: middle band gets squared a bit.
                float4 col = lerp(_Color_A, _Color_B, ab * ab);
                col = lerp(col, _Color_C, bc);

                // Subtle emissive lift.
                col.rgb += col.rgb * (_EmissionStrength * 0.35);

                return col.rgb;
            }

            float3 BuildLavaColorEnhanced(float n, float2 worldXZ)
            {
                // Extra enhancement: slight warp from a second noise sample,
                // but keep the output familiar to the original graph.
                float2 warpUV = ApplyMaskST(worldXZ, _Mask1_ST);
                float warp = SampleNoise(_Noise_1, warpUV);
                float wobble = lerp(1.0, warp, _DetailWarp);

                n = saturate(n * wobble);

                float3 baseCol = BuildLavaColor(n);

                // Glow on brighter areas
                float glow = smoothstep(_ColorGradient.y, _ColorGradient.z, n);
                baseCol += glow * _EdgeGlow * baseCol;

                return baseCol;
            }

            // ------------------------------------------------------------
            // Vertex
            // ------------------------------------------------------------

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float2 worldXZ = GetWorldXZ(worldPos);

                float n = CombinedNoiseVertex(worldXZ);

                // Original graph behavior:
                // smoothstep(_Gradient.x, _Gradient.y, noise) * Height
                float displacementMask = smoothstep(_Gradient.x, _Gradient.y, n);
                displacementMask *= _VertexNoiseStrength;

                // Keep it lava-like: lift only in Y.
                worldPos.y += displacementMask * _Height;

                o.worldPos = worldPos;
                o.uv = v.uv;
                o.pos = UnityWorldToClipPos(worldPos);
                return o;
            }

            // ------------------------------------------------------------
            // Fragment
            // ------------------------------------------------------------

            fixed4 frag(v2f i) : SV_Target
            {
                float2 worldXZ = GetWorldXZ(i.worldPos);

                float n = CombinedNoiseFragment(worldXZ);

                // A slightly different shaping on the fragment side to create flow variation.
                float flow = smoothstep(_Gradient.x, _Gradient.y, n);
                flow *= _FragmentNoiseStrength;

                // Extra animate brightness pulse, very subtle.
                float pulse = 0.5 + 0.5 * sin(_Time.y * 2.2 + n * 6.2831853);

                float3 col = BuildLavaColorEnhanced(n, worldXZ);

                // Push the upper bands a little hotter.
                col += col * (flow * _EmissionStrength * 0.5);
                col += pulse * _EdgeGlow * 0.03;

                return fixed4(saturate(col), 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
