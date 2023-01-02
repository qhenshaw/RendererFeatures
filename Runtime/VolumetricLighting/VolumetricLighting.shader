Shader "Hidden/VolumetricLighting"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Raymarch"

            HLSLPROGRAM

            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _  _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct appdata
            {
                real4 vertex : POSITION;
                real2 uv : TEXCOORD0;
            };

            struct v2f
            {
                real2 uv : TEXCOORD0;
                real4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformWorldToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;

            real _Scattering;
            real3 _SunDirection = real3(-0.5, -0.5, -0.5);
            real _Steps;
            real _JitterVolumetric;
            real _MaxDistance;
            real4 _Tint;
            real _Intensity;

            real ShadowAtten(real3 worldPosition)
            {
                return MainLightRealtimeShadow(TransformWorldToShadowCoord(worldPosition));
            }

            real3 GetWorldPos(real2 uv){
                #if UNITY_REVERSED_Z
                    real depth = SampleSceneDepth(uv);
                #else
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
                #endif
                return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
            }

            // Mie scaterring approximated with Henyey-Greenstein phase function.
            real ComputeScattering(real lightDotView)
            {
                real result = 1.0f - _Scattering * _Scattering;
                result /= (4.0f * PI * pow(abs(1.0f + _Scattering * _Scattering - (2.0f * _Scattering) * lightDotView), 1.5f));
                return result;
            }

            real random( real2 p ){
                return frac(sin(dot(p, real2(41, 289)))*45758.5453 )-0.5; 
            }
            real random01( real2 p ){
                return frac(sin(dot(p, real2(41, 289)))*45758.5453 ); 
            }
            
            real invLerp(real from, real to, real value){
                return (value - from) / (to - from);
            }
            real remap(real origFrom, real origTo, real targetFrom, real targetTo, real value){
                real rel = invLerp(origFrom, origTo, value);
                return lerp(targetFrom, targetTo, rel);
            }

            real3 frag (v2f i) : SV_Target
            {
                real3 worldPos = GetWorldPos(i.uv);             

                real3 startPosition = _WorldSpaceCameraPos;
                real3 rayVector = worldPos- startPosition;
                real3 rayDirection =  normalize(rayVector);
                real rayLength = length(rayVector);

                rayLength = min(rayLength,_MaxDistance);
                worldPos= startPosition+rayDirection*rayLength;

                if(rayLength>_MaxDistance)
                {
                    rayLength=_MaxDistance; 
                }

                real stepLength = rayLength / _Steps;
                real3 step = rayDirection * stepLength;
                
                real rayStartOffset= random01( i.uv)*stepLength *_JitterVolumetric/100;
                real3 currentPosition = startPosition + rayStartOffset*rayDirection;

                real accumFog = 0;

                UNITY_LOOP
                for (real j = 0; j < _Steps-1; j++)
                {
                    real shadowMapValue = ShadowAtten(currentPosition);
                    
                    UNITY_BRANCH
                    if(shadowMapValue>0)
                    {                       
                        real kernelColor = ComputeScattering(dot(rayDirection, _SunDirection)) ;
                        accumFog += kernelColor;
                    }
                    currentPosition += step;
                }
                accumFog /= _Steps;
                
                return accumFog * _Intensity * _Tint.xyz;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Gaussian Blur x"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct appdata
            {
                real4 vertex : POSITION;
                real2 uv : TEXCOORD0;
            };

            struct v2f
            {
                real2 uv : TEXCOORD0;
                real4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex =  TransformWorldToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            int _GaussSamples;
            real _GaussAmount;
            #define BLUR_DEPTH_FALLOFF 100.0
            static const real gauss_filter_weights[] = { 0.14446445, 0.13543542, 0.11153505, 0.08055309, 0.05087564, 0.02798160, 0.01332457, 0.00545096} ;         

            real3 frag (v2f i) : SV_Target
            {
                real3 col =0;
                real3 accumResult =0;
                real accumWeights=0;

                real depthCenter;  
                #if UNITY_REVERSED_Z
                    depthCenter = SampleSceneDepth(i.uv);  
                #else
                    depthCenter = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(i.uv));
                #endif

                UNITY_LOOP
                for(real index=-_GaussSamples;index<=_GaussSamples;index++)
                {
                    real2 uv= i.uv+real2(  index*_GaussAmount/1000,0);
                    real3 kernelSample = tex2D(_MainTex, uv).xyz;
                    real depthKernel;
                    #if UNITY_REVERSED_Z
                        depthKernel = SampleSceneDepth(uv);
                    #else
                        depthKernel = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
                    #endif
                    real depthDiff = abs(depthKernel-depthCenter);
                    real r2= depthDiff*BLUR_DEPTH_FALLOFF;
                    real g = exp(-r2*r2);
                    real weight = g * gauss_filter_weights[abs(index)];
                    accumResult+=weight*kernelSample;
                    accumWeights+=weight;
                }

                return accumResult/accumWeights;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Gaussian Blur y"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct appdata
            {
                real4 vertex : POSITION;
                real2 uv : TEXCOORD0;
            };

            struct v2f
            {
                real2 uv : TEXCOORD0;
                real4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex =  TransformWorldToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            int _GaussSamples;
            real _GaussAmount;
            #define BLUR_DEPTH_FALLOFF 100.0
            static const real gauss_filter_weights[] = { 0.14446445, 0.13543542, 0.11153505, 0.08055309, 0.05087564, 0.02798160, 0.01332457, 0.00545096 } ;

            real3 frag (v2f i) : SV_Target
            {
                real3 col =0;
                real3 accumResult =0;
                real accumWeights=0;
                real depthCenter;
                #if UNITY_REVERSED_Z
                     depthCenter = SampleSceneDepth(i.uv);
                #else
                    depthCenter = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(i.uv));
                #endif
                
                UNITY_LOOP
                for (real index = -_GaussSamples; index <= _GaussSamples; index++)
                {
                    real2 uv = i.uv + real2(0, index * _GaussAmount / 1000);
                    real3 kernelSample = tex2D(_MainTex, uv).xyz;
                    real depthKernel;
                    #if UNITY_REVERSED_Z
                        depthKernel = SampleSceneDepth(uv);
                    #else
                        depthKernel = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
                    #endif
                    real depthDiff = abs(depthKernel - depthCenter);
                    real r2 = depthDiff * BLUR_DEPTH_FALLOFF;
                    real g = exp(-r2 * r2);
                    real weight = g * gauss_filter_weights[abs(index)];
                    accumResult += weight * kernelSample;
                    accumWeights += weight;
                }
                return accumResult / accumWeights;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Compositing"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
       

            struct appdata
            {
                real4 vertex : POSITION;
                real2 uv : TEXCOORD0;
            };

            struct v2f
            {
                real2 uv : TEXCOORD0;
                real4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformWorldToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }
            sampler2D _MainTex;
            TEXTURE2D (_combinedVolumetric);
            SAMPLER(sampler_combinedVolumetric);
            TEXTURE2D  (_LowResDepth);
            SAMPLER(sampler_LowResDepth);
            real _Downsample;

            real3 frag (v2f i) : SV_Target
            {
                real3 col = 0;
                //based on https://eleni.mutantstargoat.com/hikiko/on-depth-aware-upsampling/ 

                int offset =0;
                real d0 = SampleSceneDepth(i.uv);

                /* calculating the distances between the depths of the pixels
                * in the lowres neighborhood and the full res depth value
                * (texture offset must be compile time constant and so we
                * can't use a loop)
                */
                real d1 = _LowResDepth.Sample(sampler_LowResDepth, i.uv, int2(0, 1)).x;
                real d2 = _LowResDepth.Sample(sampler_LowResDepth, i.uv, int2(0, -1)).x;
                real d3 =_LowResDepth.Sample(sampler_LowResDepth, i.uv, int2(1, 0)).x;
                real d4 = _LowResDepth.Sample(sampler_LowResDepth, i.uv, int2(-1, 0)).x;

                d1 = abs(d0 - d1);
                d2 = abs(d0 - d2);
                d3 = abs(d0 - d3);
                d4 = abs(d0 - d4);

                real dmin = min(min(d1, d2), min(d3, d4));

                if (dmin == d1)
                offset= 0;

                else if (dmin == d2)
                offset= 1;

                else if (dmin == d3)
                offset= 2;

                else  if (dmin == d4)
                offset= 3;

                col =0;
                switch(offset)
                {
                    case 0:
                    col += _combinedVolumetric.Sample(sampler_combinedVolumetric, i.uv, int2(0, 1)).xyz;
                    break;
                    case 1:
                    col += _combinedVolumetric.Sample(sampler_combinedVolumetric, i.uv, int2(0, -1)).xyz;
                    break;
                    case 2:
                    col += _combinedVolumetric.Sample(sampler_combinedVolumetric, i.uv, int2(1, 0)).xyz;
                    break;
                    case 3:
                    col += _combinedVolumetric.Sample(sampler_combinedVolumetric, i.uv, int2(-1, 0)).xyz;
                    break;
                    default:
                    col += _combinedVolumetric.Sample(sampler_combinedVolumetric, i.uv).xyz;
                    break;
                }

                real3 screen = tex2D(_MainTex,i.uv).xyz;
                return screen + col;
            }
            ENDHLSL
        }
        Pass
        {
            Name "SampleDepth"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"


            struct appdata
            {
                real4 vertex : POSITION;
                real2 uv : TEXCOORD0;
            };

            struct v2f
            {
                real2 uv : TEXCOORD0;
                real4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformWorldToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            real frag (v2f i) : SV_Target
            {
                #if UNITY_REVERSED_Z
                    real depth = SampleSceneDepth(i.uv);
                #else
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(i.uv));
                #endif
                return depth;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Volumetric Combine"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature DIRECTIONAL_LIGHT_VOLUMETRICS
            #pragma shader_feature ADDITIONAL_LIGHTS_VOLUMETRICS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct appdata
            {
                real4 vertex : POSITION;
                real2 uv : TEXCOORD0;
            };

            struct v2f
            {
                real2 uv : TEXCOORD0;
                real4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformWorldToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            TEXTURE2D(_mainLightVolumetric);
            SAMPLER(sampler_mainLightVolumetric);
            TEXTURE2D(_additionalLightsVolumetric);
            SAMPLER(sampler_additionalLightsVolumetric);

            real3 frag(v2f i) : SV_Target
            {
                real3 mainLight = real3(0,0,0);
                real3 additionalLights = real3(0, 0, 0);
#ifdef DIRECTIONAL_LIGHT_VOLUMETRICS
                mainLight = SAMPLE_TEXTURE2D(_mainLightVolumetric, sampler_mainLightVolumetric, i.uv).xyz;
#endif
#ifdef ADDITIONAL_LIGHTS_VOLUMETRICS
                additionalLights = SAMPLE_TEXTURE2D(_additionalLightsVolumetric, sampler_additionalLightsVolumetric, i.uv).xyz;
#endif

                return mainLight + additionalLights;
        }
        ENDHLSL
        }
    }
}