namespace UnityEngine.Rendering.Universal
{
    public enum BufferType
    {
        CameraColor,
        Custom
    }

    public class DrawFullscreenFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// Draws full screen mesh using given material and pass and reading from source target.
        /// </summary>
        private class DrawFullscreenPass : ScriptableRenderPass
        {
            public FilterMode filterMode { get; set; }
            public DrawFullscreenFeature.Settings settings;

            RTHandle sourceRT;
            RTHandle tempRT;

            private string _tag;

            public DrawFullscreenPass(string tag)
            {
                _tag = tag;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                sourceRT = renderingData.cameraData.renderer.cameraColorTargetHandle;
                tempRT = RTHandles.Alloc(new RenderTargetIdentifier("_TempRT"), name: "_TempRT");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get(_tag);

                RenderTextureDescriptor targetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                targetDescriptor.depthBufferBits = 0;

                cmd.GetTemporaryRT(Shader.PropertyToID(tempRT.name), targetDescriptor, FilterMode.Bilinear);

                Blit(cmd, sourceRT, tempRT, settings.blitMaterial, 0);
                Blit(cmd, tempRT, sourceRT);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            /// <inheritdoc/>
            public override void FrameCleanup(CommandBuffer cmd)
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
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.blitMaterial == null)
            {
                Debug.LogWarningFormat("Missing Material. {0} pass will not execute. Check for missing reference in the assigned renderer.", GetType().Name);
                return;
            }

            blitPass.renderPassEvent = settings.renderPassEvent;
            blitPass.settings = settings;
            renderer.EnqueuePass(blitPass);
        }
    }
}
