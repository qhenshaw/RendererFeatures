
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

void GetAdditionalLights_float(float3 worldPosition, out float total)
{
#ifdef SHADERGRAPH_PREVIEW
    attenuation = 1;
#endif
    
#if !defined(_ADDITIONAL_LIGHT_SHADOWS)
    total = 1;
#endif
    
    uint lightCount = GetAdditionalLightsCount();
    
    //for (uint i = 0; i < 1; i++)
    {
        float attenuation = 0;
        
        int lightIndex = 0;
        Light light = GetAdditionalLight(lightIndex, worldPosition);
        ShadowSamplingData shadowSamplingData = GetAdditionalLightShadowSamplingData();
        half4 shadowParams = GetAdditionalLightShadowParams(lightIndex);
    
        int shadowSliceIndex = shadowParams.w;
 
        UNITY_BRANCH
        if (shadowSliceIndex < 0)
        {
            attenuation = 1.0;
            return;
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
        total = min(total, attenuation);
    }
    
    //attenuation = SampleShadowmap(shadowCoord, TEXTURE2D_ARGS(_AdditionalLightsShadowmapTexture, sampler_AdditionalLightsShadowmapTexture), shadowSamplingData, 1, false);
    //int pixelLightCount = GetAdditionalLightsCount();
    //for (int i = 0; i < pixelLightCount; ++i)
    //{
    //    Light light = GetAdditionalLight(i, worldPosition);
    //    attenuation = SampleShadowmap(shadowCoord, TEXTURE2D_ARGS(_AdditionalLightsShadowmapTexture, sampler_AdditionalLightsShadowmapTexture), shadowSamplingData, 1, false);

    //}
}