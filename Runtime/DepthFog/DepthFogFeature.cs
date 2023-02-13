using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
    public class DepthFogFeature : ScriptableRendererFeature
    {
        private class DepthFogPass : ScriptableRenderPass
        {
            public DepthFogSettings settings;

            string profilerTag = "Depth Fog";

            int colorID = Shader.PropertyToID("_Color");

            int depthDensityID = Shader.PropertyToID("_DepthDensity");
            int depthStartID = Shader.PropertyToID("_DepthStart");
            int depthEndID = Shader.PropertyToID("_DepthEnd");
            int depthFalloffID = Shader.PropertyToID("_DepthFalloff");

            int heightDensityID = Shader.PropertyToID("_HeightDensity");
            int heightStartID = Shader.PropertyToID("_HeightStart");
            int heightEndID = Shader.PropertyToID("_HeightEnd");
            int heightFalloffID = Shader.PropertyToID("_HeightFalloff");

            RTHandle tempRT0;
            RTHandle tempRT1;
            RTHandle tempRT2;
            RTHandle sourceRT;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                base.OnCameraSetup(cmd, ref renderingData);

                sourceRT = renderingData.cameraData.renderer.cameraColorTargetHandle;
                tempRT0 = RTHandles.Alloc(new RenderTargetIdentifier("_TempRT0"), name: "_TempRT0");
                tempRT1 = RTHandles.Alloc(new RenderTargetIdentifier("_TempRT1"), name: "_TempRT1");
                tempRT2 = RTHandles.Alloc(new RenderTargetIdentifier("_TempRT2"), name: "_TempRT2");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var stack = VolumeManager.instance.stack;
                var component = stack.GetComponent<DepthFogComponent>();
                if (!component.IsActive()) return;

                CommandBuffer cmd = CommandBufferPool.Get(profilerTag);

                RenderTextureDescriptor targetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                targetDescriptor.depthBufferBits = 0;

                int width = targetDescriptor.width / settings.downsample;
                int height = targetDescriptor.height / settings.downsample;

                cmd.GetTemporaryRT(Shader.PropertyToID(tempRT0.name), targetDescriptor, FilterMode.Bilinear);
                cmd.GetTemporaryRT(Shader.PropertyToID(tempRT1.name), width, height, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                cmd.GetTemporaryRT(Shader.PropertyToID(tempRT2.name), width, height, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);

                settings.fogMaterial.SetColor(colorID, component.Color.value);

                settings.fogMaterial.SetFloat(depthDensityID, component.DepthDensity.value);
                settings.fogMaterial.SetFloat(depthStartID, component.DepthStart.value);
                settings.fogMaterial.SetFloat(depthEndID, component.DepthEnd.value);
                settings.fogMaterial.SetFloat(depthFalloffID, component.DepthFalloff.value);

                settings.fogMaterial.SetFloat(heightDensityID, component.HeightDensity.value);
                settings.fogMaterial.SetFloat(heightStartID, component.HeightStart.value);
                settings.fogMaterial.SetFloat(heightEndID, component.HeightEnd.value);
                settings.fogMaterial.SetFloat(heightFalloffID, component.HeightFalloff.value);

                settings.compositeMaterial.SetFloat(depthDensityID, component.DepthDensity.value);
                settings.compositeMaterial.SetFloat(depthStartID, component.DepthStart.value);
                settings.compositeMaterial.SetFloat(depthEndID, component.DepthEnd.value);
                settings.compositeMaterial.SetFloat(depthFalloffID, component.DepthFalloff.value);

                settings.compositeMaterial.SetFloat(heightDensityID, component.HeightDensity.value);
                settings.compositeMaterial.SetFloat(heightStartID, component.HeightStart.value);
                settings.compositeMaterial.SetFloat(heightEndID, component.HeightEnd.value);
                settings.compositeMaterial.SetFloat(heightFalloffID, component.HeightFalloff.value);

                // fog pass
                cmd.Blit(sourceRT, tempRT0, settings.fogMaterial, 0);
                cmd.Blit(tempRT0, sourceRT);

                if (settings.DepthBlur)
                {
                    // first pass
                    cmd.SetGlobalFloat("_offset", 1.5f);
                    cmd.Blit(tempRT0, tempRT1, settings.blurMaterial);

                    for (var i = 1; i < settings.blurPasses - 1; i++)
                    {
                        cmd.SetGlobalFloat("_offset", 0.5f + i);
                        cmd.Blit(tempRT1, tempRT2, settings.blurMaterial);

                        // pingpong
                        var rttmp = tempRT1;
                        tempRT1 = tempRT2;
                        tempRT2 = rttmp;
                    }

                    // final pass
                    cmd.SetGlobalFloat("_offset", 0.5f + settings.blurPasses - 1f);
                    cmd.Blit(tempRT1, tempRT2, settings.blurMaterial);
                    cmd.SetGlobalTexture("_DepthFogBlurTexture", tempRT2);

                    // compostite pass
                    cmd.Blit(sourceRT, tempRT0, settings.compositeMaterial, 0);
                    cmd.Blit(tempRT0, sourceRT);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                base.OnCameraCleanup(cmd);

                tempRT0.Release();
                tempRT1.Release();
                tempRT2.Release();
            }
        }

        [System.Serializable]
        public class DepthFogSettings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

            [Header("Blur")]
            public bool DepthBlur = true;
            [Range(2, 15)] public int blurPasses = 4;
            [Range(1, 8)] public int downsample = 2;

            [HideInInspector] public Material blurMaterial = null;
            [HideInInspector] public Material fogMaterial = null;
            [HideInInspector] public Material compositeMaterial = null;
        }

        public DepthFogSettings settings = new DepthFogSettings();
        DepthFogPass pass;

        public override void Create()
        {
            pass = new DepthFogPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.blurMaterial == null) settings.blurMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/KawaseBlur"));
            if (settings.fogMaterial == null) settings.fogMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/DepthFog"));
            if (settings.compositeMaterial == null) settings.compositeMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/DepthFogComposite"));

            pass.renderPassEvent = settings.renderPassEvent;
            pass.settings = settings;
            renderer.EnqueuePass(pass);
        }
    }
}