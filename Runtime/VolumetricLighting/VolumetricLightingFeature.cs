using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VolumetricLightingFeature : ScriptableRendererFeature
{
    class VolumetricDensityPass : ScriptableRenderPass
    {
        public Settings Settings;

        RenderTargetIdentifier particleRT;

        FilteringSettings densityFilteringSettings;
        RenderStateBlock renderStateBlock;
        readonly List<ShaderTagId> _shaderTagIds = new List<ShaderTagId>();

        int particleRTID = Shader.PropertyToID("particleRT");

        public VolumetricDensityPass(Settings settings)
        {
            Settings = settings;

            densityFilteringSettings = new FilteringSettings(null, settings.densityMask);
            renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            _shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForward"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor blitTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            int width = blitTargetDescriptor.width;
            int height = blitTargetDescriptor.height;

            cmd.GetTemporaryRT(particleRTID, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
            particleRT = new RenderTargetIdentifier(particleRTID);
            ConfigureTarget(particleRT);

            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Volumetric Lighting Density Pass");

            SortingCriteria sortingCriteria = SortingCriteria.CommonTransparent;
            DrawingSettings drawingSettings = CreateDrawingSettings(_shaderTagIds, ref renderingData, sortingCriteria);

            // draw density particles
            context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref densityFilteringSettings, ref renderStateBlock);

            // composite result
            cmd.SetGlobalTexture("_VolumetricLightingParticleDensity", particleRT);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(particleRTID);
        }
    }

    class VolumetricSurfacePass : ScriptableRenderPass
    {
        public Settings Settings;

        RenderTargetIdentifier source;
        RenderTargetIdentifier fullRT0;
        RenderTargetIdentifier fullRT1;
        RenderTargetIdentifier halfRT0;
        RenderTargetIdentifier halfRT1;
        RenderTargetIdentifier quarRT0;
        RenderTargetIdentifier quarRT1;

        FilteringSettings surfaceFilteringSettings;
        RenderStateBlock renderStateBlock;
        readonly List<ShaderTagId> _shaderTagIds = new List<ShaderTagId>();

        int fullRT0ID = Shader.PropertyToID("fullRT0");
        int fullRT1ID = Shader.PropertyToID("fullRT1");
        int halfRT0ID = Shader.PropertyToID("halfRT0");
        int halfRT1ID = Shader.PropertyToID("halfRT1");
        int quarRT0ID = Shader.PropertyToID("quarRT0");
        int quarRT1ID = Shader.PropertyToID("quarRT1");

        public VolumetricSurfacePass(Settings settings)
        {
            Settings = settings;

            surfaceFilteringSettings = new FilteringSettings(null, settings.layerMask);
            renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            _shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForward"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor blitTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            Vector2Int full = new Vector2Int(blitTargetDescriptor.width, blitTargetDescriptor.height);
            Vector2Int half = new Vector2Int(blitTargetDescriptor.width / 2, blitTargetDescriptor.height / 2);
            Vector2Int quarter = new Vector2Int(blitTargetDescriptor.width / 4, blitTargetDescriptor.height / 4);

            source = renderingData.cameraData.renderer.cameraColorTarget;
            cmd.GetTemporaryRT(fullRT0ID, full.x, full.y, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
            cmd.GetTemporaryRT(fullRT1ID, full.x, full.y, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
            cmd.GetTemporaryRT(halfRT0ID, half.x, half.y, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
            cmd.GetTemporaryRT(halfRT1ID, half.x, half.y, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
            cmd.GetTemporaryRT(quarRT0ID, quarter.x, quarter.y, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
            cmd.GetTemporaryRT(quarRT1ID, quarter.x, quarter.y, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);

            fullRT0 = new RenderTargetIdentifier(fullRT0ID);
            fullRT1 = new RenderTargetIdentifier(fullRT1ID);
            halfRT0 = new RenderTargetIdentifier(halfRT0ID);
            halfRT1 = new RenderTargetIdentifier(halfRT1ID);
            quarRT0 = new RenderTargetIdentifier(quarRT0ID);
            quarRT1 = new RenderTargetIdentifier(quarRT1ID);

            ConfigureTarget(fullRT0);
            ConfigureTarget(fullRT1);
            ConfigureTarget(halfRT0);
            ConfigureTarget(halfRT1);
            ConfigureTarget(quarRT0);
            ConfigureTarget(quarRT1);

            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Volumetric Lighting Surface Pass");

            SortingCriteria sortingCriteria = SortingCriteria.CommonTransparent;
            DrawingSettings drawingSettings = CreateDrawingSettings(_shaderTagIds, ref renderingData, sortingCriteria);

            // draw volumetric surfaces
            context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref surfaceFilteringSettings, ref renderStateBlock);

            //// blur quarter
            //if (Settings.blurPasses > 0)
            //{
            //    cmd.SetGlobalFloat("_offset", 0.5f);
            //    cmd.Blit(quarRT1, quarRT0, Settings.blurMaterial);

            //    for (int i = 1; i < Settings.blurPasses - 1; i++)
            //    {
            //        cmd.SetGlobalFloat("_offset", 0.5f + i);
            //        cmd.Blit(quarRT0, quarRT1, Settings.blurMaterial);

            //        var temp = quarRT0;
            //        quarRT0 = quarRT1;
            //        quarRT1 = temp;
            //    }

            //    cmd.Blit(quarRT0, quarRT1, Settings.blurMaterial);
            //}

            // blur half
            if (Settings.blurPasses > 0)
            {
                cmd.SetGlobalFloat("_offset", 0.5f);
                cmd.Blit(quarRT1, halfRT0, Settings.blurMaterial);

                for (int i = 1; i < Settings.blurPasses - 1; i++)
                {
                    cmd.SetGlobalFloat("_offset", 0.5f + i);
                    cmd.Blit(halfRT0, halfRT1, Settings.blurMaterial);

                    var temp = halfRT0;
                    halfRT0 = halfRT1;
                    halfRT1 = temp;
                }

                cmd.Blit(halfRT0, halfRT1, Settings.blurMaterial);
            }

            // blur full
            if (Settings.blurPasses > 0)
            {
                cmd.SetGlobalFloat("_offset", 0.5f);
                cmd.Blit(halfRT1, fullRT0, Settings.blurMaterial);

                for (int i = 1; i < Settings.blurPasses - 1; i++)
                {
                    cmd.SetGlobalFloat("_offset", 0.5f + i);
                    cmd.Blit(fullRT0, fullRT1, Settings.blurMaterial);

                    var temp = fullRT0;
                    fullRT0 = fullRT1;
                    fullRT1 = temp;
                }

                cmd.Blit(fullRT0, fullRT1, Settings.blurMaterial);
            }

            // copy low res depth
            cmd.Blit(source, halfRT1, Settings.depthMaterial, 0);
            cmd.SetGlobalTexture("_LowResDepth", halfRT1);

            //// composite result
            cmd.SetGlobalTexture("_VolumetricLightingContribution", fullRT1);
            cmd.Blit(source, fullRT0, Settings.compositeMaterial, 0);
            cmd.Blit(fullRT0, source);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(fullRT0ID);
            cmd.ReleaseTemporaryRT(fullRT1ID);
            cmd.ReleaseTemporaryRT(halfRT0ID);
            cmd.ReleaseTemporaryRT(halfRT1ID);
            cmd.ReleaseTemporaryRT(quarRT0ID);
            cmd.ReleaseTemporaryRT(quarRT1ID);
        }
    }

    [System.Serializable]
    public class Settings
    {
        public LayerMask layerMask;
        public LayerMask densityMask;
        public Material compositeMaterial;
        public Material depthMaterial;
        [Range(1, 8)] public int downSample = 4;
        [Range(0, 8)] public int blurPasses = 4;
        [HideInInspector] public Material blurMaterial;
        [HideInInspector] public Material blitMaterial;
    }

    public Settings settings = new Settings();
    VolumetricDensityPass densityPass;
    VolumetricSurfacePass surfacePass;

    public override void Create()
    {
        densityPass = new VolumetricDensityPass(settings);
        surfacePass = new VolumetricSurfacePass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.compositeMaterial == null) return;
        if (settings.depthMaterial == null) return;
        if (settings.blurMaterial == null) settings.blurMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/KawaseBlur"));
        if (settings.blitMaterial == null) settings.blitMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Universal Render Pipeline/Blit"));

        densityPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        surfacePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        renderer.EnqueuePass(densityPass);
        renderer.EnqueuePass(surfacePass);
    }
}