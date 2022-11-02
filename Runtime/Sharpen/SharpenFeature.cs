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

            RenderTargetIdentifier source;
            RenderTargetIdentifier destination;
            int sourceId;
            int destinationId;
            string m_ProfilerTag;

            int temporaryRTId = Shader.PropertyToID("_TempRT");
            int sizeID = Shader.PropertyToID("_Size");
            int intensityID = Shader.PropertyToID("_Intensity");

            public SharpenPass(string tag)
            {
                m_ProfilerTag = tag;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor blitTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                blitTargetDescriptor.depthBufferBits = 0;

                var renderer = renderingData.cameraData.renderer;

                sourceId = -1;
                source = renderer.cameraColorTarget;

                destinationId = temporaryRTId;
                cmd.GetTemporaryRT(destinationId, blitTargetDescriptor, filterMode);
                destination = new RenderTargetIdentifier(destinationId);
            }

            /// <inheritdoc/>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);

                settings.material.SetFloat(sizeID, settings.Size);
                settings.material.SetFloat(intensityID, settings.Intensity);

                Blit(cmd, source, destination, settings.material, 0);
                Blit(cmd, destination, source);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            /// <inheritdoc/>
            public override void FrameCleanup(CommandBuffer cmd)
            {
                if (destinationId != -1)
                    cmd.ReleaseTemporaryRT(destinationId);

                if (source == destination && sourceId != -1)
                    cmd.ReleaseTemporaryRT(sourceId);
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
            pass = new SharpenPass(name);
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
