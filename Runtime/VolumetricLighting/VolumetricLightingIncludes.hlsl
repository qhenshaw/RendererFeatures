
// #pragma multi_compile _MAIN_LIGHT_SHADOWS_CASCADE
// #pragma multi_compile _SHADOWS_SOFT

void GetMainLight_float( float3 worldoPos, out float3 Direction , out float3 Color, out float ShadowAtten){
    #ifdef SHADERGRAPH_PREVIEW
        Direction = float3(1,1,1);
        Color = float3(1,1,1);
        ShadowAtten = 1.0f;
    #else
    
    //shadow Coord 만들기 
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    half4 clipPos = TransformWorldToHClip(worldPos);
    half4 shadowCoord =  ComputeScreenPos(clipPos);
    #else
    half4 shadowCoord =  TransformWorldToShadowCoord(worldoPos);
    #endif
     
    Light light = GetMainLight();
    Direction = light.direction;
    Color = light.color;

    //메인라이트가 없거나 리시브 셰도우 오프가 되어 있을때 
    #if !defined(_MAIN_LIGHT_SHADOWS)
        ShadowAtten = 1.0f;
    #endif

    //ShadowAtten 받아와서 만들기 
    #if SHADOWS_SCREEN
        ShadowAtten = SampleScreenSpaceShadowmap(shadowCoord);
    #else
        ShadowSamplingData shadowSamplingData = GetMainLightShadowSamplingData();
        half shadowStrength = GetMainLightShadowStrength();
        ShadowAtten = SampleShadowmap(shadowCoord, TEXTURE2D_ARGS(_MainLightShadowmapTexture, sampler_MainLightShadowmapTexture), shadowSamplingData, shadowStrength, false);
    #endif

    #endif
}

void GetAdditionalLightAttenuation(int index, float3 worldPosition, out float attenuation, out float3 color)
{
    attenuation = 0;
        
    Light light = GetAdditionalLight(index, worldPosition);
    ShadowSamplingData shadowSamplingData = GetAdditionalLightShadowSamplingData();
    half4 shadowParams = GetAdditionalLightShadowParams(index);
    
    int shadowSliceIndex = shadowParams.w;
 
    UNITY_BRANCH
    if (shadowSliceIndex < 0)
    {
        attenuation = 1.0;
    }
 
    half isPointLight = shadowParams.z;
 
    UNITY_BRANCH
    if (isPointLight)
    {
        // This is a point light, we have to find out which shadow slice to sample from
        float cubemapFaceId = CubeMapFaceID(-light.direction);
        shadowSliceIndex += cubemapFaceId;
    }
    
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    float4 shadowCoord = mul(_AdditionalLightsWorldToShadow_SSBO[shadowSliceIndex], float4(worldPosition, 1.0));
#else
    float4 shadowCoord = mul(_AdditionalLightsWorldToShadow[shadowSliceIndex], float4(worldPosition, 1.0));
#endif
 
    float shadow = SampleShadowmap(TEXTURE2D_ARGS(_AdditionalLightsShadowmapTexture, sampler_AdditionalLightsShadowmapTexture), shadowCoord, shadowSamplingData, shadowParams, true);
    attenuation = shadow * light.distanceAttenuation;
    color = light.color;
}

float4 _PixelLightPositions[16];
float _PixelLightRanges[16];
float4 _PixelLightColors[16];

void GetAdditionalLightsVolumetric_float(float3 worldPosition, float count, float depth, out float3 lightColor, out float total)
{
#ifdef SHADERGRAPH_PREVIEW
    attenuation = 1;
#endif
    
    total = 0;
    
#if !defined(_ADDITIONAL_LIGHT_SHADOWS)
    total = 1;
#endif
    
    int lightCount = (uint) count;
    float3 camPos = _WorldSpaceCameraPos;
    float3 rayDirection = normalize(worldPosition - camPos);
    float depthMax = 15;
    float density = 0.005;
    int stepCount = 64;
    float fragmentDistance = min(depthMax, depth);
    float stepLength = fragmentDistance / stepCount;
    
    for (int i = 0; i < stepCount; i++)
    {
        for (int j = 0; j < lightCount; j++)
        {
            float3 samplePosition = camPos + rayDirection * stepLength * i;
            float attenuation;
            float3 color;
            GetAdditionalLightAttenuation(j, samplePosition, attenuation, color);
            lightColor += color / stepCount;
            total += attenuation * density;
        }
    }
    
    //for (int i = 0; i < stepCount; i++)
    //{
    //    for (int j = 0; j < lightCount; j++)
    //    {
    //        float3 samplePosition = camPos + rayDirection * stepLength * i;
    //        float attenuation;
    //        float3 color;
    //        //GetAdditionalLightAttenuation(j, samplePosition, attenuation, color);
    //        float3 position = _PixelLightPositions[j].rgb;
    //        float range = _PixelLightRanges[j];
    //        float distance = length(samplePosition - position);
    //        attenuation = pow(1 - saturate(distance / range), 1);
    //        color = _PixelLightColors[j].rgb;
    //        lightColor += color / stepCount;
    //        total += attenuation * density;
    //    }
    //}
    
    total = saturate(total);
}

void GetAdditionalLightVolumetric_float(float3 worldPosition, float lightIndex, float depth, out float3 lightColor, out float total)
{
#ifdef SHADERGRAPH_PREVIEW
    attenuation = 1;
#endif
    
    total = 0;
    
#if !defined(_ADDITIONAL_LIGHT_SHADOWS)
    total = 1;
#endif
    
    float3 camPos = _WorldSpaceCameraPos;
    float3 rayDirection = normalize(worldPosition - camPos);
    float depthMax = 15;
    float density = 0.005;
    int stepCount = 64;
    float fragmentDistance = min(depthMax, depth);
    float stepLength = fragmentDistance / stepCount;
    
    for (int i = 0; i < stepCount; i++)
    {
        float3 samplePosition = camPos + rayDirection * stepLength * i;
        float attenuation;
        float3 color;
        GetAdditionalLightAttenuation(lightIndex, samplePosition, attenuation, color);
        lightColor += color / stepCount;
        total += attenuation * density;
    }
    
    total = saturate(total);
}