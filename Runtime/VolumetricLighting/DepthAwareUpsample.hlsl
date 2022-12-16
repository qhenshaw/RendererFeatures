void DepthAwareUpsample_float(float2 UV, float d0, UnityTexture2D tex, UnityTexture2D lowResDepth, UnitySamplerState SS, out float3 result)
{
    float2 step = -float2(tex.texelSize.x, tex.texelSize.y) * 0.25;
    
    float2 up = float2(0, 1);
    float2 right = float2(1, 0);
    float2 down = float2(0, -1);
    float2 left = float2(-1, 0);
    
    real d1 = SAMPLE_TEXTURE2D(lowResDepth, SS, UV + step.y * up).r;
    real d2 = SAMPLE_TEXTURE2D(lowResDepth, SS, UV + step.x * right).r;
    real d3 = SAMPLE_TEXTURE2D(lowResDepth, SS, UV + step.y * down).r;
    real d4 = SAMPLE_TEXTURE2D(lowResDepth, SS, UV + step.x * left).r ;
    
    d1 = abs(d0 - d1);
    d2 = abs(d0 - d2);
    d3 = abs(d0 - d3);
    d4 = abs(d0 - d4);
    
    real dmin = min(min(d1, d2), min(d3, d4));
    
    int offset;
        
    UNITY_BRANCH
    if (dmin == d1)
        offset = 0;
    else if (dmin == d2)
        offset = 1;
    else if (dmin == d3)
        offset = 2;
    else if (dmin == d4)
        offset = 3;
    
    real3 col = real3(0, 0, 0);
    UNITY_BRANCH
    switch (offset)
    {
        case 0:
            col = SAMPLE_TEXTURE2D(tex, SS, UV + step.y * up).rgb;
            break;
        case 1:
            col = SAMPLE_TEXTURE2D(tex, SS, UV + step.x * right).rgb;
            break;
        case 2:
            col = SAMPLE_TEXTURE2D(tex, SS, UV + step.y * down).rgb;
            break;
        case 3:
            col = SAMPLE_TEXTURE2D(tex, SS, UV + step.x * left).rgb;
            break;
        default:
            col = SAMPLE_TEXTURE2D(tex, SS, UV).rgb;
            break;
    }
    
    result = col;
}