using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
    public class KawaseBlurFeature : ScriptableRendererFeature
    {
        private class KawaseBlurRenderPass : ScriptableRenderPass
        {
            public KawaseBlurSettings settings;

            int tmpId1;
            int tmpId2;

            RenderTargetIdentifier tmpRT1;
            RenderTargetIdentifier tmpRT2;
            string profilerTag;

            public RenderTargetIdentifier Source { get; set; }

            public KawaseBlurRenderPass(string profilerTag)
            {
                this.profilerTag = profilerTag;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var renderer = renderingData.cameraData.renderer;
                Source = renderer.cameraColorTarget;
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                var width = cameraTextureDescriptor.width / settings.downsample;
                var height = cameraTextureDescriptor.height / settings.downsample;

                tmpId1 = Shader.PropertyToID("tmpBlurRT1");
                tmpId2 = Shader.PropertyToID("tmpBlurRT2");
                cmd.GetTemporaryRT(tmpId1, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);
                cmd.GetTemporaryRT(tmpId2, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);

                tmpRT1 = new RenderTargetIdentifier(tmpId1);
                tmpRT2 = new RenderTargetIdentifier(tmpId2);

                ConfigureTarget(tmpRT1);
                ConfigureTarget(tmpRT2);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get(profilerTag);

                RenderTextureDescriptor opaqueDesc = renderingData.cameraData.cameraTargetDescriptor;
                opaqueDesc.depthBufferBits = 0;

                cmd.SetGlobalFloat("_offset", 0.5f);
                cmd.Blit(Source, tmpRT1, settings.material);

                for (int i = 1; i < settings.blurPasses - 1; i++)
                {
                    cmd.SetGlobalFloat("_offset", 0.5f + i);
                    cmd.Blit(tmpRT1, tmpRT2, settings.material);

                    var temp = tmpRT1;
                    tmpRT1 = tmpRT2;
                    tmpRT2 = temp;
                }

                if(settings.copyToFramebuffer)
                {
                    cmd.Blit(tmpRT1, Source, settings.material);
                }
                else
                {
                    cmd.Blit(tmpRT1, tmpRT2, settings.material);
                    cmd.SetGlobalTexture(settings.targetName, tmpRT2);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                CommandBufferPool.Release(cmd);
            }

            public override void FrameCleanup(CommandBuffer cmd)
            {
            }
        }

        [System.Serializable]
        public class KawaseBlurSettings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            [HideInInspector] public Material material = null;

            [Range(2, 15)]
            public int blurPasses = 4;

            [Range(1, 4)]
            public int downsample = 2;
            public bool copyToFramebuffer;
            public string targetName = "_BlurTexture";
        }

        public KawaseBlurSettings settings = new KawaseBlurSettings();
        KawaseBlurRenderPass pass;

        public override void Create()
        {
            pass = new KawaseBlurRenderPass("KawaseBlur");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Reflection || renderingData.cameraData.cameraType == CameraType.Preview) return;

            if (settings.material == null) settings.material = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/KawaseBlur"));

            pass.renderPassEvent = settings.renderPassEvent;
            pass.settings = settings;
            renderer.EnqueuePass(pass);
        }
    }
}