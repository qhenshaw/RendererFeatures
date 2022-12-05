namespace UnityEngine.Rendering.Universal
{
    public class DepthFogFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// Draws full screen mesh using given material and pass and reading from source target.
        /// </summary>
        private class DepthFogPass : ScriptableRenderPass
        {
            public FilterMode filterMode { get; set; }
            public Settings settings;

            RenderTargetIdentifier source;
            RenderTargetIdentifier destination;
            int sourceId;
            int destinationId;
            string m_ProfilerTag;

            int temporaryRTId = Shader.PropertyToID("_TempRT");
            int colorID = Shader.PropertyToID("_Color");
            int densityID = Shader.PropertyToID("_Density");
            int startID = Shader.PropertyToID("_Start");
            int endID = Shader.PropertyToID("_End");
            int falloffID = Shader.PropertyToID("_Falloff");

            public DepthFogPass(string tag)
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

                settings.material.SetColor(colorID, settings.Color);
                settings.material.SetFloat(densityID, settings.Density);
                settings.material.SetFloat(startID, settings.Start);
                settings.material.SetFloat(endID, settings.End);
                settings.material.SetFloat(falloffID, settings.Falloff);

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
            [ColorUsage(false, true)] public Color Color = Color.white;
            [Range(0f, 1f)] public float Density = 0.5f;
            [Range(0f, 1000f)] public float Start = 25f;
            [Range(0f, 1000f)] public float End = 100f;
            [Range(1f, 4f)] public float Falloff = 1f;

            [HideInInspector] public Material material = null;
        }

        public Settings settings = new Settings();
        DepthFogPass pass;

        public override void Create()
        {
            pass = new DepthFogPass(name);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.material == null) settings.material = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/DepthFog"));

            pass.renderPassEvent = settings.renderPassEvent;
            pass.settings = settings;
            renderer.EnqueuePass(pass);
        }
    }
}
