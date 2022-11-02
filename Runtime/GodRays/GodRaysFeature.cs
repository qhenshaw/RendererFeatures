using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GodRaysFeature : ScriptableRendererFeature
{
    public enum DownSample { off = 1, half = 2, third = 3, quarter = 4 };

    class GodRaysPass : ScriptableRenderPass
    {
        public Settings settings;
        public RenderTargetIdentifier Source;
        RenderTargetHandle tempTexture;
        RenderTargetHandle lowResDepthRT;
        RenderTargetHandle temptexture3;

        string profilerTag;

        public GodRaysPass(string profilerTag)
        {
            this.profilerTag = profilerTag;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            Source = renderingData.cameraData.renderer.cameraColorTarget;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var original = cameraTextureDescriptor;
            int divider = (int)settings.downsampling;

            if (Camera.current != null) //This is necessary so it uses the proper resolution in the scene window
            {
                cameraTextureDescriptor.width = (int)Camera.current.pixelRect.width / divider;
                cameraTextureDescriptor.height = (int)Camera.current.pixelRect.height / divider;
                original.width = (int)Camera.current.pixelRect.width;
                original.height = (int)Camera.current.pixelRect.height;
            }
            else //regular game window
            {
                cameraTextureDescriptor.width /= divider;
                cameraTextureDescriptor.height /= divider;
            }

            //R8 has noticeable banding
            cameraTextureDescriptor.colorFormat = RenderTextureFormat.R16;
            //we dont need to resolve AA in every single Blit
            cameraTextureDescriptor.msaaSamples = 1;
            //we need to assing a different id for every render texture
            lowResDepthRT.id = 1;
            temptexture3.id = 2;

            cmd.GetTemporaryRT(tempTexture.id, cameraTextureDescriptor);
            ConfigureTarget(tempTexture.Identifier());
            cmd.GetTemporaryRT(lowResDepthRT.id, cameraTextureDescriptor);
            ConfigureTarget(lowResDepthRT.Identifier());
            cmd.GetTemporaryRT(temptexture3.id, original);
            ConfigureTarget(temptexture3.Identifier());
            ConfigureClear(ClearFlag.All, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(profilerTag);
            cmd.Clear();

            //it is very important that if something fails our code still calls
            //CommandBufferPool.Release(cmd) or we will have a HUGE memory leak
            try
            {

                if (settings.material == null) settings.material = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/VolumetricLight"));
                settings.material.SetFloat("_Scattering", settings.scattering);
                settings.material.SetFloat("_Steps", settings.steps);
                settings.material.SetFloat("_JitterVolumetric", settings.jitter);
                settings.material.SetFloat("_MaxDistance", settings.maxDistance);
                settings.material.SetFloat("_Intensity", settings.intensity);
                settings.material.SetFloat("_GaussSamples", settings.gaussBlur.samples);
                settings.material.SetFloat("_GaussAmount", settings.gaussBlur.amount);

                //this is a debug feature which will let us see the process until any given point
                switch (settings.stage)
                {
                    case Settings.Stage.raymarch:
                        cmd.Blit(Source, tempTexture.Identifier());
                        cmd.Blit(tempTexture.Identifier(), Source, settings.material, 0);
                        break;
                    case Settings.Stage.gaussianBlur:
                        cmd.Blit(Source, tempTexture.Identifier(), settings.material, 0);
                        cmd.Blit(tempTexture.Identifier(), lowResDepthRT.Identifier(), settings.material, 1);
                        cmd.Blit(lowResDepthRT.Identifier(), Source, settings.material, 2);
                        break;
                    case Settings.Stage.full:
                    default:

                        //raymarch
                        cmd.Blit(Source, tempTexture.Identifier(), settings.material, 0);
                        //bilateral blu X, we use the lowresdepth render texture for other things too, it is just a name
                        cmd.Blit(tempTexture.Identifier(), lowResDepthRT.Identifier(), settings.material, 1);
                        //bilateral blur Y
                        cmd.Blit(lowResDepthRT.Identifier(), tempTexture.Identifier(), settings.material, 2);
                        //save it in a global texture
                        cmd.SetGlobalTexture("_volumetricTexture", tempTexture.Identifier());
                        //downsample depth
                        cmd.Blit(Source, lowResDepthRT.Identifier(), settings.material, 4);
                        cmd.SetGlobalTexture("_LowResDepth", lowResDepthRT.Identifier());
                        //upsample and composite
                        cmd.Blit(Source, temptexture3.Identifier(), settings.material, 3);
                        cmd.Blit(temptexture3.Identifier(), Source);
                        break;
                }

                context.ExecuteCommandBuffer(cmd);
            }
            catch
            {
                Debug.LogError("error");
            }
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
    }

    [System.Serializable]
    public class Settings
    {
        public DownSample downsampling = DownSample.off;
        public enum Stage { raymarch, gaussianBlur, full };

        [Space(10)]
        public Stage stage = Stage.full;
        public float intensity = 2f;
        public float scattering = -0.25f;
        public float steps = 12;
        public float maxDistance = 50f;
        public float jitter = 250f;

        [System.Serializable]
        public class GaussBlur
        {
            public float amount = 4;
            public float samples = 2;
        }

        public GaussBlur gaussBlur = new GaussBlur();
        [HideInInspector] public Material material;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public Settings settings = new Settings();
    GodRaysPass pass;

    public override void Create()
    {
        pass = new GodRaysPass(name);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material == null) settings.material = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/VolumetricLight"));

        pass.renderPassEvent = settings.renderPassEvent;
        pass.settings = settings;
        renderer.EnqueuePass(pass);
    }
}
