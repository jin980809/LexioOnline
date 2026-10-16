Shader "Lexio/Cartoon"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1.0
        _ShadowColor ("Shadow Tint", Color) = (0.35,0.33,0.44,1)
        _MidColor ("Mid Tint", Color) = (0.69,0.68,0.75,1)
        _Step1 ("Shadow Step", Range(-1,1)) = 0.0
        _Step2 ("Light Step", Range(-1,1)) = 0.45
        _Smooth ("Edge Smooth", Range(0,0.1)) = 0.01
        _OutlineColor ("Outline Color", Color) = (0.141,0.086,0.22,1)
        _OutlineWidth ("Outline Width", Range(0,0.1)) = 0.02
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _ShadowColor, _MidColor, _OutlineColor;
            half _Step1, _Step2, _Smooth, _BumpScale;
            float _OutlineWidth;
        CBUFFER_END
        ENDHLSL

        // 1) 툰 음영
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            struct A { float4 pos:POSITION; float3 n:NORMAL; float4 t:TANGENT; float2 uv:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 n:TEXCOORD1; float3 wp:TEXCOORD2; float4 t:TEXCOORD3; };

            V vert(A i)
            {
                V o;
                o.wp  = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(o.wp);
                o.n   = TransformObjectToWorldNormal(i.n);
                o.t   = float4(TransformObjectToWorldDir(i.t.xyz), i.t.w * GetOddNegativeScale());
                o.uv  = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                Light l = GetMainLight(TransformWorldToShadowCoord(i.wp));
                float3 n = normalize(i.n);
                float3 t = normalize(i.t.xyz);
                float3 b = cross(n, t) * i.t.w;
                half3 nts = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                n = normalize(TransformTangentToWorld(nts, half3x3(t, b, n)));
                half ndl = dot(n, l.direction);
                ndl = min(ndl, l.shadowAttenuation * 2 - 1);

                half s1 = smoothstep(_Step1 - _Smooth, _Step1 + _Smooth, ndl);
                half s2 = smoothstep(_Step2 - _Smooth, _Step2 + _Smooth, ndl);
                half3 tint = lerp(_ShadowColor.rgb, lerp(_MidColor.rgb, 1, s2), s1);
                return half4(albedo.rgb * tint * l.color, albedo.a);
            }
            ENDHLSL
        }

        // 2) 외곽선
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 pos:POSITION; float3 n:NORMAL; };
            float4 vert(A i) : SV_POSITION
            {
                return TransformObjectToHClip(i.pos.xyz + normalize(i.n) * _OutlineWidth);
            }
            half4 frag() : SV_Target { return _OutlineColor; }
            ENDHLSL
        }

        // 3) 그림자 드리우기
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection;
            struct A { float4 pos:POSITION; float3 n:NORMAL; };
            float4 vert(A i) : SV_POSITION
            {
                float3 wp = TransformObjectToWorld(i.pos.xyz);
                float3 wn = TransformObjectToWorldNormal(i.n);
                float4 cs = TransformWorldToHClip(ApplyShadowBias(wp, wn, _LightDirection));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return cs;
            }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        // 4) 깊이 (카메라 깊이 텍스처용)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 pos:POSITION; };
            float4 vert(A i) : SV_POSITION { return TransformObjectToHClip(i.pos.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}