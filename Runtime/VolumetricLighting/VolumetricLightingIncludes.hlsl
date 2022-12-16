real random01(real2 p)
{
    return frac(sin(dot(p, real2(41, 289))) * 45758.5453);
}

void AdditionalLights_float(float3 WorldPosition, float depth, float2 uv, out float3 color)
{
    color = 0;
    
#ifndef SHADERGRAPH_PREVIEW
    
    float3 worldPosition = WorldPosition;
    
    float3 camPos = _WorldSpaceCameraPos;
    float3 rayVec = worldPosition - camPos;
    float3 rayDirection = normalize(rayVec);
    float rayLength = length(rayVec);
    int stepCount = 64;
    float maxDistance = 500;
    float stepLength = 0.1;
    float clampedDepth = min(depth, maxDistance);
    float scaledStep = clampedDepth / stepCount;
    half4 shadowMask = half4(1, 1, 1, 1);
    
    int pixelLightCount = GetAdditionalLightsCount();
    int maxLights = 8;
    pixelLightCount = min(pixelLightCount, maxLights);
    uv += float2(1, 1) * _Time.y;
    float random = random01(uv);
    float offset = random * 0.1;
    
    float jitter = random * stepLength * 1;
                
    for (int j = 0; j < pixelLightCount; j++)
    {
        int perObjectLightIndex = GetPerObjectLightIndex(j);
        float3 lightPosition = _AdditionalLightsPosition[perObjectLightIndex].xyz;
        float lightDistance = length(lightPosition - camPos);
        float4 distanceAndSpotAttenuation = _AdditionalLightsAttenuation[perObjectLightIndex];
        float lightRange = rsqrt(distanceAndSpotAttenuation.x);
        float lightCloseDistance = lightDistance - lightRange;
        lightCloseDistance = clamp(lightCloseDistance, 0, maxDistance);
        
        for (int i = 0; i < stepCount; i++)
        {
            // sample light attenuation
            float3 samplePosition = camPos + rayDirection * (stepLength * i + jitter + lightCloseDistance);
            Light light = GetAdditionalLight(j, samplePosition, shadowMask);
            
            // attenuation
            float lightAttenuation = light.distanceAttenuation * light.shadowAttenuation;
    
            UNITY_BRANCH
            if (lightAttenuation > 0.01)
            {
                // depth occlusion
                float sampleDistance = length(samplePosition - camPos);
                float penetration = depth - sampleDistance;
                float depthCull = saturate(penetration * 10);
            
                // noise
                float3 noisePosition = samplePosition / _NoiseScale + _Time.y * -_NoiseSpeed;
                float noise = SAMPLE_TEXTURE3D(_Noise, sampler_Noise, noisePosition).r;
                noise = lerp(_NoiseRemap.x, _NoiseRemap.y + 1, noise);
                noise = saturate(noise);

                // final combine
                color += _Density * light.color * noise * lightAttenuation * depthCull;
            }
        }
    }
    
    color = clamp(color, _Clamp.x, _Clamp.y);
#endif
}