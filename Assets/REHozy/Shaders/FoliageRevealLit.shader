// Opaque URP foliage with dither reveal (no alpha blending).
Shader "REHozy/FoliageRevealLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (0.35, 0.75, 0.35, 1)
        [Range(0, 1)] _Reveal("Reveal", Float) = 0
        [Range(0, 0.25)] _RevealSoftness("Reveal Softness", Float) = 0.04
        [Range(1, 8)] _DitherScale("Dither Scale", Float) = 3
        [Range(0, 2)] _SpecularStrength("Specular Strength", Float) = 0.35
        [Range(0, 2)] _AmbientStrength("Ambient Strength", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Reveal;
            half _RevealSoftness;
            half _DitherScale;
            half _SpecularStrength;
            half _AmbientStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half Dither4x4(float2 screenPos)
            {
                uint2 p = (uint2)fmod(screenPos, 4.0);
                const half dither[16] =
                {
                    0.0 / 16.0, 8.0 / 16.0, 2.0 / 16.0, 10.0 / 16.0,
                    12.0 / 16.0, 4.0 / 16.0, 14.0 / 16.0, 6.0 / 16.0,
                    3.0 / 16.0, 11.0 / 16.0, 1.0 / 16.0, 9.0 / 16.0,
                    15.0 / 16.0, 7.0 / 16.0, 13.0 / 16.0, 5.0 / 16.0
                };
                return dither[p.x + p.y * 4u];
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                output.screenPos = ComputeScreenPos(vertexInput.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUv = input.screenPos.xy / max(input.screenPos.w, 0.0001);
                half dither = Dither4x4(screenUv * _ScreenParams.xy / _DitherScale);
                half revealCutoff = 1.0h - _Reveal + _RevealSoftness;
                clip(dither - revealCutoff);

                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half3 normalWS = normalize(input.normalWS);
                half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDir;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half mainAtten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half mainNdotL = saturate(dot(normalWS, mainLight.direction));
                half3 direct = mainLight.color * mainAtten * mainNdotL;

                half3 halfDir = normalize(mainLight.direction + viewDir);
                half spec = pow(saturate(dot(normalWS, halfDir)), 32.0h) * _SpecularStrength * mainAtten;
                half3 specular = mainLight.color * spec;

                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                #if defined(LIGHT_LOOP_BEGIN)
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, inputData.positionWS, half4(1, 1, 1, 1));
                    half addAtten = addLight.distanceAttenuation * addLight.shadowAttenuation;
                    half addNdotL = saturate(dot(normalWS, addLight.direction));
                    direct += addLight.color * addAtten * addNdotL;
                    half3 addHalf = normalize(addLight.direction + viewDir);
                    specular += addLight.color * pow(saturate(dot(normalWS, addHalf)), 32.0h)
                        * _SpecularStrength * addAtten;
                LIGHT_LOOP_END
                #else
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light addLight = GetAdditionalLight(lightIndex, inputData.positionWS, half4(1, 1, 1, 1));
                    half addAtten = addLight.distanceAttenuation * addLight.shadowAttenuation;
                    half addNdotL = saturate(dot(normalWS, addLight.direction));
                    direct += addLight.color * addAtten * addNdotL;
                    half3 addHalf = normalize(addLight.direction + viewDir);
                    specular += addLight.color * pow(saturate(dot(normalWS, addHalf)), 32.0h)
                        * _SpecularStrength * addAtten;
                }
                #endif
                #endif

                half3 gi = SampleSH(normalWS) * _AmbientStrength;
                half3 lit = albedo * (gi + direct) + specular;
                half4 col = half4(lit, 1.0h);
                col.rgb = MixFog(col.rgb, input.fogFactor);
                return col;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half _Reveal;
            half _RevealSoftness;
            half _DitherScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half Dither4x4(float2 screenPos)
            {
                uint2 p = (uint2)fmod(screenPos, 4.0);
                const half dither[16] =
                {
                    0.0 / 16.0, 8.0 / 16.0, 2.0 / 16.0, 10.0 / 16.0,
                    12.0 / 16.0, 4.0 / 16.0, 14.0 / 16.0, 6.0 / 16.0,
                    3.0 / 16.0, 11.0 / 16.0, 1.0 / 16.0, 9.0 / 16.0,
                    15.0 / 16.0, 7.0 / 16.0, 13.0 / 16.0, 5.0 / 16.0
                };
                return dither[p.x + p.y * 4u];
            }

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz));
                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                float2 screenUv = input.screenPos.xy / max(input.screenPos.w, 0.0001);
                half dither = Dither4x4(screenUv * _ScreenParams.xy / _DitherScale);
                half revealCutoff = 1.0h - _Reveal + _RevealSoftness;
                clip(dither - revealCutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
