// functionality cobbled together from multiple sources, much thanks to Ben Golus
// https://gist.github.com/bgolus/a07ed65602c009d5e2f753826e8078a0

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

float4 _CameraDepthTexture_TexelSize;

// get z buffer
float getRawDepth(float2 UV)
{
    return SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, UV);
}

// Z buffer to linear 0..1 depth
inline float Linear01Depth(float z)
{
    return 1.0 / (_ZBufferParams.x * z + _ZBufferParams.y);
}

// inspired by keijiro's depth inverse projection
// https://github.com/keijiro/DepthInverseProjection
// constructs view space ray at the far clip plane from the screen uv
// then multiplies that ray by the linear 01 depth
float3 viewSpacePosAtScreenUV(float2 uv)
{
    float3 viewSpaceRay = mul(unity_CameraInvProjection, float4(uv * 2.0 - 1.0, 1.0, 1.0) * _ProjectionParams.z);
    float rawDepth = getRawDepth(uv);
    return viewSpaceRay * Linear01Depth(rawDepth);
}

float3 viewSpacePosAtPixelPosition(float2 vpos)
{
    float2 uv = vpos * _CameraDepthTexture_TexelSize.xy;
    return viewSpacePosAtScreenUV(uv);
}

// based on Yuwen Wu's Accurate Normal Reconstruction 
// https://atyuwen.github.io/posts/normal-reconstruction/
// basically as accurate as you can get!
// no artifacts on depth disparities
// no artifacts on edges
// artifacts on triangles that are <3 pixels across
float3 viewNormal(float2 uv)
{
    // screen uv from vpos
    // float2 uv = vpos * _CameraDepthTexture_TexelSize.xy;

    // current pixel's depth
    float c = getRawDepth(uv);

    // get current pixel's view space position
    half3 viewSpacePos_c = viewSpacePosAtScreenUV(uv);

    // get view space position at 1 pixel offsets in each major direction
    half3 viewSpacePos_l = viewSpacePosAtScreenUV(uv + float2(-1.0, 0.0) * _CameraDepthTexture_TexelSize.xy);
    half3 viewSpacePos_r = viewSpacePosAtScreenUV(uv + float2(1.0, 0.0) * _CameraDepthTexture_TexelSize.xy);
    half3 viewSpacePos_d = viewSpacePosAtScreenUV(uv + float2(0.0, -1.0) * _CameraDepthTexture_TexelSize.xy);
    half3 viewSpacePos_u = viewSpacePosAtScreenUV(uv + float2(0.0, 1.0) * _CameraDepthTexture_TexelSize.xy);

    // get the difference between the current and each offset position
    half3 l = viewSpacePos_c - viewSpacePos_l;
    half3 r = viewSpacePos_r - viewSpacePos_c;
    half3 d = viewSpacePos_c - viewSpacePos_d;
    half3 u = viewSpacePos_u - viewSpacePos_c;

    // get depth values at 1 & 2 pixels offsets from current along the horizontal axis
    half4 H = half4(
        getRawDepth(uv + float2(-1.0, 0.0) * _CameraDepthTexture_TexelSize.xy),
        getRawDepth(uv + float2(1.0, 0.0) * _CameraDepthTexture_TexelSize.xy),
        getRawDepth(uv + float2(-2.0, 0.0) * _CameraDepthTexture_TexelSize.xy),
        getRawDepth(uv + float2(2.0, 0.0) * _CameraDepthTexture_TexelSize.xy)
    );

    // get depth values at 1 & 2 pixels offsets from current along the vertical axis
    half4 V = half4(
        getRawDepth(uv + float2(0.0, -1.0) * _CameraDepthTexture_TexelSize.xy),
        getRawDepth(uv + float2(0.0, 1.0) * _CameraDepthTexture_TexelSize.xy),
        getRawDepth(uv + float2(0.0, -2.0) * _CameraDepthTexture_TexelSize.xy),
        getRawDepth(uv + float2(0.0, 2.0) * _CameraDepthTexture_TexelSize.xy)
    );

    // current pixel's depth difference from slope of offset depth samples
    // differs from original article because we're using non-linear depth values
    // see article's comments
    half2 he = abs((2 * H.xy - H.zw) - c);
    half2 ve = abs((2 * V.xy - V.zw) - c);

    // pick horizontal and vertical diff with the smallest depth difference from slopes
    half3 hDeriv = he.x < he.y ? l : r;
    half3 vDeriv = ve.x < ve.y ? d : u;

    // get view space normal from the cross product of the best derivatives
    half3 viewNormal = normalize(cross(hDeriv, vDeriv));

    return viewNormal;
}

