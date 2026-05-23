#ifndef REHOZY_SNOW_VERTEX_LIT_DEFORM_INCLUDED
#define REHOZY_SNOW_VERTEX_LIT_DEFORM_INCLUDED

half REHOZY_SampleDeformMap(TEXTURE2D_PARAM(tex, samp), float2 deformUV)
{
    half deform;
    if (_DeformSmoothEnable > 0.5)
    {
        float2 uvStep = _DeformSmoothRadius * _DeformScale;
        deform = 0;
        [unroll] for (int sy = -1; sy <= 1; sy++)
        [unroll] for (int sx = -1; sx <= 1; sx++)
            deform += SAMPLE_TEXTURE2D_LOD(tex, samp, deformUV + float2(sx, sy) * uvStep, 0).r;
        deform /= 9.0;
    }
    else
    {
        deform = SAMPLE_TEXTURE2D_LOD(tex, samp, deformUV, 0).r;
    }

    if (_SmallPieceRadius > 0.005)
    {
        float2 smallStep = _SmallPieceRadius * _DeformScale;
        half smallBlur = 0;
        [unroll] for (int my = -1; my <= 1; my++)
        [unroll] for (int mx = -1; mx <= 1; mx++)
            smallBlur += SAMPLE_TEXTURE2D_LOD(tex, samp, deformUV + float2(mx, my) * smallStep, 0).r;
        smallBlur /= 9.0;
        deform = min(deform, smallBlur);
    }

    return deform;
}

half REHOZY_ComputeEdgeFalloff(float3 posOS, float2 meshUV)
{
    if (_EdgeFalloffEnable < 0.5)
    {
        return 1.0;
    }

    float d;
    if (_EdgeFalloffRadial > 0.5)
    {
        half2 h = max(_PlaneHalfExtent.xy, 0.001);
        float2 fracPos = (posOS.xz + h) / (2.0 * h);
        float2 centered = fracPos * 2.0 - 1.0;
        float2 ellip = float2(centered.x, centered.y * (h.x / h.y));
        d = 1.0 - saturate(length(ellip));
    }
    else if (_EdgeFalloffUseObjectPos > 0.5)
    {
        half2 h = max(_PlaneHalfExtent.xy, 0.001);
        float2 fracPos = (posOS.xz + h) / (2.0 * h);
        d = min(min(fracPos.x, 1.0 - fracPos.x), min(fracPos.y, 1.0 - fracPos.y)) * 2.0;
    }
    else
    {
        d = min(min(meshUV.x, 1.0 - meshUV.x), min(meshUV.y, 1.0 - meshUV.y)) * 2.0;
    }

    half w = clamp(_EdgeFalloffWidth, 0.02, 0.5);
    return smoothstep(0.0, w, d);
}

half REHOZY_SampleHeightNoise(float2 worldXZ, half edgeFalloff)
{
    if (_HeightNoiseStrength < 0.0001)
    {
        return 0;
    }

    half4 packed = SAMPLE_TEXTURE2D_LOD(
        _HeightNoiseTex, sampler_HeightNoiseTex, worldXZ * _HeightNoiseScale, 0);
    half3 n = UnpackNormalScale(packed, 1.0);
    half ripple = saturate(1.0 - n.z);
    return ripple * _HeightNoiseStrength * edgeFalloff;
}

half REHOZY_ResolveTemporalDeform(half deformNow, float2 deformUV)
{
    if (_DeformTemporalEnable < 0.5)
    {
        return deformNow;
    }

    half deformPrev = REHOZY_SampleDeformMap(TEXTURE2D_ARGS(_DeformPrevMap, sampler_DeformPrevMap), deformUV);
    float dur = max((float)_DeformTemporalDuration, 0.0001);
    float t = saturate((_Time.y - (float)_DeformTemporalStartTime) / dur);
    t = t * t * (3.0 - 2.0 * t);
    return lerp(deformPrev, deformNow, t);
}

void REHOZY_ApplySnowVertexDeform(
    inout float3 posOS,
    float3 worldPosBase,
    float2 meshUV,
    float3 normalOS,
    out half snowAmount,
    out half edgeFactor)
{
    float2 deformUV = (worldPosBase.xz + float2(_GlobalOffsetXZ.x, _GlobalOffsetXZ.y)) * _DeformScale;
    half deformNow = REHOZY_SampleDeformMap(TEXTURE2D_ARGS(_DeformMap, sampler_DeformMap), deformUV);
    half deform = REHOZY_ResolveTemporalDeform(deformNow, deformUV);
    half falloff = REHOZY_ComputeEdgeFalloff(posOS, meshUV);
    half noise = REHOZY_SampleHeightNoise(worldPosBase.xz, falloff);

    deform = saturate(deform * falloff + noise);
    snowAmount = deform;
    edgeFactor = (1.0 - falloff) * (_EdgeFalloffEnable > 0.5 ? 1.0 : 0.0);
    posOS += normalOS * (deform * _SnowHeight);
}

#endif
