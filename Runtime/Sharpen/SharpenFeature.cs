namespace UnityEngine.Rendering.Universal
{
    public class SharpenFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// Draws full screen mesh using given material and pass and reading from source target.
        /// </summary>
        private class SharpenPass : ScriptableRenderPass
        {
            public FilterMode filterMode { get; set; }
            public Settings settings;

            RTHandle sourceRT;
            RTHandle tempRT;

            int sizeID = Shader.PropertyToID("_Size");
            int intensityID = Shader.PropertyToID("_Intensity");

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                sourceRT = renderingData.cameraData.renderer.cameraColorTargetHandle;
                tempRT = RTHandles.Alloc(new RenderTargetIdentifier("_TempRT"), name: "_TempRT");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get("Sharpen Feature");

                RenderTextureDescriptor targetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                targetDescriptor.depthBufferBits = 0;

                cmd.GetTemporaryRT(Shader.PropertyToID(tempRT.name), targetDescriptor, FilterMode.Bilinear);

                settings.material.SetFloat(sizeID, settings.Size);
                settings.material.SetFloat(intensityID, settings.Intensity);

                Blit(cmd, sourceRT, tempRT, settings.material, 0);
                Blit(cmd, tempRT, sourceRT);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                base.OnCameraCleanup(cmd);

                tempRT.Release();
            }
        }

        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            [Range(0f, 0.001f)] public float Size = 0.0005f;
            [Range(0f, 1f)] public float Intensity = 0.5f;

            [HideInInspector] public Material material = null;
        }

        public Settings settings = new Settings();
        SharpenPass pass;

        public override void Create()
        {
            pass = new SharpenPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.material == null) settings.material = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Sharpen"));

            pass.renderPassEvent = settings.renderPassEvent;
            pass.settings = settings;
            renderer.EnqueuePass(pass);
        }
    }
}
