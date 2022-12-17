real random01(real2 seed)
{
    return frac(sin(dot(seed, float2(12.9898, 78.233))) * 45758.5453);
}

real2 randomV2(real2 seed)
{
    real x = random01(seed);
    real y = random01(real2(x, 0.5645));
    return real2(x,y);
}

void AdditionalLights_float(float3 WorldPosition, float depth, float2 uv, out float3 color)
{
    color = 0;
    
#ifndef SHADERGRAPH_PREVIEW
    
    real3 worldPosition = WorldPosition;
    
    real3 camPos = _WorldSpaceCameraPos;
    real3 rayVec = worldPosition - camPos;
    real3 rayDirection = normalize(rayVec);
    real rayLength = length(rayVec);
    int stepCount = 64;
    real maxDistance = 500;
    real stepLength = 0.2;
    real clampedDepth = min(depth, maxDistance);
    real scaledStep = clampedDepth / stepCount;
    real4 shadowMask = real4(1, 1, 1, 1);
    
    int pixelLightCount = GetAdditionalLightsCount();
    uv += float2(1, 1) * _Time.y * 0.000001;
    real random = random01(uv);
    
    float jitter = random * stepLength;
                
    for (int j = 0; j < pixelLightCount; j++)
    {
        int perObjectLightIndex = GetPerObjectLightIndex(j);
        real3 lightPosition = _AdditionalLightsPosition[perObjectLightIndex].xyz;
        real lightDistance = length(lightPosition - camPos);
        real4 distanceAndSpotAttenuation = _AdditionalLightsAttenuation[perObjectLightIndex];
        real lightRange = rsqrt(distanceAndSpotAttenuation.x);
        lightRange = clamp(lightRange, 0, stepCount * stepLength);
        real lightCloseDistance = lightDistance - lightRange;
        lightCloseDistance = clamp(lightCloseDistance, 0, maxDistance);
        
        for (int i = 0; i < stepCount; i++)
        {
            // sample light attenuation
            real3 samplePosition = camPos + rayDirection * (stepLength * i + jitter + lightCloseDistance);
            Light light = GetAdditionalLight(j, samplePosition, shadowMask);
            
            // attenuation
            real lightAttenuation = light.distanceAttenuation * light.shadowAttenuation;
    
            // depth occlusion
            real sampleDistance = length(samplePosition - camPos);
            real penetration = depth - sampleDistance;
            real depthCull = saturate(penetration * 10);
            
                // noise
            real3 noisePosition = samplePosition / _NoiseScale + _Time.y * -_NoiseSpeed;
            real noise = SAMPLE_TEXTURE3D(_Noise, sampler_Noise, noisePosition).r;
            noise = lerp(_NoiseRemap.x, _NoiseRemap.y + 1, noise);
            noise = saturate(noise);

                // final combine
            color += _Density * light.color * noise * lightAttenuation * depthCull;
        }
    }
    
    color = clamp(color, _Clamp.x, _Clamp.y);
#endif
}