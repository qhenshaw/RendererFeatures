//standard hash
real random(real2 p)
{
    return frac(sin(dot(p, real2(41, 289))) * 45758.5453) - 0.5;
}
real random01(real2 p)
{
    return frac(sin(dot(p, real2(41, 289))) * 45758.5453);
}

void AdditionalLights_float(float3 WorldPosition, float depth, float2 uv, float noise, out float3 color)
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
    real minStepLength = 0.05;
    real maxStepLength = 0.2;
    real stepLengthDistance = 2;
    real clampedDepth = min(depth, maxDistance);
    real scaledStep = clampedDepth / stepCount;
    real4 shadowMask = real4(1, 1, 1, 1);
    
    int pixelLightCount = GetAdditionalLightsCount();
    real random = random01(uv);
                
    UNITY_LOOP
    for (int j = 0; j < pixelLightCount; j++)
    {
        int perObjectLightIndex = GetPerObjectLightIndex(j);
        real3 lightPosition = _AdditionalLightsPosition[perObjectLightIndex].xyz;
        real3 lightForwardDirection = _AdditionalLightsSpotDir[perObjectLightIndex].xyz;
        real4 shadowParams = GetAdditionalLightShadowParams(perObjectLightIndex);
        real pointLightCorrection = max(shadowParams.z, 1 - shadowParams.x);
        real lightDistance = length(lightPosition - camPos);
        real stepLength = lerp(minStepLength, maxStepLength, saturate(lightDistance / stepLengthDistance));
        real jitter = random * stepLength * 2.5;
        real4 distanceAndSpotAttenuation = _AdditionalLightsAttenuation[perObjectLightIndex];
        real lightRange = rsqrt(distanceAndSpotAttenuation.x);
        lightRange = clamp(lightRange, 0, stepCount * stepLength);
        real lightCloseDistance = lightDistance - lightRange;
        lightCloseDistance = clamp(lightCloseDistance, 0, maxDistance);
        
        real3 startPosition = camPos + rayDirection * (jitter + lightCloseDistance);
        real startDepth = length(startPosition - camPos);
        
        UNITY_BRANCH
        if(startDepth < depth)
        {
            UNITY_LOOP
            for (int i = 0; i < stepCount; i++)
            {
                // sample light attenuation
                real3 samplePosition = startPosition + rayDirection * stepLength * i;
                Light light = GetAdditionalLight(j, samplePosition, shadowMask);
                
                // attenuation
                real lightAttenuation = light.distanceAttenuation * light.shadowAttenuation;
                
                UNITY_BRANCH
                if (lightAttenuation > 0.01)
                {
                    // depth occlusion
                    real sampleDistance = length(samplePosition - camPos);
                    real penetration = depth - sampleDistance;
                
                    UNITY_BRANCH
                    if (penetration > 0)
                    {
                        // noise
                        real noise = 1;
    #ifdef USENOISE
                        real3 noisePosition = samplePosition / _NoiseScale + _Time.y * -_NoiseSpeed;
                        noise = SAMPLE_TEXTURE3D(_Noise, sampler_Noise, noisePosition).r;
                        noise = lerp(_NoiseRemap.x, _NoiseRemap.y + 1, noise);
                        noise = saturate(noise);
    #endif

                        // final combine
                        color += _Density * light.color * noise * lightAttenuation;
                    }
                }
            }
        }
    }
    
    color = clamp(color, _Clamp.x, _Clamp.y);
#endif
}