// 반전 헐(inverted hull) 외곽선. 선택된 타일의 렌더러에 머티리얼로 추가해서 사용
// _OutlineCenter: 메시 바운드 중심(오브젝트 공간). 중심에서 바깥 방향으로 월드 기준 _OutlineWidth 만큼 부풀림
Shader "Lexio/Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.85, 0.1, 1)
        _OutlineWidth ("Outline Width (World)", Float) = 0.06
        _OutlineCenter ("Outline Center (Object)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float4 _OutlineCenter;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 centerWS = TransformObjectToWorld(_OutlineCenter.xyz);
                float3 direction = positionWS - centerWS;
                float lengthSq = dot(direction, direction);
                if (lengthSq > 1e-8)
                {
                    positionWS += direction * rsqrt(lengthSq) * _OutlineWidth;
                }
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
