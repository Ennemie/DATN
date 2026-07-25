Shader "Custom/FogScroll"
{
    Properties
    {
        _CloudTex ("Cloud Large", 2D) = "white" {}
        _FlowTex ("Flow Streaks", 2D) = "white" {}
        _DetailTex ("Fine Detail", 2D) = "white" {}

        _Tint ("Tint", Color) = (0.55,0.55,0.55,0.65)

        _CloudScale ("Cloud Scale", Range(0.05, 2)) = 0.18
        _FlowScale ("Flow Scale", Range(0.05, 5)) = 1.0
        _DetailScale ("Detail Scale", Range(0.05, 8)) = 2.5

        _CloudSpeed ("Cloud Speed", Vector) = (0.001, 0.0007, 0, 0)
        _FlowSpeed ("Flow Speed", Vector) = (-0.0015, 0.001, 0, 0)
        _DetailSpeed ("Detail Speed", Vector) = (0.0008, -0.0012, 0, 0)

        _WarpStrength ("Warp Strength", Range(0, 0.25)) = 0.05

        _CloudWeight ("Cloud Weight", Range(0, 1)) = 0.75
        _FlowWeight ("Flow Weight", Range(0, 1)) = 0.18
        _DetailWeight ("Detail Weight", Range(0, 1)) = 0.07

        _MinGray ("Min Gray", Range(0, 1)) = 0.22
        _MaxGray ("Max Gray", Range(0, 1)) = 0.92

        _Alpha ("Alpha", Range(0, 1)) = 0.65
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
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

            #include "UnityCG.cginc"

            sampler2D _CloudTex;
            sampler2D _FlowTex;
            sampler2D _DetailTex;

            float4 _CloudTex_ST;
            float4 _FlowTex_ST;
            float4 _DetailTex_ST;

            fixed4 _Tint;

            float _CloudScale;
            float _FlowScale;
            float _DetailScale;

            float4 _CloudSpeed;
            float4 _FlowSpeed;
            float4 _DetailSpeed;

            float _WarpStrength;

            float _CloudWeight;
            float _FlowWeight;
            float _DetailWeight;

            float _MinGray;
            float _MaxGray;

            float _Alpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 baseUV = i.uv;

                float2 cloudUV  = baseUV * _CloudScale  + _Time.y * _CloudSpeed.xy;
                float2 flowUV   = baseUV * _FlowScale   + _Time.y * _FlowSpeed.xy;
                float2 detailUV = baseUV * _DetailScale + _Time.y * _DetailSpeed.xy;

                cloudUV  = TRANSFORM_TEX(cloudUV, _CloudTex);
                flowUV   = TRANSFORM_TEX(flowUV, _FlowTex);
                detailUV = TRANSFORM_TEX(detailUV, _DetailTex);

                fixed4 cloudSample  = tex2D(_CloudTex, cloudUV);
                fixed4 flowSample   = tex2D(_FlowTex, flowUV);
                fixed4 detailSample = tex2D(_DetailTex, detailUV);

                float warpX = (
                    (detailSample.r - 0.5) * 0.9 +
                    (flowSample.r - 0.5) * 0.45
                ) * _WarpStrength;

                float warpY = (
                    (detailSample.g - 0.5) * 0.9 +
                    (cloudSample.r - 0.5) * 0.35
                ) * _WarpStrength;

                float2 warpedCloudUV  = cloudUV  + float2(warpX, warpY);
                float2 warpedFlowUV   = flowUV   + float2(-warpY * 0.8, warpX * 0.8);
                float2 warpedDetailUV = detailUV + float2(warpX * 0.5, -warpY * 0.5);

                cloudSample  = tex2D(_CloudTex, warpedCloudUV);
                flowSample   = tex2D(_FlowTex, warpedFlowUV);
                detailSample = tex2D(_DetailTex, warpedDetailUV);

                float cloudValue  = cloudSample.r;
                float flowValue   = flowSample.r;
                float detailValue = detailSample.r;

                float noise =
                    (cloudValue  * _CloudWeight) +
                    (flowValue   * _FlowWeight) +
                    (detailValue * _DetailWeight);

                noise = saturate(noise);
                
                // Giữ vùng sáng nền để không bị đen kịt
                noise = smoothstep(0.02, 0.95, noise);

                float gray = lerp(_MinGray, _MaxGray, noise);
                fixed3 finalRgb = fixed3(gray, gray, gray) * _Tint.rgb;

                // Alpha luôn có nền, không phụ thuộc noise quá mạnh
                float alphaNoise = lerp(0.75, 1.0, noise);
                float finalAlpha = _Tint.a * _Alpha * alphaNoise;

                return cloudSample;
            }
            ENDCG
        }
    }

    FallBack Off
}