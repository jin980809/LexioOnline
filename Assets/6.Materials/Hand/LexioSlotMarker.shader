// 손패 칸 표시 (드래그 중). UI 패널과 같은 카툰풍: 둥근 사각형 + 진한 외곽선
// 채움 색은 _BaseColor (HandView가 칸마다 MaterialPropertyBlock으로 지정). 채움이 옅으면 외곽선도 같이 옅어짐
// 쿼드 스케일에서 실제 크기를 계산하므로 칸 크기가 바뀌어도 모서리/외곽선 두께는 월드 기준으로 유지됨
Shader "Lexio/SlotMarker"
{
    Properties
    {
        _BaseColor ("Fill Color", Color) = (1, 0.97, 0.88, 0.22)
        _OutlineColor ("Outline Color", Color) = (0.141, 0.086, 0.22, 0.85)
        _OutlineWidth ("Outline Width (World)", Float) = 0.035
        _CornerRadius ("Corner Radius (World)", Float) = 0.1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "SlotMarker"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _OutlineColor;
                float _OutlineWidth;
                float _CornerRadius;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                // 칸 중심 기준 위치와 칸 크기의 절반 (월드 단위)
                float2 local : TEXCOORD0;
                float2 halfSize : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // 기본 쿼드는 1x1이라 오브젝트 스케일이 곧 월드 크기
                float4x4 objectToWorld = GetObjectToWorldMatrix();
                float2 size = float2(length(objectToWorld._m00_m10_m20), length(objectToWorld._m01_m11_m21));
                output.halfSize = size * 0.5;
                output.local = (input.uv - 0.5) * size;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 둥근 사각형까지의 거리 (0 = 가장자리, 안쪽은 음수)
                float radius = min(_CornerRadius, min(input.halfSize.x, input.halfSize.y));
                float2 q = abs(input.local) - input.halfSize + radius;
                float dist = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
                float edge = fwidth(dist);

                half inside = 1.0 - smoothstep(-edge, edge, dist);
                half outline = smoothstep(-_OutlineWidth - edge, -_OutlineWidth + edge, dist);

                half4 outlineColor = _OutlineColor;
                outlineColor.a *= saturate(_BaseColor.a * 2.5);
                half4 color = lerp(_BaseColor, outlineColor, outline);
                color.a *= inside;
                return color;
            }
            ENDHLSL
        }
    }
}
