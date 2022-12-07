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

            string profilerTag;

            int tmpRT0ID = Shader.PropertyToID("tmpBlurRT0");
            int tmpRT1ID = Shader.PropertyToID("tmpBlurRT1");
            int tmpRT2ID = Shader.PropertyToID("tmpBlurRT2");

            int colorID = Shader.PropertyToID("_Color");

            int depthDensityID = Shader.PropertyToID("_DepthDensity");
            int depthStartID = Shader.PropertyToID("_DepthStart");
            int depthEndID = Shader.PropertyToID("_DepthEnd");
            int depthFalloffID = Shader.PropertyToID("_DepthFalloff");

            int heightDensityID = Shader.PropertyToID("_HeightDensity");
            int heightStartID = Shader.PropertyToID("_HeightStart");
            int heightEndID = Shader.PropertyToID("_HeightEnd");
            int heightFalloffID = Shader.PropertyToID("_HeightFalloff");

            RenderTargetIdentifier tmpRT0;
            RenderTargetIdentifier tmpRT1;
            RenderTargetIdentifier tmpRT2;
            public RenderTargetIdentifier Source { get; set; }

            public DepthFogPass()
            {
                profilerTag = "DepthFog";
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var renderer = renderingData.cameraData.renderer;
                Source = renderer.cameraColorTarget;

                RenderTextureDescriptor blitTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                blitTargetDescriptor.depthBufferBits = 0;
                var width = blitTargetDescriptor.width / settings.downsample;
                var height = blitTargetDescriptor.height / settings.downsample;

                cmd.GetTemporaryRT(tmpRT0ID, blitTargetDescriptor, FilterMode.Bilinear);
                cmd.GetTemporaryRT(tmpRT1ID, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                cmd.GetTemporaryRT(tmpRT2ID, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);

                tmpRT0 = new RenderTargetIdentifier(tmpRT0ID);
                tmpRT1 = new RenderTargetIdentifier(tmpRT1ID);
                tmpRT2 = new RenderTargetIdentifier(tmpRT2ID);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get(profilerTag);

                settings.fogMaterial.SetColor(colorID, settings.Color);

                settings.fogMaterial.SetFloat(depthDensityID, settings.DepthDensity);
                settings.fogMaterial.SetFloat(depthStartID, settings.DepthStart);
                settings.fogMaterial.SetFloat(depthEndID, settings.DepthEnd);
                settings.fogMaterial.SetFloat(depthFalloffID, settings.DepthFalloff);

                settings.fogMaterial.SetFloat(heightDensityID, settings.HeightDensity);
                settings.fogMaterial.SetFloat(heightStartID, settings.HeightStart);
                settings.fogMaterial.SetFloat(heightEndID, settings.HeightEnd);
                settings.fogMaterial.SetFloat(heightFalloffID, settings.HeightFalloff);

                settings.compositeMaterial.SetFloat(depthDensityID, settings.DepthDensity);
                settings.compositeMaterial.SetFloat(depthStartID, settings.DepthStart);
                settings.compositeMaterial.SetFloat(depthEndID, settings.DepthEnd);
                settings.compositeMaterial.SetFloat(depthFalloffID, settings.DepthFalloff);

                settings.compositeMaterial.SetFloat(heightDensityID, settings.HeightDensity);
                settings.compositeMaterial.SetFloat(heightStartID, settings.HeightStart);
                settings.compositeMaterial.SetFloat(heightEndID, settings.HeightEnd);
                settings.compositeMaterial.SetFloat(heightFalloffID, settings.HeightFalloff);

                // fog pass
                cmd.Blit(Source, tmpRT0, settings.fogMaterial, 0);
                cmd.Blit(tmpRT0, Source);

                if (settings.DepthBlur)
                {
                    // first pass
                    cmd.SetGlobalFloat("_offset", 1.5f);
                    cmd.Blit(tmpRT0, tmpRT1, settings.blurMaterial);

                    for (var i = 1; i < settings.blurPasses - 1; i++)
                    {
                        cmd.SetGlobalFloat("_offset", 0.5f + i);
                        cmd.Blit(tmpRT1, tmpRT2, settings.blurMaterial);

                        // pingpong
                        var rttmp = tmpRT1;
                        tmpRT1 = tmpRT2;
                        tmpRT2 = rttmp;
                    }

                    // final pass
                    cmd.SetGlobalFloat("_offset", 0.5f + settings.blurPasses - 1f);
                    cmd.Blit(tmpRT1, tmpRT2, settings.blurMaterial);
                    cmd.SetGlobalTexture("_DepthFogBlurTexture", tmpRT2);

                    // compostite pass
                    cmd.Blit(Source, tmpRT0, settings.compositeMaterial, 0);
                    cmd.Blit(tmpRT0, Source);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                CommandBufferPool.Release(cmd);
            }

            public override void FrameCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(tmpRT0ID);
                cmd.ReleaseTemporaryRT(tmpRT1ID);
                cmd.ReleaseTemporaryRT(tmpRT2ID);
            }
        }

        [System.Serializable]
        public class DepthFogSettings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            [ColorUsage(false, true)] public Color Color = Color.white;

            [Header("Depth Fog")]
            [Range(0f, 1f)] public float DepthDensity = 0.85f;
            public float DepthStart = 25f;
            public float DepthEnd = 100f;
            [Range(1f, 4f)] public float DepthFalloff = 1f;

            [Header("Height Fog")]
            [Range(0f, 1f)] public float HeightDensity = 0f;
            public float HeightStart = 0f;
            public float HeightEnd = 25f;
            [Range(1f, 4f)] public float HeightFalloff = 2f;

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