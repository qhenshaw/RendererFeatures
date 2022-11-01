namespace UnityEngine.Rendering.Universal
{
    public class SobelOutlineFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// Draws full screen mesh using given material and pass and reading from source target.
        /// </summary>
        private class SobelOutlinePass : ScriptableRenderPass
        {
            public FilterMode filterMode { get; set; }
            public Settings settings;

            RenderTargetIdentifier source;
            RenderTargetIdentifier destination;
            int sourceId;
            int destinationId;
            string m_ProfilerTag;

            int temporaryRTId = Shader.PropertyToID("_TempRT");
            int deltaID = Shader.PropertyToID("_Delta");
            int powerID = Shader.PropertyToID("_Power");

            public SobelOutlinePass(string tag)
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

                settings.material.SetFloat(deltaID, settings.LineThickness);
                settings.material.SetFloat(powerID, settings.Power);

                Blit(cmd, source, destination, settings.material, -1);
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
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            [Range(0.00005f, 0.0025f)] public float LineThickness = 0.001f;
            [Range(50f, 10000f)] public float Power = 50f;

            [HideInInspector] public Material material = null;
        }

        public Settings settings = new Settings();
        SobelOutlinePass pass;

        public override void Create()
        {
            pass = new SobelOutlinePass(name);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.material == null) settings.material = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/SobelFilter"));

            pass.renderPassEvent = settings.renderPassEvent;
            pass.settings = settings;
            renderer.EnqueuePass(pass);
        }
    }
}
