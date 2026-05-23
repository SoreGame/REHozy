// Snow Vertex Shader + PBR textures (Unity 6 URP).
// Текстуры: Diffuse, Normal, Roughness, Translucency. DeformMap — отдельная heightmap снег/земля.
Shader "LeftToMelt/SnowVertexLit" {
    Properties {
        [Header(Albedo)]
        _SnowTex ("Snow Texture (Diffuse/Albedo)", 2D) = "white" {}
        _SnowColor ("Snow Color Tint", Color) = (0.95, 0.97, 1, 1)
        _SnowScale ("Snow Tiling", Range(0.02, 8)) = 0.5

        [Header(Ground)]
        _GroundTex ("Ground Texture", 2D) = "gray" {}
        _GroundColor ("Ground Color", Color) = (0.3, 0.25, 0.2, 1)
        _GroundScale ("Ground Tiling", Range(0.05, 5)) = 0.5

        [Header(DirtySnow)]
        _DirtySnowTex ("Dirty Snow Texture", 2D) = "gray" {}
        _DirtySnowColor ("Dirty Snow Color", Color) = (0.6, 0.55, 0.5, 1)
        _DirtySnowScale ("Dirty Snow Tiling", Range(0.02, 8)) = 0.5

        [Header(Blend Ground Dirty Snow)]
        [Range(0.01, 0.99)] _SnowBlendLow ("Ground-Dirty boundary", Float) = 0.5
        [Range(0.02, 1)] _SnowBlendHigh ("Dirty-Snow boundary", Float) = 1

        [Header(Normal)]
        _BumpMap ("Normal Map (nor_gl)", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 2)) = 1

        [Header(Roughness)]
        _RoughnessMap ("Roughness Map", 2D) = "gray" {}
        _RoughnessScale ("Roughness Scale", Range(0, 1)) = 1
        _SmoothnessFallback ("Smoothness (if no texture)", Range(0, 1)) = 0.4

        [Header(Translucency)]
        _TranslucencyMap ("Translucency Map", 2D) = "white" {}
        _TranslucencyStrength ("Translucency Strength", Range(0, 1)) = 0.25

        [Header(Deform)]
        _DeformMap ("Heightmap R=1 snow R=0.5 dirty R=0 ground", 2D) = "white" {}
        _DeformPrevMap ("(Temporal) Previous deform map", 2D) = "white" {}
        _DeformScale ("Deform UV Scale", Range(0.01, 0.5)) = 0.05
        _SnowHeight ("Snow displacement along normal", Range(0, 2)) = 0.15
        _SnowVisibilityCutoff ("Snow visibility cutoff", Range(0, 1)) = 0.1
        _GlobalOffsetXZ ("Offset XZ world", Vector) = (0, 0, 0, 0)
        [Toggle] _DeformSmoothEnable ("Smooth edges (round)", Float) = 1
        [Range(0.01, 0.4)] _DeformSmoothRadius ("Smooth radius (world units)", Float) = 0.08
        [Range(0, 0.6)] _SmallPieceRadius ("Remove pieces smaller than (world, 0=off)", Float) = 0.15
        [Header(Deform Temporal Smoothing)]
        [Toggle] _DeformTemporalEnable ("Temporal smoothing (GPU lerp prev→current)", Float) = 0
        [Range(0.01, 0.5)] _DeformTemporalDuration ("Temporal duration (seconds)", Float) = 0.08
        _DeformTemporalStartTime ("Temporal start time (set by script)", Float) = 0

        [Header(Height Noise)]
        _HeightNoiseTex ("Height noise normal (waves_n)", 2D) = "bump" {}
        [Range(0.02, 8)] _HeightNoiseScale ("Height noise tiling", Float) = 0.35
        [Range(0, 1)] _HeightNoiseStrength ("Height noise amount", Float) = 0.12

        [Header(Edge Rounding)]
        [Toggle] _EdgeFalloffEnable ("Enable edge rounding (no overhang at corners)", Float) = 1
        [Toggle] _EdgeFalloffRadial ("Radial mound (round, not box)", Float) = 1
        [Toggle] _EdgeFalloffUseObjectPos ("Use object pos (ProBuilder)", Float) = 1
        [Range(0.02, 0.5)] _EdgeFalloffWidth ("Fade zone (larger = rounder edges)", Float) = 0.18
        [Vector2] _PlaneHalfExtent ("Plane half size XZ", Vector) = (2, 2, 0, 0)
        [Range(0, 1)] _EdgeHideLayers ("Hide layers at edge (0=visible, 1=smooth)", Float) = 0.85

        [Header(Lighting)]
        _SpecularStrength ("Specular strength", Range(0, 2)) = 0.8
        _AmbientStrength ("Ambient strength", Range(0, 2)) = 1
    }
    SubShader {
        Tags {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass {
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
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"

            TEXTURE2D(_SnowTex);
            SAMPLER(sampler_SnowTex);
            TEXTURE2D(_GroundTex);
            SAMPLER(sampler_GroundTex);
            TEXTURE2D(_DirtySnowTex);
            SAMPLER(sampler_DirtySnowTex);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_RoughnessMap);
            SAMPLER(sampler_RoughnessMap);
            TEXTURE2D(_TranslucencyMap);
            SAMPLER(sampler_TranslucencyMap);
            TEXTURE2D(_DeformMap);
            SAMPLER(sampler_DeformMap);
            TEXTURE2D(_DeformPrevMap);
            SAMPLER(sampler_DeformPrevMap);
            TEXTURE2D(_HeightNoiseTex);
            SAMPLER(sampler_HeightNoiseTex);

            CBUFFER_START(UnityPerMaterial)
            float4 _SnowTex_ST;
            half4 _SnowColor;
            half4 _GroundColor;
            half4 _DirtySnowColor;
            half _SnowScale;
            half _GroundScale;
            half _DirtySnowScale;
            half _DeformScale;
            half _SnowHeight;
            half _SnowVisibilityCutoff;
            half4 _GlobalOffsetXZ;
            half _DeformSmoothEnable;
            half _DeformSmoothRadius;
            half _SmallPieceRadius;
            half _DeformTemporalEnable;
            half _DeformTemporalDuration;
            half _DeformTemporalStartTime;
            half _HeightNoiseScale;
            half _HeightNoiseStrength;
            half _EdgeFalloffEnable;
            half _EdgeFalloffRadial;
            half _EdgeFalloffUseObjectPos;
            half _EdgeFalloffWidth;
            half4 _PlaneHalfExtent;
            half _EdgeHideLayers;
            half _SnowBlendLow;
            half _SnowBlendHigh;
            half _RoughnessScale;
            half _SmoothnessFallback;
            half _TranslucencyStrength;
            half _SpecularStrength;
            half _AmbientStrength;
            half _BumpScale;
            CBUFFER_END

            #include "SnowVertexLit_Deform.hlsl"

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 clipPos : SV_POSITION;
                float2 uvSnow : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                float3 posWS : TEXCOORD2;
                half3 normalWS : TEXCOORD3;
                half4 tangentWS : TEXCOORD4;
                half snowAmount : TEXCOORD5;
                half edgeFactor : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 posOS = v.vertex.xyz;
                float3 worldPosBase = TransformObjectToWorld(posOS).xyz;
                float3 normalOS = normalize(v.normal);

                REHOZY_ApplySnowVertexDeform(posOS, worldPosBase, v.uv, normalOS, o.snowAmount, o.edgeFactor);

                float3 tangentOS = v.tangent.xyz;
                if (length(tangentOS) < 0.01) {
                    tangentOS = abs(normalOS.y) < 0.999 ? cross(normalOS, float3(0, 1, 0)) : cross(normalOS, float3(1, 0, 0));
                    tangentOS = normalize(tangentOS);
                }
                half signB = v.tangent.w;
                if (abs(signB) < 0.01) signB = 1.0;

                float3 posWS = TransformObjectToWorld(posOS).xyz;
                float3 n = normalize(TransformObjectToWorldNormal(normalOS));
                float3 t = normalize(TransformObjectToWorldDir(tangentOS));
                o.tangentWS = half4(t, signB);
                o.clipPos = TransformWorldToHClip(posWS);
                o.uvSnow = worldPosBase.xz * _SnowScale;
                o.posWS = posWS;
                o.normalWS = n;
                o.fogFactor = ComputeFogFactor(o.clipPos.z);
                return o;
            }

            half3 SampleNormal(float2 uv) {
                half4 n = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv);
                half3 nTS = UnpackNormal(n);
                nTS.xy *= _BumpScale;
                nTS = normalize(nTS);
                return nTS;
            }

            half4 frag(v2f i) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Discard pixels where snow layer is thin enough to reveal the ground plane beneath.
                clip(i.snowAmount - _SnowVisibilityCutoff);

                // 3-level lerp: R=0 ground, Low..High dirty snow, R=1 white snow
                half t1 = smoothstep(0, _SnowBlendLow, i.snowAmount);
                half t2 = smoothstep(_SnowBlendLow, _SnowBlendHigh, i.snowAmount);
                half3 snowAlbedo = SAMPLE_TEXTURE2D(_SnowTex, sampler_SnowTex, i.uvSnow).rgb * _SnowColor.rgb;
                half3 groundAlbedo = SAMPLE_TEXTURE2D(_GroundTex, sampler_GroundTex, i.uvSnow * (_GroundScale / _SnowScale)).rgb * _GroundColor.rgb;
                half3 dirtyAlbedo = SAMPLE_TEXTURE2D(_DirtySnowTex, sampler_DirtySnowTex, i.uvSnow * (_DirtySnowScale / _SnowScale)).rgb * _DirtySnowColor.rgb;
                half3 albedo = lerp(lerp(groundAlbedo, dirtyAlbedo, t1), lerp(dirtyAlbedo, snowAlbedo, t2), t2);
                // На краях делаем белым — как верхняя снежная текстура, чтобы не было видно слоёв
                albedo = lerp(albedo, snowAlbedo, i.edgeFactor * _EdgeHideLayers);

                half3 nWS = normalize(i.normalWS);
                half3 tWS = i.tangentWS.xyz;
                half3 bWS = cross(nWS, tWS) * i.tangentWS.w;
                half3 nTS = SampleNormal(i.uvSnow);
                half3x3 TBN = half3x3(tWS, bWS, nWS);
                nWS = normalize(mul(nTS, TBN));

                // Roughness map: тёмное = гладко (острые блики), светлое = шероховато (размытые блики)
                half roughnessSample = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, i.uvSnow).r;
                half roughness = roughnessSample * _RoughnessScale;
                half smoothness = 1.0 - roughness;
                smoothness = lerp(_SmoothnessFallback, smoothness, _RoughnessScale);
                half specPower = exp2(10 * smoothness + 1);

                half translucencyMask = SAMPLE_TEXTURE2D(_TranslucencyMap, sampler_TranslucencyMap, i.uvSnow).r;

                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.posWS);

                // InputData для Unity 6 Forward+ (LIGHT_LOOP)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.posWS;
                inputData.normalWS = nWS;
                inputData.viewDirectionWS = viewDir;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.clipPos);

                // Main light
                float4 shadowCoord = TransformWorldToShadowCoord(i.posWS);
                Light mainLight = GetMainLight(shadowCoord);
                half atten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half NdotL = dot(nWS, mainLight.direction);
                half3 direct = mainLight.color * atten * saturate(NdotL);
                // Просвечивание: подсветка с обратной стороны (снег пропускает свет)
                half3 trans = mainLight.color * atten * translucencyMask * _TranslucencyStrength * saturate(-NdotL);
                half3 halfDir = normalize(mainLight.direction + viewDir);
                half spec = pow(saturate(dot(nWS, halfDir)), specPower) * _SpecularStrength * atten;
                half3 specular = mainLight.color * spec;

                // Additional lights (spot/point) — Unity 6 URP Forward+ или классический цикл
                #if _ADDITIONAL_LIGHTS
                uint pixelLightCount = GetAdditionalLightsCount();
                #if defined(LIGHT_LOOP_BEGIN)
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, inputData.positionWS, half4(1, 1, 1, 1));
                    half addAtten = addLight.distanceAttenuation * addLight.shadowAttenuation;
                    half addNdotL = dot(nWS, addLight.direction);
                    direct += addLight.color * addAtten * saturate(addNdotL);
                    trans += addLight.color * addAtten * translucencyMask * _TranslucencyStrength * saturate(-addNdotL);
                    half3 addHalf = normalize(addLight.direction + viewDir);
                    specular += addLight.color * pow(saturate(dot(nWS, addHalf)), specPower) * _SpecularStrength * addAtten;
                LIGHT_LOOP_END
                #else
                for (uint k = 0u; k < pixelLightCount; k++) {
                    Light addLight = GetAdditionalLight(k, inputData.positionWS, half4(1, 1, 1, 1));
                    half addAtten = addLight.distanceAttenuation * addLight.shadowAttenuation;
                    half addNdotL = dot(nWS, addLight.direction);
                    direct += addLight.color * addAtten * saturate(addNdotL);
                    trans += addLight.color * addAtten * translucencyMask * _TranslucencyStrength * saturate(-addNdotL);
                    half3 addHalf = normalize(addLight.direction + viewDir);
                    specular += addLight.color * pow(saturate(dot(nWS, addHalf)), specPower) * _SpecularStrength * addAtten;
                }
                #endif
                #endif

                half3 gi = SampleSH(nWS) * _AmbientStrength;
                half3 lit = albedo * (gi + direct) + trans + specular;

                half4 col = half4(lit, 1);
                col.rgb = MixFog(col.rgb, i.fogFactor);
                return col;
            }
            ENDHLSL
        }

        Pass {
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
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

            TEXTURE2D(_DeformMap);
            SAMPLER(sampler_DeformMap);
            TEXTURE2D(_DeformPrevMap);
            SAMPLER(sampler_DeformPrevMap);
            TEXTURE2D(_HeightNoiseTex);
            SAMPLER(sampler_HeightNoiseTex);

            CBUFFER_START(UnityPerMaterial)
            half _DeformScale;
            half _SnowHeight;
            half _SnowVisibilityCutoff;
            half4 _GlobalOffsetXZ;
            half _DeformSmoothEnable;
            half _DeformSmoothRadius;
            half _SmallPieceRadius;
            half _DeformTemporalEnable;
            half _DeformTemporalDuration;
            half _DeformTemporalStartTime;
            half _HeightNoiseScale;
            half _HeightNoiseStrength;
            half _EdgeFalloffEnable;
            half _EdgeFalloffRadial;
            half _EdgeFalloffUseObjectPos;
            half _EdgeFalloffWidth;
            half4 _PlaneHalfExtent;
            CBUFFER_END

            #include "SnowVertexLit_Deform.hlsl"

            struct ShadowAppdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowV2f {
                float4 pos : SV_POSITION;
                half snowAmount : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowV2f ShadowVert(ShadowAppdata v) {
                ShadowV2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 posOS = v.vertex.xyz;
                float3 worldPosBase = TransformObjectToWorld(posOS).xyz;
                float3 normalOS = normalize(v.normal);
                half edgeFactor;
                REHOZY_ApplySnowVertexDeform(posOS, worldPosBase, v.uv, normalOS, o.snowAmount, edgeFactor);

                float3 posWS = TransformObjectToWorld(posOS).xyz;
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                o.pos = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, _MainLightPosition.xyz));
                return o;
            }

            half4 ShadowFrag(ShadowV2f i) : SV_Target {
                clip(i.snowAmount - _SnowVisibilityCutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

            TEXTURE2D(_DeformMap);
            SAMPLER(sampler_DeformMap);
            TEXTURE2D(_DeformPrevMap);
            SAMPLER(sampler_DeformPrevMap);
            TEXTURE2D(_HeightNoiseTex);
            SAMPLER(sampler_HeightNoiseTex);

            CBUFFER_START(UnityPerMaterial)
            half _DeformScale;
            half _SnowHeight;
            half _SnowVisibilityCutoff;
            half4 _GlobalOffsetXZ;
            half _DeformSmoothEnable;
            half _DeformSmoothRadius;
            half _SmallPieceRadius;
            half _DeformTemporalEnable;
            half _DeformTemporalDuration;
            half _DeformTemporalStartTime;
            half _HeightNoiseScale;
            half _HeightNoiseStrength;
            half _EdgeFalloffEnable;
            half _EdgeFalloffRadial;
            half _EdgeFalloffUseObjectPos;
            half _EdgeFalloffWidth;
            half4 _PlaneHalfExtent;
            CBUFFER_END

            #include "SnowVertexLit_Deform.hlsl"

            struct DepthAppdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthV2f {
                float4 pos : SV_POSITION;
                half snowAmount : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthV2f DepthVert(DepthAppdata v) {
                DepthV2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 posOS = v.vertex.xyz;
                float3 worldPosBase = TransformObjectToWorld(posOS).xyz;
                float3 normalOS = normalize(v.normal);
                half edgeFactor;
                REHOZY_ApplySnowVertexDeform(posOS, worldPosBase, v.uv, normalOS, o.snowAmount, edgeFactor);

                o.pos = TransformObjectToHClip(posOS);
                return o;
            }

            half4 DepthFrag(DepthV2f i) : SV_Target {
                clip(i.snowAmount - _SnowVisibilityCutoff);
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
