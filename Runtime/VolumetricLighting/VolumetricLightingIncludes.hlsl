real random01(real2 p)
{
    return frac(sin(dot(p, real2(41, 289))) * 45758.5453);
}

void AdditionalLightsContribution_float(float3 WorldPosition, float3 surfacePosition, float3 objectPosition, float3 objectScale, float depth, float2 uv, float noise, out float3 color)
{
    color = 0;
    
#ifndef SHADERGRAPH_PREVIEW    
    real3 camPos = _WorldSpaceCameraPos;
    real3 rayVec = WorldPosition - camPos;
    real3 rayDirection = normalize(rayVec);
    real rayLength = length(rayVec);
    real surfaceDistance = length(surfacePosition - camPos);
    int stepCount = 64;
    real maxDistance = 500;
    real minStepLength = 0.05;
    real maxStepLength = 0.2;
    real stepLengthDistance = 2;
    real clampedDepth = min(depth, maxDistance);
    real scaledStep = clampedDepth / stepCount;
    real4 shadowMask = real4(1, 1, 1, 1);
    
    int pixelLightCount = GetAdditionalLightsCount();
    
    UNITY_LOOP
    for (int i = 0; i < stepCount; i++)
    {
        real stepLength = 0.2;
        real jitter = noise * stepLength * 2.5;
        real rayMaxLength = stepCount * stepLength;
        real offset = clamp(rayLength - rayMaxLength, 0, maxDistance);
        //if (rayLength > surfaceDistance)
        //{
        //    offset = 0;
        //}
        offset = 0;
        real3 startPosition = camPos + rayDirection * (jitter + offset);
        real startDepth = length(startPosition - camPos);
        real3 samplePosition = startPosition + rayDirection * stepLength * i;
        real sampleDistance = length(samplePosition - camPos);
        
        UNITY_BRANCH
        if (sampleDistance < depth && sampleDistance < surfaceDistance)
        {
            // noise
            real noise = 1;
            #ifdef _USENOISE0
                real3 scaleOffset = objectScale * 0.5;
                real3 noisePosition0 = (samplePosition + _Noise0Offset - objectPosition + scaleOffset) / objectScale + _Time.y * -_Noise0Speed;
                real3 noise0 = SAMPLE_TEXTURE3D(_Noise0, sampler_Noise0, noisePosition0).r;
                noise0 = lerp(_Noise0Remap.x, _Noise0Remap.y, noise0);
                noise0 = clamp(noise0, 0, 1000);
                noise *= noise0.x;
            #endif
                            
            #ifdef _USENOISE1
                real3 noisePosition1 = (samplePosition + _Noise1Offset) / _Noise1Scale + _Time.y * -_Noise1Speed + objectPosition;
                real3 noise1 = SAMPLE_TEXTURE3D(_Noise1, sampler_Noise1, noisePosition1).r;
                noise1 = lerp(_Noise1Remap.x, _Noise1Remap.y, noise1);
                noise1 = clamp(noise1, 0, 1000);
                noise *= noise1.x;
            #endif
            
            color += _ShadowDensity * noise * _FogColor.rgb;
            
            UNITY_LOOP
            for (int j = 0; j < pixelLightCount; j++)
            {
                int perObjectLightIndex = GetPerObjectLightIndex(j);
                real3 lightPosition = _AdditionalLightsPosition[perObjectLightIndex].xyz;
        
                UNITY_BRANCH
                if (length(lightPosition - objectPosition) < _MaxLightDistance)
                {
                    real4 shadowParams = GetAdditionalLightShadowParams(perObjectLightIndex);
                    real pointLightCorrection = max(shadowParams.z, 1 - shadowParams.x);
                    real boost = lerp(_SpotLightBoost, _PointLightBoost, pointLightCorrection);
                    real lightDistance = length(lightPosition - camPos);
                //real stepLength = lerp(minStepLength, maxStepLength, saturate(lightDistance / stepLengthDistance));
                    real4 distanceAndSpotAttenuation = _AdditionalLightsAttenuation[perObjectLightIndex];
                    real lightRange = rsqrt(distanceAndSpotAttenuation.x);
                    lightRange = clamp(lightRange, 0, stepCount * stepLength);
        
                    // attenuation
                    Light light = GetAdditionalLight(j, samplePosition, shadowMask);
                    real lightAttenuation = light.distanceAttenuation * light.shadowAttenuation;
                
                    UNITY_BRANCH
                    if (lightAttenuation > 0)
                    {
                            // final combine
                        color += _Density * light.color * noise * lightAttenuation * (1 + boost);
                    }
                }
            }
        }
    }
    
    color = clamp(color, _Clamp.x, _Clamp.y);
#endif
}