Shader "SyntyStudios/Zombies"
{
    Properties
    {
        _Texture("Texture", 2D) = "white" {}
        _Blood("Blood", 2D) = "white" {}
        _BloodColor("BloodColor", Color) = (0.6470588,0.2569204,0.2569204,0)
        _BloodAmount("BloodAmount", Range(0, 1)) = 0
        _Spec("Spec", Color) = (0,0,0,0)
        _Smoothness("Smoothness", Range(0, 1)) = 0
        _Emissive("Emissive", 2D) = "black" {}
        _EmissiveColor("Emissive Color", Color) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Texture);
            SAMPLER(sampler_Texture);
            TEXTURE2D(_Blood);
            SAMPLER(sampler_Blood);
            TEXTURE2D(_Emissive);
            SAMPLER(sampler_Emissive);

            CBUFFER_START(UnityPerMaterial)
                float4 _Texture_ST;
                float4 _Blood_ST;
                float4 _Emissive_ST;
                float4 _BloodColor;
                float4 _EmissiveColor;
                float4 _Spec;
                float _BloodAmount;
                float _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float2 uv2 : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _Texture);
                output.uv2 = TRANSFORM_TEX(input.uv2, _Blood);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, input.uv);
                half4 bloodMask = SAMPLE_TEXTURE2D(_Blood, sampler_Blood, input.uv2);
                half3 albedo = lerp(baseColor.rgb, _BloodColor.rgb, saturate(bloodMask.r * _BloodAmount));

                Light mainLight = GetMainLight();
                half3 normalWS = normalize(input.normalWS);
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 lit = albedo * (mainLight.color * (ndotl * half(0.8) + half(0.25)));
                half3 emission = SAMPLE_TEXTURE2D(_Emissive, sampler_Emissive, input.uv).rgb * _EmissiveColor.rgb;
                return half4(lit + emission, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
