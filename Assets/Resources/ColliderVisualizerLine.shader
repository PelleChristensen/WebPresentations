// Screen-space thick lines for ColliderVisualizer.
// Each line segment is a quad of 4 vertices. Every vertex stores the segment's start point (POSITION),
// its end point (TEXCOORD1), which end it belongs to (uv.x: 0 = start, 1 = end) and which side (uv.y: -1/+1).
// The vertex shader projects both ends to the screen and pushes the vertex sideways by _Thickness pixels,
// so lines keep the same pixel width at any distance and any object scale.
// Lives in a Resources folder so it is always included in builds (Shader.Find).
Shader "Hidden/ColliderVisualizer/ThickLine"
{
    Properties
    {
        _Color ("Color", Color) = (0.57, 0.96, 0.53, 1)
        _Thickness ("Thickness (pixels)", Float) = 3
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Thickness;

            struct appdata
            {
                float3 a    : POSITION;   // segment start (object space)
                float2 uv   : TEXCOORD0;  // x: end (0/1), y: side (-1/+1)
                float3 b    : TEXCOORD1;  // segment end (object space)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4 ca = UnityObjectToClipPos(float4(v.a, 1));
                float4 cb = UnityObjectToClipPos(float4(v.b, 1));

                float2 halfScreen = _ScreenParams.xy * 0.5;
                float2 sa = ca.xy / max(abs(ca.w), 1e-5) * halfScreen;
                float2 sb = cb.xy / max(abs(cb.w), 1e-5) * halfScreen;

                float2 d = sb - sa;
                float len = length(d);
                d = len > 1e-4 ? d / len : float2(1, 0);
                float2 n = float2(-d.y, d.x);

                float endSign = v.uv.x * 2 - 1;            // -1 at start, +1 at end
                float4 c = v.uv.x < 0.5 ? ca : cb;
                // Push sideways, and extend half a thickness past each end so corners close up.
                float2 offsetPx = (n * v.uv.y + d * endSign) * (_Thickness * 0.5);
                c.xy += offsetPx / halfScreen * c.w;

                o.pos = c;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
