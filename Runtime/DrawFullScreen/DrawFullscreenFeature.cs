namespace UnityEngine.Rendering.Universal
{
    public class DrawFullscreenFeature : ScriptableRendererFeature
    {
        private class DrawFullscreenPass : ScriptableRenderPass
        {
            public Settings settings;

            RTHandle sourceRT;
            RTHandle tempRT;

            private string _profilerTag;

            public DrawFullscreenPass(string tag)
            {
                _profilerTag = tag;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor targetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                targetDescriptor.depthBufferBits = 0;

                sourceRT = renderingData.cameraData.renderer.cameraColorTargetHandle;
                RenderingUtils.ReAllocateIfNeeded(ref tempRT, targetDescriptor, name: "_TempRT");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (renderingData.cameraData.cameraType == CameraType.Preview) return;
                if (renderingData.cameraData.cameraType == CameraType.Reflection) return;

                CommandBuffer cmd = CommandBufferPool.Get(_profilerTag);

                Blitter.BlitCameraTexture(cmd, sourceRT, tempRT, settings.blitMaterial, 0);
                Blitter.BlitCameraTexture(cmd, tempRT, sourceRT, Vector2.one);
                //Blit(cmd, sourceRT, tempRT, settings.blitMaterial, 0);
                //Blit(cmd, tempRT, sourceRT);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                tempRT.Release();
            }
        }

        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            public Material blitMaterial = null;
        }

        public Settings settings = new Settings();
        DrawFullscreenPass blitPass;

        public override void Create()
        {
            blitPass = new DrawFullscreenPass(name);
            blitPass.renderPassEvent = settings.renderPassEvent;
            blitPass.settings = settings;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.blitMaterial == null)
            {
                Debug.LogWarningFormat("Missing Material. {0} pass will not execute. Check for missing reference in the assigned renderer.", GetType().Name);
                return;
            }

            renderer.EnqueuePass(blitPass);
        }
    }
}
