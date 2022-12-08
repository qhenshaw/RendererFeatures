namespace UnityEngine.Rendering.Universal
{
    public class VolumetricLightingFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// Draws full screen mesh using given material and pass and reading from source target.
        /// </summary>
        private class VolumetricLightingPass : ScriptableRenderPass
        {
            public FilterMode filterMode { get; set; }
            public Settings settings;

            RenderTargetIdentifier source;
            RenderTargetIdentifier destination;
            int temporaryRTId = Shader.PropertyToID("_TempRT");

            int sourceId;
            int destinationId;

            string m_ProfilerTag = "Volumetric Lights";

            Vector4[] lightPositions;
            float[] lightRanges;
            Vector4[] lightColors;

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

                lightPositions = new Vector4[UniversalRenderPipeline.maxVisibleAdditionalLights];
                lightRanges = new float[UniversalRenderPipeline.maxVisibleAdditionalLights];
                lightColors = new Vector4[UniversalRenderPipeline.maxVisibleAdditionalLights];
            }

            /// <inheritdoc/>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);

                var lightData = renderingData.lightData;
                var visibleLights = renderingData.lightData.visibleLights;
                int maxLights = Mathf.Min(visibleLights.Length, UniversalRenderPipeline.maxVisibleAdditionalLights);
                int lightIndex = 0;
                for (int i = 0; i < maxLights; i++)
                {
                    if (lightData.mainLightIndex == i) continue;
                    {
                        VisibleLight light = visibleLights[i];
                        lightPositions[lightIndex] = light.light.transform.position;
                        lightRanges[lightIndex] = light.range;
                        Color color = light.finalColor;
                        lightColors[lightIndex] = new Vector4(color.r, color.g, color.b, 1f);
                        lightIndex++;
                    }
                }

                cmd.SetGlobalInteger("_PixelLightCount", lightIndex);
                cmd.SetGlobalVectorArray("_PixelLightPositions", lightPositions);
                cmd.SetGlobalFloatArray("_PixelLightRanges", lightRanges);
                cmd.SetGlobalVectorArray("_PixelLightColors", lightColors);

                cmd.Blit(source, destination, settings.blitMaterial, settings.blitMaterialPassIndex);
                cmd.Blit(destination, source);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            /// <inheritdoc/>
            public override void FrameCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(sourceId);
                cmd.ReleaseTemporaryRT(destinationId); 
            }
        }

        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

            public Material blitMaterial = null;
            public int blitMaterialPassIndex = 0;
        }

        public Settings settings = new Settings();
        VolumetricLightingPass blitPass;

        public override void Create()
        {
            blitPass = new VolumetricLightingPass();
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
