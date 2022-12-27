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

        RenderTargetHandle lowResDepthRT;
        RenderTargetHandle tempTexture;

        public VolumetricSurfacePass(Settings settings)
        {
            Settings = settings;

            surfaceFilteringSettings = new FilteringSettings(null, settings.layerMask);
            renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            _shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForward"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));

            lowResDepthRT.id = Shader.PropertyToID("lowResDepthRT");
            tempTexture.id = Shader.PropertyToID("tempTexture");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor blitTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            Vector2Int full = new Vector2Int(blitTargetDescriptor.width, blitTargetDescriptor.height);
            Vector2Int half = new Vector2Int(blitTargetDescriptor.width / 2, blitTargetDescriptor.height / 2);
            Vector2Int quarter = new Vector2Int(blitTargetDescriptor.width / 2, blitTargetDescriptor.height / 2);

            source = renderingData.cameraData.renderer.cameraColorTarget;
            cmd.GetTemporaryRT(fullRT0ID, full.x, full.y, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
            cmd.GetTemporaryRT(fullRT1ID, full.x, full.y, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
            cmd.GetTemporaryRT(halfRT0ID, quarter.x, quarter.y, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
            cmd.GetTemporaryRT(halfRT1ID, quarter.x, quarter.y, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
            cmd.GetTemporaryRT(quarRT0ID, quarter.x, quarter.y, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
            cmd.GetTemporaryRT(quarRT1ID, quarter.x, quarter.y, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);

            fullRT0 = new RenderTargetIdentifier(fullRT0ID);
            fullRT1 = new RenderTargetIdentifier(fullRT1ID);
            halfRT0 = new RenderTargetIdentifier(halfRT0ID);
            halfRT1 = new RenderTargetIdentifier(halfRT1ID);
            quarRT0 = new RenderTargetIdentifier(quarRT0ID);
            quarRT1 = new RenderTargetIdentifier(quarRT1ID);

            blitTargetDescriptor.width /= 4;
            blitTargetDescriptor.height /= 4;
            blitTargetDescriptor.msaaSamples = 1;

            cmd.GetTemporaryRT(lowResDepthRT.id, blitTargetDescriptor);
            ConfigureTarget(lowResDepthRT.Identifier());

            cmd.GetTemporaryRT(tempTexture.id, blitTargetDescriptor);
            ConfigureTarget(tempTexture.Identifier());

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

            Settings.godRaysMaterial.SetFloat("_Intensity", 1f);
            Settings.godRaysMaterial.SetVector("_Tint", new Vector4(1f, 1f, 1f, 1f));
            Settings.godRaysMaterial.SetFloat("_GaussSamples", Settings.blurSamples);
            Settings.godRaysMaterial.SetFloat("_GaussAmount", Settings.blurAmount);

            // blur horiz/vert
            cmd.Blit(quarRT1, quarRT0, Settings.godRaysMaterial, 1);
            cmd.Blit(quarRT0, halfRT0ID, Settings.godRaysMaterial, 2);
            cmd.SetGlobalTexture("_volumetricTexture", halfRT0ID);

            // downsample depth
            cmd.Blit(source, quarRT1, Settings.godRaysMaterial, 4);
            cmd.SetGlobalTexture("_LowResDepth", quarRT1);

            // upsample and composite
            cmd.Blit(source, fullRT0, Settings.godRaysMaterial, 3);
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
        public float blurSamples = 2f;
        public float blurAmount = 4f;
        [HideInInspector] public Material blurMaterial;
        [HideInInspector] public Material blitMaterial;
        [HideInInspector] public Material godRaysMaterial;
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
        if (settings.godRaysMaterial == null) settings.godRaysMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/GodRays"));

        densityPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        surfacePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        renderer.EnqueuePass(densityPass);
        renderer.EnqueuePass(surfacePass);
    }
}