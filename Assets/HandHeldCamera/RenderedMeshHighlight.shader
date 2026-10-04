// Glow/rim overlay drawn ONLY by the viewer (main) camera on top of meshes that
// the InWorldCamera sends for rendering. Used by RenderedMeshHighlighter.cs.
// _ZTest = LessEqual (4): highlight visible surfaces.
// _ZTest = Greater  (5): x-ray - show parts hidden behind other geometry.
Shader "GameCraft/RenderedMeshHighlight"
{
    Properties
    {
        _Color ("Color", Color) = (0.2, 0.9, 1, 1)
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.06
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0, 4)) = 1.1
        _PulseSpeed ("Pulse Speed", Float) = 2.5
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.3
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Highlight"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One      // additive glow
            ZWrite Off
            ZTest [_ZTest]
            Cull Back
            Offset -1, -1           // sit just in front of the original surface

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _FillAlpha;
                half _RimPower;
                half _RimIntensity;
                half _PulseSpeed;
                half _PulseAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = normalize(i.viewDirWS);
                half rim = pow(1.0h - saturate(abs(dot(n, v))), _RimPower);
                half pulse = 1.0h - _PulseAmount * (0.5h + 0.5h * sin(_Time.y * _PulseSpeed));
                half a = saturate(_FillAlpha + rim * _RimIntensity) * pulse * _Color.a;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
