namespace UnityEngine.Rendering.Universal
{
    public class OutlineFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// Draws full screen mesh using given material and pass and reading from source target.
        /// </summary>
        private class OutlinePass : ScriptableRenderPass
        {
            public FilterMode filterMode { get; set; }
            public Settings settings;
            public LocalKeyword previewKeyword;

            RTHandle sourceRT;
            RTHandle tempRT;
            string _tag;

            int temporaryRTId = Shader.PropertyToID("_TempRT");
            int outlineThicknessID = Shader.PropertyToID("_OutlineThickness");
            int depthSensitivityID = Shader.PropertyToID("_DepthSensitivity");
            int normalsSensitivityID = Shader.PropertyToID("_NormalsSensitivity");
            int colorSensitivityID = Shader.PropertyToID("_Colorensitivity");
            int outlineColorID = Shader.PropertyToID("_OutlineColor");

            public OutlinePass(string tag)
            {
                _tag = tag;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                sourceRT = renderingData.cameraData.renderer.cameraColorTargetHandle;
                tempRT = RTHandles.Alloc(new RenderTargetIdentifier("_TempRT"), name: "_TempRT");

                ConfigureInput(ScriptableRenderPassInput.Normal);
            }

            /// <inheritdoc/>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get(_tag);

                RenderTextureDescriptor targetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                targetDescriptor.depthBufferBits = 0;

                cmd.GetTemporaryRT(Shader.PropertyToID(tempRT.name), targetDescriptor, FilterMode.Bilinear);

                settings.material.SetFloat(outlineThicknessID, settings.OutlineThickness);
                settings.material.SetFloat(depthSensitivityID, settings.DepthSensitivity);
                settings.material.SetFloat(normalsSensitivityID, settings.NormalsSensitivity);
                settings.material.SetFloat(colorSensitivityID, settings.ColorSensitivity);
                settings.material.SetColor(outlineColorID, settings.OutlineColor);
                if (settings.Preview) settings.material.EnableKeyword(previewKeyword);
                else settings.material.DisableKeyword(previewKeyword);

                Blit(cmd, sourceRT, tempRT, settings.material, -1);
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
            [Header("Event")]
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingSkybox;

            [Header("Tuning")]
            public float OutlineThickness = 2f;
            public float DepthSensitivity = 1f;
            public float NormalsSensitivity = 1f;
            [HideInInspector] public float ColorSensitivity = 0f;
            public Color OutlineColor = Color.black;

            [Header("Debug")]
            public bool Preview = false;

            [HideInInspector] public Material material = null;
        }

        public Settings settings = new Settings();
        OutlinePass pass;

        public override void Create()
        {
            pass = new OutlinePass(name);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.material == null) settings.material = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Outline"));

            pass.renderPassEvent = settings.renderPassEvent;
            pass.settings = settings;
            pass.previewKeyword = new LocalKeyword(settings.material.shader, "_PREVIEW");
            renderer.EnqueuePass(pass);
        }
    }
}
