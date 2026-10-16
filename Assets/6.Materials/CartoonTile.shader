Shader "Lexio/CartoonTile"
{
    Properties
    {
        _BaseMap ("Pattern Map (검정 바탕)", 2D) = "black" {}
        _BodyColor ("Body Color", Color) = (0.16,0.16,0.2,1)
        _PatternBoost ("Pattern Brightness", Range(0.5,2)) = 1.1
        _PatternUnlit ("Pattern Unlit", Range(0,1)) = 0.8
        _MaskMin ("Mask Min", Range(0,1)) = 0.08
        _MaskMax ("Mask Max", Range(0,1)) = 0.35

        _ShadowColor ("Shadow Tint", Color) = (0.45,0.43,0.55,1)
        _MidColor ("Mid Tint", Color) = (0.72,0.71,0.8,1)
        _Step1 ("Shadow Step", Range(-1,1)) = 0.0
        _Step2 ("Light Step", Range(-1,1)) = 0.45
        _Smooth ("Edge Smooth", Range(0,0.1)) = 0.01

        _SpecColor ("Spec Color", Color) = (0.55,0.55,0.65,1)
        _SpecSize ("Spec Size", Range(0.8,1)) = 0.96

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
            half4 _BodyColor, _ShadowColor, _MidColor, _SpecColor, _OutlineColor;
            half _PatternBoost, _PatternUnlit, _MaskMin, _MaskMax;
            half _Step1, _Step2, _Smooth, _SpecSize;
            float _OutlineWidth;
        CBUFFER_END
        ENDHLSL

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
            struct A { float4 pos:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 n:TEXCOORD1; float3 wp:TEXCOORD2; };

            V vert(A i)
            {
                V o;
                o.wp  = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(o.wp);
                o.n   = TransformObjectToWorldNormal(i.n);
                o.uv  = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                half  bright = max(tex.r, max(tex.g, tex.b));
                half  mask = saturate((bright - _MaskMin) / max(_MaskMax - _MaskMin, 1e-4));

                float3 n = normalize(i.n);
                Light l = GetMainLight(TransformWorldToShadowCoord(i.wp));
                half ndl = dot(n, l.direction);
                ndl = min(ndl, l.shadowAttenuation * 2 - 1);

                half s1 = smoothstep(_Step1 - _Smooth, _Step1 + _Smooth, ndl);
                half s2 = smoothstep(_Step2 - _Smooth, _Step2 + _Smooth, ndl);
                half3 tint = lerp(_ShadowColor.rgb, lerp(_MidColor.rgb, 1, s2), s1);

                // 몸통: 툰 음영 + 딱 끊기는 하이라이트
                float3 v = GetWorldSpaceNormalizeViewDir(i.wp);
                half nh = dot(n, normalize(l.direction + v));
                half spec = step(_SpecSize, nh) * s1;
                half3 body = _BodyColor.rgb * tint * l.color + spec * _SpecColor.rgb;

                // 문양: 음영 영향 최소화
                half3 pattern = tex * _PatternBoost * lerp(tint * l.color, 1, _PatternUnlit);

                return half4(lerp(body, pattern, mask), 1);
            }
            ENDHLSL
        }

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
            { return TransformObjectToHClip(i.pos.xyz + normalize(i.n) * _OutlineWidth); }
            half4 frag() : SV_Target { return _OutlineColor; }
            ENDHLSL
        }

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