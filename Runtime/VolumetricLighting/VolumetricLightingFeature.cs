namespace UnityEngine.Rendering.Universal
{
    public class VolumetricLightingFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// Draws full screen mesh using given material and pass and reading from source target.
        /// </summary>
        private class VolumetricLightingPass : ScriptableRenderPass
        {
            public Settings settings;

            RenderTargetIdentifier source;
            RenderTargetIdentifier volumetric;
            RenderTargetIdentifier temp0;
            RenderTargetIdentifier temp1;

            int sourceID = Shader.PropertyToID("_Source");
            int volumetricID = Shader.PropertyToID("_Volumetric");
            int temp0ID = Shader.PropertyToID("_Temp0");
            int temp1ID = Shader.PropertyToID("_Temp1");

            Vector4[] lightPositions;
            float[] lightRanges;
            Vector4[] lightColors;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                source = renderingData.cameraData.renderer.cameraColorTarget;
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                cameraTextureDescriptor.depthBufferBits = 0;

                var width = cameraTextureDescriptor.width / settings.downSample;
                var height = cameraTextureDescriptor.height / settings.downSample;

                cmd.GetTemporaryRT(volumetricID, cameraTextureDescriptor);
                cmd.GetTemporaryRT(temp0ID, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);
                cmd.GetTemporaryRT(temp1ID, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);
                volumetric = new RenderTargetIdentifier(volumetricID);
                temp0 = new RenderTargetIdentifier(temp0ID);
                temp1 = new RenderTargetIdentifier(temp1ID);
                ConfigureTarget(volumetric);
                ConfigureTarget(temp0);
                ConfigureTarget(temp1);


                lightPositions = new Vector4[UniversalRenderPipeline.maxVisibleAdditionalLights];
                lightRanges = new float[UniversalRenderPipeline.maxVisibleAdditionalLights];
                lightColors = new Vector4[UniversalRenderPipeline.maxVisibleAdditionalLights];
            }

            /// <inheritdoc/>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get("Volumetric Lighting Feature");

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

                // volumetric pass
                cmd.Blit(source, temp1, settings.volumetricMaterial, 0);

                // blur pass
                cmd.SetGlobalFloat("_offset", 0.5f);
                cmd.Blit(temp1, temp0, settings.blurMaterial);

                for (int i = 1; i < settings.blurPasses - 1; i++)
                {
                    cmd.SetGlobalFloat("_offset", 0.5f + i);
                    cmd.Blit(temp0, temp1, settings.blurMaterial);

                    var temp = temp0;
                    temp0 = temp1;
                    temp1 = temp;
                }

                cmd.Blit(temp0, temp1, settings.blurMaterial);
                cmd.SetGlobalTexture("_VolumetricLightingContribution", temp1);
                //cmd.Blit(temp1, source);

                // composite pass
                cmd.Blit(source, volumetric, settings.compositeMaterial, 0);
                cmd.Blit(volumetric, source);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            /// <inheritdoc/>
            public override void FrameCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(sourceID);
                cmd.ReleaseTemporaryRT(volumetricID);
                cmd.ReleaseTemporaryRT(temp0ID);
                cmd.ReleaseTemporaryRT(temp1ID);
            }
        }

        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            public int downSample = 2;
            public Material volumetricMaterial = null;
            public Material compositeMaterial = null;
            [HideInInspector] public Material blurMaterial = null;
            public int blurPasses = 4;
        }

        public Settings settings = new Settings();
        VolumetricLightingPass blitPass;

        public override void Create()
        {
            blitPass = new VolumetricLightingPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.volumetricMaterial == null) return;
            if (settings.compositeMaterial == null) return;
            if (settings.blurMaterial == null) settings.blurMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/KawaseBlur"));

            blitPass.renderPassEvent = settings.renderPassEvent;
            blitPass.settings = settings;
            renderer.EnqueuePass(blitPass);
        }
    }
}
