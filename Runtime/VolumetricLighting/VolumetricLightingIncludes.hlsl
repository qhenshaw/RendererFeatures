real random01(real2 p)
{
    return frac(sin(dot(p, real2(41, 289))) * 45758.5453);
}

void AdditionalLights_float(float3 WorldPosition, float2 uv, out float3 color)
{
    color = 0;
    
#ifndef SHADERGRAPH_PREVIEW
    
    float3 worldPosition = WorldPosition;
    
    float3 camPos = _WorldSpaceCameraPos;
    float3 rayVec = worldPosition - camPos;
    float3 rayDirection = normalize(rayVec);
    float rayLength = length(rayVec);
    float density = 0.03;
    int stepCount = 128;
    float maxDistance = 50;
    float stepLength = 0.1;
    float depth = length(worldPosition - camPos);
    float clampedDepth = min(depth, maxDistance);
    float scaledStep = clampedDepth / stepCount;
    
    int pixelLightCount = GetAdditionalLightsCount();
    uv += float2(1, 1) * _Time.y;
    float random = random01(uv);
    float offset = random * 0.1;
    
    float startOffset = random * stepLength * 1;
                
    for (int j = 0; j < pixelLightCount; j++)
    {
        for (int i = 0; i < stepCount; i++)
        {
            // sample light attenuation
            float3 samplePosition = camPos + rayDirection * (stepLength * i + startOffset);
            Light light = GetAdditionalLight(j, samplePosition, half4(1, 1, 1, 1));
    
            // depth occlusion
            float sampleDistance = length(samplePosition - camPos);
            float penetration = sampleDistance - depth;
            float depthCull = penetration < 0;
            
            // noise
            float3 noisePosition = samplePosition / _NoiseScale + _Time.y * -_NoiseSpeed;
            float noise = SAMPLE_TEXTURE3D(_Noise, sampler_Noise, noisePosition).r;
            noise = lerp(_NoiseRemap.x, _NoiseRemap.y + 1, noise);
            noise = saturate(noise);
                
            // final contribution
            float lightAttenuation = light.distanceAttenuation * light.shadowAttenuation;
            color += density * light.color * noise * lightAttenuation * depthCull;
            color = clamp(color, _Clamp.x, _Clamp.y);
        }
    }
#endif
}