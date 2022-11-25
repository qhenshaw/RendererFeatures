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
            public LocalKeyword previewKeyword;

            RenderTargetIdentifier source;
            RenderTargetIdentifier destination;
            int sourceId;
            int destinationId;
            string m_ProfilerTag;

            int temporaryRTId = Shader.PropertyToID("_TempRT");
            int outlineThicknessID = Shader.PropertyToID("_OutlineThickness");
            int depthSensitivityID = Shader.PropertyToID("_DepthSensitivity");
            int normalsSensitivityID = Shader.PropertyToID("_NormalsSensitivity");
            int colorSensitivityID = Shader.PropertyToID("_Colorensitivity");
            int outlineColorID = Shader.PropertyToID("_OutlineColor");

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

                ConfigureInput(ScriptableRenderPassInput.Normal);
            }

            /// <inheritdoc/>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);

                settings.material.SetFloat(outlineThicknessID, settings.OutlineThickness);
                settings.material.SetFloat(depthSensitivityID, settings.DepthSensitivity);
                settings.material.SetFloat(normalsSensitivityID, settings.NormalsSensitivity);
                settings.material.SetFloat(colorSensitivityID, settings.ColorSensitivity);
                settings.material.SetColor(outlineColorID, settings.OutlineColor);
                if (settings.Preview) settings.material.EnableKeyword(previewKeyword);
                else settings.material.DisableKeyword(previewKeyword);

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
            [Header("Event")]
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingSkybox;

            [Header("Tuning")]
            public float OutlineThickness = 1f;
            public float DepthSensitivity = 0.1f;
            public float NormalsSensitivity = 1f;
            [HideInInspector] public float ColorSensitivity = 0f;
            public Color OutlineColor = Color.black;

            [Header("Debug")]
            public bool Preview = false;

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
            if (settings.material == null) settings.material = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/SobelOutline"));

            pass.renderPassEvent = settings.renderPassEvent;
            pass.settings = settings;
            pass.previewKeyword = new LocalKeyword(settings.material.shader, "_PREVIEW");
            renderer.EnqueuePass(pass);
        }
    }
}