// outline method from:
// https://alexanderameye.github.io/notes/edge-detection-outlines/
void Outline_float(float2 UV, float OutlineThickness, float DepthSensitivity, float NormalsSensitivity, float ColorSensitivity, float4 OutlineColor, out float4 Combined, out float4 Outline)
{
    float halfScaleFloor = floor(OutlineThickness * 0.5);
    float halfScaleCeil = ceil(OutlineThickness * 0.5);
    float2 Texel = (1.0) / float2(_MainTex_TexelSize.z, _MainTex_TexelSize.w);

    float2 uvSamples[4];
    float depthSamples[4];
    float3 normalSamples[4], colorSamples[4];

    uvSamples[0] = UV - float2(Texel.x, Texel.y) * halfScaleFloor;
    uvSamples[1] = UV + float2(Texel.x, Texel.y) * halfScaleCeil;
    uvSamples[2] = UV + float2(Texel.x * halfScaleCeil, -Texel.y * halfScaleFloor);
    uvSamples[3] = UV + float2(-Texel.x * halfScaleFloor, Texel.y * halfScaleCeil);

    for(int i = 0; i < 4 ; i++)
    {
        depthSamples[i] = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, uvSamples[i]).r;
        normalSamples[i] = viewNormal(uvSamples[i]);
        colorSamples[i] = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvSamples[i]);
    }
    
    // surface dot product with camera
    float3 camDir = -1 * mul(UNITY_MATRIX_M, transpose(mul(UNITY_MATRIX_I_M, UNITY_MATRIX_I_V))[2].xyz);
    float3 normals = viewNormal(UV);
    float camDot = dot(normals, camDir);
    float nonParallelMask = pow(saturate(abs(camDot)), 3);

    // Depth
    float depthFiniteDifference0 = depthSamples[1] - depthSamples[0];
    float depthFiniteDifference1 = depthSamples[3] - depthSamples[2];
    float edgeDepth = sqrt(pow(depthFiniteDifference0, 2) + pow(depthFiniteDifference1, 2)) * 100;
    edgeDepth *= nonParallelMask;
    float depthThreshold = (1/DepthSensitivity) * depthSamples[0];
    edgeDepth = edgeDepth > depthThreshold ? 1 : 0;

    // Normals
    float3 normalFiniteDifference0 = normalSamples[1] - normalSamples[0];
    float3 normalFiniteDifference1 = normalSamples[3] - normalSamples[2];
    float edgeNormal = sqrt(dot(normalFiniteDifference0, normalFiniteDifference0) + dot(normalFiniteDifference1, normalFiniteDifference1));
    edgeNormal = edgeNormal > (1/NormalsSensitivity) ? 1 : 0;

    // Color
    float3 colorFiniteDifference0 = colorSamples[1] - colorSamples[0];
    float3 colorFiniteDifference1 = colorSamples[3] - colorSamples[2];
    float edgeColor = sqrt(dot(colorFiniteDifference0, colorFiniteDifference0) + dot(colorFiniteDifference1, colorFiniteDifference1));
	edgeColor = edgeColor > (1/ColorSensitivity) ? 1 : 0;

    float edge = max(edgeDepth, max(edgeNormal, edgeColor));

    float4 original = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvSamples[0]);	
    Combined = ((1 - edge) * original) + (edge * lerp(original, OutlineColor,  OutlineColor.a));
    Outline = ((1 - edge) * float4(1, 1, 1, 1)) + (edge * lerp(float4(1,1,1,1), OutlineColor, OutlineColor.a));
}