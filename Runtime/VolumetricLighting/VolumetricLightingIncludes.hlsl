real random01(real2 p)
{
    return frac(sin(dot(p, real2(41, 289))) * 45758.5453);
}

void AdditionalLightsContribution_float(float3 WorldPosition, float3 surfacePosition, float depth, float2 uv, float noise, out float3 color)
{
    color = 0;
    
#ifndef SHADERGRAPH_PREVIEW
    
    real3 worldPosition = WorldPosition;
    
    real3 camPos = _WorldSpaceCameraPos;
    real3 rayVec = worldPosition - camPos;
    real3 rayDirection = normalize(rayVec);
    real rayLength = length(rayVec);
    real surfaceDistance = length(surfacePosition - camPos);
    int stepCount = 32;
    real maxDistance = 500;
    real minStepLength = 0.05;
    real maxStepLength = 0.2;
    real stepLengthDistance = 2;
    real clampedDepth = min(depth, maxDistance);
    real scaledStep = clampedDepth / stepCount;
    real4 shadowMask = real4(1, 1, 1, 1);
    
    int pixelLightCount = GetAdditionalLightsCount();
    
    //real3 particleParams = SAMPLE_TEXTURE2D(_VolumetricLightingParticleDensity, sampler_VolumetricLightingParticleDensity, uv);
                
    UNITY_LOOP
    for (int j = 0; j < pixelLightCount; j++)
    {
        int perObjectLightIndex = GetPerObjectLightIndex(j);
        real3 lightPosition = _AdditionalLightsPosition[perObjectLightIndex].xyz;
        
        UNITY_BRANCH
        if (length(lightPosition - SHADERGRAPH_OBJECT_POSITION) < _MaxLightDistance)
        {
            real4 shadowParams = GetAdditionalLightShadowParams(perObjectLightIndex);
            real pointLightCorrection = max(shadowParams.z, 1 - shadowParams.x);
            real boost = lerp(_SpotLightBoost, _PointLightBoost, pointLightCorrection);
            real lightDistance = length(lightPosition - camPos);
            real stepLength = lerp(minStepLength, maxStepLength, saturate(lightDistance / stepLengthDistance));
            real jitter = noise * stepLength * 2.5;
            real4 distanceAndSpotAttenuation = _AdditionalLightsAttenuation[perObjectLightIndex];
            real lightRange = rsqrt(distanceAndSpotAttenuation.x);
            lightRange = clamp(lightRange, 0, stepCount * stepLength);
            real lightCloseDistance = lightDistance - lightRange;
            lightCloseDistance = clamp(lightCloseDistance, 0, maxDistance);
            real3 startPosition = camPos + rayDirection * (jitter + lightCloseDistance);
            real startDepth = length(startPosition - camPos);
        
            UNITY_BRANCH
            if (startDepth < depth)
            {
                UNITY_LOOP
                for (int i = 0; i < stepCount; i++)
                {
                    // sample light attenuation
                    real3 samplePosition = startPosition + rayDirection * stepLength * i;
                    real sampleDistance = length(samplePosition - camPos);
                    
                    UNITY_BRANCH
                    if (sampleDistance < depth && sampleDistance < surfaceDistance)
                    {
                        // attenuation
                        Light light = GetAdditionalLight(j, samplePosition, shadowMask);
                        real lightAttenuation = light.distanceAttenuation * light.shadowAttenuation;
                
                        UNITY_BRANCH
                        if (lightAttenuation > 0)
                        {
                            // noise
                            real noise = 0;
                        #ifdef _USENOISE0
                            real3 noisePosition0 = (samplePosition + _Noise0Offset) / _Noise0Scale + _Time.y * -_Noise0Speed;
                            real3 noise0 = SAMPLE_TEXTURE3D(_Noise0, sampler_Noise0, noisePosition0).r;
                            noise0 = lerp(_Noise0Remap.x, _Noise0Remap.y + 1, noise0);
                            noise += saturate(noise0).x;
                        #endif
                            
                        #ifdef _USENOISE1
                            real3 noisePosition1 = (samplePosition + _Noise1Offset) / _Noise1Scale + _Time.y * -_Noise1Speed;
                            real3 noise1 = SAMPLE_TEXTURE3D(_Noise1, sampler_Noise1, noisePosition1).r;
                            noise1 = lerp(_Noise1Remap.x, _Noise1Remap.y + 1, noise1);
                            noise += saturate(noise1).x;
                        #endif
                            
                        #ifndef _USENOISE0
                            #ifndef _USENOISE1
                                noise = 1;
                            #endif
                        #endif

                            // final combine
                            color += _Density * light.color * noise * lightAttenuation * (1 + boost);
                        }
                    }
                }
            }
        }
    }
    
    color = clamp(color, _Clamp.x, _Clamp.y);
#endif
}