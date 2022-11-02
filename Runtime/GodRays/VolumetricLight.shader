Shader "Hidden/VolumetricLight"
{
    Properties
    {
        //we need to have _MainTex written exactly like this because unity will pass the source render texture into _MainTex automatically 
        _MainTex ("Texture", 2D) = "white" {}

    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM

            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _  _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            //Boilerplate code, we aren't doind anything with our vertices or any other input info,
            // because technically we are working on a quad taking up the whole screen
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
                o.vertex = TransformWorldToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;

            //I set up these uniforms from the ScriptableRendererFeature
            real _Scattering;
            real3 _SunDirection = real3(-0.5, -0.5, -0.5);
            real _Steps;
            real _JitterVolumetric;
            real _MaxDistance;

            //This function will tell us if a certain point in world space coordinates is in light or shadow of the main light
            real ShadowAtten(real3 worldPosition)
            {
                return MainLightRealtimeShadow(TransformWorldToShadowCoord(worldPosition));
            }

            //Unity already has a function that can reconstruct world space position from depth
            real3 GetWorldPos(real2 uv){
                #if UNITY_REVERSED_Z
                    real depth = SampleSceneDepth(uv);
                #else
                    // Adjust z to match NDC for OpenGL
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
                #endif
                return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
            }

            // Mie scaterring approximated with Henyey-Greenstein phase function.
            real ComputeScattering(real lightDotView)
            {
                real result = 1.0f - _Scattering * _Scattering;
                result /= (4.0f * PI * pow(1.0f + _Scattering * _Scattering - (2.0f * _Scattering) *      lightDotView, 1.5f));
                return result;
            }

            //standart hash
            real random( real2 p ){
                return frac(sin(dot(p, real2(41, 289)))*45758.5453 )-0.5; 
            }
            real random01( real2 p ){
                return frac(sin(dot(p, real2(41, 289)))*45758.5453 ); 
            }
            
            //from Ronja https://www.ronja-tutorials.com/post/047-invlerp_remap/
            real invLerp(real from, real to, real value){
                return (value - from) / (to - from);
            }
            real remap(real origFrom, real origTo, real targetFrom, real targetTo, real value){
                real rel = invLerp(origFrom, origTo, value);
                return lerp(targetFrom, targetTo, rel);
            }

            //this implementation is loosely based on http://www.alexandre-pestana.com/volumetric-lights/ and https://fr.slideshare.net/BenjaminGlatzel/volumetric-lighting-for-many-lights-in-lords-of-the-fallen

            // #define MIN_STEPS 25

            real frag (v2f i) : SV_Target
            {
                //first we get the world space position of every pixel on screen
                real3 worldPos = GetWorldPos(i.uv);             

                //we find out our ray info, that depends on the distance to the camera
                real3 startPosition = _WorldSpaceCameraPos;
                real3 rayVector = worldPos- startPosition;
                real3 rayDirection =  normalize(rayVector);
                real rayLength = length(rayVector);

                rayLength = min(rayLength,_MaxDistance);
                worldPos= startPosition+rayDirection*rayLength;

                if(rayLength>_MaxDistance){
                    rayLength=_MaxDistance;
                    
                }

                //We can limit the amount of steps for close objects
                // steps= remap(0,_MaxDistance,MIN_STEPS,_Steps,rayLength);  
                //or
                // steps= remap(0,_MaxDistance,0,_Steps,rayLength);   
                // steps = max(steps,MIN_STEPS);

                real stepLength = rayLength / _Steps;
                real3 step = rayDirection * stepLength;
                
                //to eliminate banding we sample at diffent depths for every ray, this way we obfuscate the shadowmap patterns
                real rayStartOffset= random01( i.uv)*stepLength *_JitterVolumetric/100;
                real3 currentPosition = startPosition + rayStartOffset*rayDirection;

                real accumFog = 0;

                //we ask for the shadow map value at different depths, if the sample is in light we compute the contribution at that point and add it
                for (real j = 0; j < _Steps-1; j++)
                {
                    real shadowMapValue = ShadowAtten(currentPosition);
                    
                    //if it is in light
                    if(shadowMapValue>0){                       
                        real kernelColor = ComputeScattering(dot(rayDirection, _SunDirection)) ;
                        accumFog += kernelColor;
                    }
                    currentPosition += step;
                }
                //we need the average value, so we divide between the amount of samples 
                accumFog /= _Steps;
                
                return accumFog;
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
                o.vertex =  TransformWorldToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            int _GaussSamples;
            real _GaussAmount;
            //bilateral blur from 
            static const real gauss_filter_weights[] = { 0.14446445, 0.13543542, 0.11153505, 0.08055309, 0.05087564, 0.02798160, 0.01332457, 0.00545096} ;         
            #define BLUR_DEPTH_FALLOFF 100.0

            real frag (v2f i) : SV_Target
            {
                real col =0;
                real accumResult =0;
                real accumWeights=0;
                //depth at the current pixel
                real depthCenter;  
                #if UNITY_REVERSED_Z
                    depthCenter = SampleSceneDepth(i.uv);  
                #else
                    // Adjust z to match NDC for OpenGL
                    depthCenter = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(i.uv));
                #endif

                for(real index=-_GaussSamples;index<=_GaussSamples;index++){
                    //we offset our uvs by a tiny amount 
                    real2 uv= i.uv+real2(  index*_GaussAmount/1000,0);
                    //sample the color at that location
                    real kernelSample = tex2D(_MainTex, uv);
                    //depth at the sampled pixel
                    real depthKernel;
                    #if UNITY_REVERSED_Z
                        depthKernel = SampleSceneDepth(uv);
                    #else
                        // Adjust z to match NDC for OpenGL
                        depthKernel = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
                    #endif
                    //weight calculation depending on distance and depth difference
                    real depthDiff = abs(depthKernel-depthCenter);
                    real r2= depthDiff*BLUR_DEPTH_FALLOFF;
                    real g = exp(-r2*r2);
                    real weight = g * gauss_filter_weights[abs(index)];
                    //sum for every iteration of the color and weight of this sample 
                    accumResult+=weight*kernelSample;
                    accumWeights+=weight;
                }
                //final color
                col= accumResult/accumWeights;

                return col;
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
                o.vertex =  TransformWorldToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            int _GaussSamples;
            real _GaussAmount;
            #define BLUR_DEPTH_FALLOFF 100.0
            static const real gauss_filter_weights[] = { 0.14446445, 0.13543542, 0.11153505, 0.08055309, 0.05087564, 0.02798160, 0.01332457, 0.00545096 } ;


            real frag (v2f i) : SV_Target
            {
                real col =0;
                real accumResult =0;
                real accumWeights=0;
                
                if(_GaussAmount>0){
                    for(real index=-_GaussSamples;index<=_GaussSamples;index++){
                        real2 uv= i.uv+ real2 (0,index*_GaussAmount/1000);
                        real kernelSample = tex2D(_MainTex, uv);
                        real depthKernel ;
                        real depthCenter;  
                        #if UNITY_REVERSED_Z
                            depthCenter = SampleSceneDepth(i.uv);
                            depthKernel = SampleSceneDepth(uv);
                        #else
                            // Adjust z to match NDC for OpenGL
                            depthCenter = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(i.uv));
                            depthKernel = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
                        #endif
                        real depthDiff = abs(depthKernel-depthCenter);
                        real r2= depthDiff*BLUR_DEPTH_FALLOFF;
                        real g = exp(-r2*r2);
                        real weight = g * gauss_filter_weights[abs(index)];
                        accumResult+=weight*kernelSample;
                        accumWeights+=weight;
                    }
                    col=  accumResult/accumWeights;
                    
                }
                else{
                    col = tex2D(_MainTex,i.uv);
                }

                return col;
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
                o.vertex = TransformWorldToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }
            sampler2D _MainTex;
            TEXTURE2D (_volumetricTexture);
            SAMPLER(sampler_volumetricTexture);
            TEXTURE2D  (_LowResDepth);
            SAMPLER(sampler_LowResDepth);
            real4 _SunColor;
            real _Intensity;
            real _Downsample;

            

            real3 frag (v2f i) : SV_Target
            {
                real col = 0;
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
                switch(offset){
                    case 0:
                    col = _volumetricTexture.Sample(sampler_volumetricTexture, i.uv, int2(0, 1));
                    break;
                    case 1:
                    col = _volumetricTexture.Sample(sampler_volumetricTexture, i.uv, int2(0, -1));
                    break;
                    case 2:
                    col = _volumetricTexture.Sample(sampler_volumetricTexture, i.uv, int2(1, 0));
                    break;
                    case 3:
                    col = _volumetricTexture.Sample(sampler_volumetricTexture, i.uv, int2(-1, 0));
                    break;
                    default:
                    col =  _volumetricTexture.Sample(sampler_volumetricTexture, i.uv);
                    break;
                }


                real3 finalShaft =saturate (col)* normalize (_SunColor)*_Intensity;

                real3 screen = tex2D(_MainTex,i.uv);
                return screen+finalShaft;
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
                o.vertex = TransformWorldToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            real frag (v2f i) : SV_Target
            {
                #if UNITY_REVERSED_Z
                    real depth = SampleSceneDepth(i.uv);
                #else
                    // Adjust z to match NDC for OpenGL
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(i.uv));
                #endif
                return depth;
            }
            ENDHLSL
        }
    }
}