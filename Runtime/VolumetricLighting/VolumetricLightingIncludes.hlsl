real random01(real2 p)
{
    return frac(sin(dot(p, real2(41, 289))) * 45758.5453);
}

void AdditionalLightsContribution_float(real3 WorldPosition, real3 surfacePosition, real3 objectPosition, real3 objectScale, real depth, real2 uv, real blueNoise, out real3 color)
{
    color = 0;
    
#ifndef SHADERGRAPH_PREVIEW
    
    // raymarch positions/directions
    real3 camPos = _WorldSpaceCameraPos;
    real3 rayVec = WorldPosition - camPos;
    real3 rayDirection = normalize(rayVec);
    real rayLength = length(rayVec);
    real surfaceDistance = length(surfacePosition - camPos);
    
    // raymarch settings
    int stepCount = _FogVolumetricSteps;
    real stepLength = _FogVolumeStepLength;
    real maxDistance = _FogVolumeMaxDistance;
    
    // start raymarch near object
    real maxScale = max(max(objectScale.x, objectScale.y), objectScale.z);
    real radius = maxScale * 0.5;
    real objectDistance = length(objectPosition - camPos);
    real closePos = 0;
    UNITY_BRANCH
    if (objectDistance > radius)
    {
        closePos = length(objectDistance - radius);
    }

    // additional (point/spot) light count
    int pixelLightCount = GetAdditionalLightsCount();
    
    UNITY_LOOP
    for (int i = 0; i < stepCount; i++)
    {
        // offset sample position using blue noise jitter
        real jitter = blueNoise * stepLength * 2.5;
        real3 startPosition = camPos + rayDirection * (jitter + closePos);
        real3 samplePosition = startPosition + rayDirection * stepLength * i;
        real sampleDistance = length(samplePosition - camPos);
        real sampleMaxDistance = min(min(depth, surfaceDistance), maxDistance);
        
        UNITY_BRANCH    // check for depth
        if (sampleDistance < sampleMaxDistance)
        {
            real noise = 1;
            // clamped noise
            #ifdef _USENOISE0
                real3 scaleOffset = objectScale * 0.5;
                real3 noisePosition0 = (samplePosition - objectPosition + scaleOffset) / objectScale;
                real3 noise0 = SAMPLE_TEXTURE3D(_Noise0, sampler_Noise0, noisePosition0).r;
                noise0 = lerp(_Noise0Remap.x, _Noise0Remap.y, noise0);
                noise0 = clamp(noise0, 0, 1000);
                noise *= noise0.x;
            #endif
            
            // tiling noise
            #ifdef _USENOISE1
                real3 noisePosition1 = (samplePosition + _Noise1Offset) / _Noise1Scale + _Time.y * -_Noise1Speed;
                real3 noise1 = SAMPLE_TEXTURE3D(_Noise1, sampler_Noise1, noisePosition1).r;
                noise1 = lerp(_Noise1Remap.x, _Noise1Remap.y, noise1);
                noise1 = clamp(noise1, 0, 1000);
                noise *= noise1.x;
            #endif
            
            // fog accumulation
            color += _ShadowDensity * noise * _FogColor.rgb;
            
            // main directional light accumulation
            real4 mainLightShadowCoord = TransformWorldToShadowCoord(samplePosition);
            Light mainLight = GetMainLight(mainLightShadowCoord);
            real mainLightAttenuation = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
            color += _Density * mainLight.color * noise * mainLightAttenuation * _DirectionalLightStrength;
            
            // additional lights
            UNITY_LOOP
            for (int j = 0; j < pixelLightCount; j++)
            {
                int perObjectLightIndex = GetPerObjectLightIndex(j);
                real3 lightPosition = _AdditionalLightsPosition[perObjectLightIndex].xyz;
        
                UNITY_BRANCH
                if (length(lightPosition - objectPosition) < _MaxLightDistance)
                {
                    // add additional density per light type
                    real4 shadowParams = GetAdditionalLightShadowParams(perObjectLightIndex);
                    real isPointLight = max(shadowParams.z, 1 - shadowParams.x);
                    real boost = lerp(_SpotLightBoost, _PointLightBoost, isPointLight);
        
                    // attenuation
                    Light light = GetAdditionalLight(j, samplePosition, real4(1, 1, 1, 1));
                    real lightAttenuation = light.distanceAttenuation * light.shadowAttenuation;
                
                    // additional light accumulation
                    color += _Density * light.color * noise * lightAttenuation * (1 + boost);
                }
            }
        }
    }
    
    color = clamp(color, _Clamp.x, _Clamp.y);
#endif
}