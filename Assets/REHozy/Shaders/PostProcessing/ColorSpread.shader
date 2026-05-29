Shader "Hidden/REHozy/ColorSpread"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "ColorSpread"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            float4 _Center;
            float _StartTime;
            float _GrowthSpeed;
            float _MaxRadius;
            float _EdgeSoftness;
            float _NoiseScale;
            float _NoiseStrength;
            float _Step;
            int _PreviousMask;
            int _WaveAddMask;
            float _WaveEdgeIntensity;
            float _WaveEdgeWidth;
            float4 _WaveEdgeColor;
            float4 _RedHueRangeA;
            float4 _RedHueRangeB;
            float4 _BlueHueRangeA;
            float4 _BlueHueRangeB;
            float4 _GreenHueRangeA;
            float4 _GreenHueRangeB;
            float4x4 _InverseViewProjMatrix;

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            static const float3 LuminanceWeights = float3(0.2126729, 0.7151522, 0.0721750);
            static const int MaskRed = 1;
            static const int MaskBlue = 2;
            static const int MaskGreen = 4;
            static const int MaskAll = 8;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            float3 RgbToHsv(float3 c)
            {
                float4 k = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, k.wz), float4(c.gb, k.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            float HueInRange(float hue, float4 range)
            {
                if (range.y < range.x)
                    return step(range.x, hue) + step(hue, range.y);
                return step(range.x, hue) * step(hue, range.y);
            }

            float HueMaskRed(float hue)
            {
                return max(HueInRange(hue, _RedHueRangeA), HueInRange(hue, _RedHueRangeB));
            }

            float HueMaskBlue(float hue)
            {
                return max(HueInRange(hue, _BlueHueRangeA), HueInRange(hue, _BlueHueRangeB));
            }

            float HueMaskGreen(float hue)
            {
                return max(HueInRange(hue, _GreenHueRangeA), HueInRange(hue, _GreenHueRangeB));
            }

            float HueMaskFromPaletteBits(float hue, int mask)
            {
                if (mask & MaskAll)
                    return 1.0;

                float m = 0.0;
                if (mask & MaskRed)
                    m = max(m, HueMaskRed(hue));
                if (mask & MaskBlue)
                    m = max(m, HueMaskBlue(hue));
                if (mask & MaskGreen)
                    m = max(m, HueMaskGreen(hue));
                return m;
            }

            float3 ApplyPaletteMask(float3 fullColor, int mask)
            {
                float3 result = dot(fullColor, LuminanceWeights).xxx;
                float hue = RgbToHsv(fullColor).x;

                if (mask & MaskRed)
                    result = lerp(result, fullColor, HueMaskRed(hue));
                if (mask & MaskBlue)
                    result = lerp(result, fullColor, HueMaskBlue(hue));
                if (mask & MaskGreen)
                    result = lerp(result, fullColor, HueMaskGreen(hue));

                return result;
            }

            bool IsSkyDepth(float deviceDepth)
            {
            #if UNITY_REVERSED_Z
                return deviceDepth < 1e-4;
            #else
                return deviceDepth > 1.0 - 1e-4;
            #endif
            }

            float ComputeEffectRadius(float2 worldPosXZ)
            {
                float timeElapsed = _Time.y - _StartTime;
                float effectRadius = min(timeElapsed * _GrowthSpeed, _MaxRadius);
                effectRadius = max(effectRadius, 0.0);

                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, worldPosXZ * _NoiseScale).r;
                return max(effectRadius - noise * _NoiseStrength, 0.0);
            }

            float ComputeSpreadFromDist(float dist, float effectRadius)
            {
                float softness = max(_EdgeSoftness, 0.001);
                return 1.0 - smoothstep(effectRadius - softness, effectRadius, dist);
            }

            float ComputeWaveEdge(float dist, float effectRadius, float spread, bool useDistanceRing)
            {
                if (_WaveEdgeIntensity <= 0.0)
                    return 0.0;

                float ringSpread = smoothstep(0.4, 0.5, spread) * (1.0 - smoothstep(0.5, 0.6, spread));
                float ringDist = 0.0;
                if (useDistanceRing)
                {
                    float band = max(_WaveEdgeWidth, 0.25);
                    ringDist = smoothstep(effectRadius - band, effectRadius, dist)
                        * (1.0 - smoothstep(effectRadius, effectRadius + band * 0.35, dist));
                }

                return saturate(max(ringDist, ringSpread)) * _WaveEdgeIntensity;
            }

            float3 ApplyWaveEdge(float3 color, float dist, float effectRadius, float spread, bool useDistanceRing)
            {
                float edge = ComputeWaveEdge(dist, effectRadius, spread, useDistanceRing);
                return lerp(color, _WaveEdgeColor.rgb, edge * _WaveEdgeColor.a);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                float4 fullColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);

                if (_Step < 0.5)
                    return float4(dot(fullColor.rgb, LuminanceWeights).xxx, fullColor.a);

                float deviceDepth = SampleSceneDepth(uv);
                float3 worldPos = ComputeWorldSpacePosition(uv, deviceDepth, _InverseViewProjMatrix);
                bool isSky = IsSkyDepth(deviceDepth);
                bool fullColorWave = _Step >= 3.5;

                float3 prevColor = ApplyPaletteMask(fullColor.rgb, _PreviousMask);

                // Sky depth gives meaningless XZ distance; distance-based spread/edge leaves a stuck ring.
                if (isSky)
                {
                    if (!fullColorWave)
                        return float4(prevColor, fullColor.a);

                    float globalSpread = saturate(((_Time.y - _StartTime) * _GrowthSpeed) / max(_MaxRadius, 0.001));
                    float3 skyResult = lerp(prevColor, fullColor.rgb, globalSpread);
                    skyResult = ApplyWaveEdge(skyResult, 0.0, 0.0, globalSpread, false);
                    return float4(skyResult, fullColor.a);
                }

                float dist = distance(worldPos.xz, _Center.xz);
                float effectRadius = ComputeEffectRadius(worldPos.xz);
                float spread = ComputeSpreadFromDist(dist, effectRadius);

                if (fullColorWave)
                {
                    float3 result = lerp(prevColor, fullColor.rgb, spread);
                    result = ApplyWaveEdge(result, dist, effectRadius, spread, true);
                    return float4(result, fullColor.a);
                }

                float hue = RgbToHsv(fullColor.rgb).x;
                float addBand = HueMaskFromPaletteBits(hue, _WaveAddMask);
                float3 withNewBand = lerp(prevColor, fullColor.rgb, addBand);
                float3 result = lerp(prevColor, withNewBand, spread);
                result = ApplyWaveEdge(result, dist, effectRadius, spread, true);
                return float4(result, fullColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
