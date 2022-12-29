using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VolumetricLightingFeature : ScriptableRendererFeature
{
    protected const string volumetricSurfaceID = "volumetricSurfaceID";

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

            cmd.GetTemporaryRT(particleRTID, blitTargetDescriptor);
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

    class SurfacePass : ScriptableRenderPass
    {
        public Settings Settings;

        RenderTargetIdentifier RT;

        FilteringSettings densityFilteringSettings;
        RenderStateBlock renderStateBlock;
        readonly List<ShaderTagId> _shaderTagIds = new List<ShaderTagId>();

        int rtID;

        public SurfacePass(Settings settings)
        {
            Settings = settings;

            densityFilteringSettings = new FilteringSettings(null, settings.layerMask);
            renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            _shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForward"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
            rtID = Shader.PropertyToID(volumetricSurfaceID);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor blitTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            blitTargetDescriptor.width /= Settings.downSample;
            blitTargetDescriptor.height /= Settings.downSample;

            cmd.GetTemporaryRT(rtID, blitTargetDescriptor);
            RT = new RenderTargetIdentifier(rtID);
            ConfigureTarget(RT);

            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Volumetric Lighting Surface Pass");

            SortingCriteria sortingCriteria = SortingCriteria.CommonTransparent;
            DrawingSettings drawingSettings = CreateDrawingSettings(_shaderTagIds, ref renderingData, sortingCriteria);

            // draw density particles
            context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref densityFilteringSettings, ref renderStateBlock);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(rtID);
        }
    }

    class CompositePass : ScriptableRenderPass
    {
        public Settings Settings;

        RenderTargetIdentifier source;
        RenderTargetHandle full0;
        RenderTargetHandle full1;
        RenderTargetHandle low0;
        RenderTargetHandle low1;
        RenderTargetHandle volumetricSurfaceResult;
        RenderTargetHandle lowDepth;

        FilteringSettings surfaceFilteringSettings;
        RenderStateBlock renderStateBlock;
        readonly List<ShaderTagId> _shaderTagIds = new List<ShaderTagId>();

        public CompositePass(Settings settings)
        {
            Settings = settings;

            surfaceFilteringSettings = new FilteringSettings(null, settings.layerMask);
            renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            _shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForward"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));

            full0.id = Shader.PropertyToID("full0");
            full1.id = Shader.PropertyToID("full1");
            low0.id = Shader.PropertyToID("low0");
            low1.id = Shader.PropertyToID("low1");
            volumetricSurfaceResult.id = Shader.PropertyToID(volumetricSurfaceID);
            lowDepth.id = Shader.PropertyToID("lowDepth");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor blitTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            var original = blitTargetDescriptor;
            int divider = Settings.downSample;

            if (Camera.current != null) //This is necessary so it uses the proper resolution in the scene window
            {
                blitTargetDescriptor.width = (int)Camera.current.pixelRect.width / divider;
                blitTargetDescriptor.height = (int)Camera.current.pixelRect.height / divider;
                original.width = (int)Camera.current.pixelRect.width;
                original.height = (int)Camera.current.pixelRect.height;
            }
            else //regular game window
            {
                blitTargetDescriptor.width /= divider;
                blitTargetDescriptor.height /= divider;
            }

            blitTargetDescriptor.msaaSamples = 1;

            source = renderingData.cameraData.renderer.cameraColorTarget;

            cmd.GetTemporaryRT(full0.id, original);
            cmd.GetTemporaryRT(full1.id, original);
            cmd.GetTemporaryRT(low0.id, blitTargetDescriptor);
            cmd.GetTemporaryRT(low1.id, blitTargetDescriptor);
            cmd.GetTemporaryRT(volumetricSurfaceResult.id, blitTargetDescriptor);
            cmd.GetTemporaryRT(lowDepth.id, blitTargetDescriptor);

            ConfigureTarget(full0.Identifier());
            ConfigureTarget(full1.Identifier());
            ConfigureTarget(low0.Identifier());
            ConfigureTarget(low1.Identifier());
            ConfigureTarget(volumetricSurfaceResult.Identifier());
            ConfigureTarget(lowDepth.Identifier());

            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Volumetric Lighting Pass");
            cmd.Clear();

            // configure directional light
            foreach (VisibleLight visibleLight in renderingData.lightData.visibleLights)
            {
                if (visibleLight.light.type == LightType.Directional)
                {
                    Settings.material.SetVector("_Tint", visibleLight.finalColor);
                    Settings.material.SetVector("_SunDirection", visibleLight.light.transform.forward);
                }
            }

            // configure shader properties
            Settings.material.SetFloat("_Scattering", 0f);
            Settings.material.SetFloat("_Steps", Settings.steps);
            Settings.material.SetFloat("_JitterVolumetric", 250);
            Settings.material.SetFloat("_MaxDistance", Settings.maxDistance);
            Settings.material.SetFloat("_Intensity", Settings.intensity);
            Settings.material.SetFloat("_GaussSamples", Settings.blurSamples);
            Settings.material.SetFloat("_GaussAmount", Settings.blurAmount);

            // directional light raymarch
            cmd.Blit(source, low0.Identifier(), Settings.material, 0);

            // blur horiz/vert
            cmd.Blit(low0.Identifier(), low1.Identifier(), Settings.material, 1);
            cmd.Blit(low1.Identifier(), low0.Identifier(), Settings.material, 2);
            cmd.SetGlobalTexture("_mainLightVolumetric", low0.Identifier());

            // blur horiz/vert
            cmd.Blit(volumetricSurfaceResult.Identifier(), low1.Identifier(), Settings.material, 1);
            cmd.Blit(low1.Identifier(), volumetricSurfaceResult.Identifier(), Settings.material, 2);
            cmd.SetGlobalTexture("_additionalLightsVolumetric", volumetricSurfaceResult.Identifier());

            // downsample depth
            cmd.Blit(source, lowDepth.Identifier(), Settings.material, 4);
            cmd.SetGlobalTexture("_LowResDepth", lowDepth.Identifier());

            // upsample and composite
            cmd.Blit(source, full0.Identifier(), Settings.material, 3);
            cmd.Blit(full0.Identifier(), source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(full0.id);
            cmd.ReleaseTemporaryRT(full1.id);
            cmd.ReleaseTemporaryRT(low0.id);
            cmd.ReleaseTemporaryRT(low1.id);
            cmd.ReleaseTemporaryRT(volumetricSurfaceResult.id);
            cmd.ReleaseTemporaryRT(lowDepth.id);
        }
    }

    [System.Serializable]
    public class Settings
    {
        [Header("Layers")]
        public LayerMask layerMask;
        public LayerMask densityMask;

        [Header("Blur")]
        [Range(1, 4)] public int downSample = 2;
        public float blurSamples = 2f;
        public float blurAmount = 4f;

        [Header("Directional Light")]
        public float intensity = 1f;
        public float maxDistance = 50f;
        public int steps = 12;

        [HideInInspector] public Material material;
    }

    public Settings settings = new Settings();
    VolumetricDensityPass densityPass;
    SurfacePass surfacePass;
    CompositePass compositePass;

    public override void Create()
    {
        densityPass = new VolumetricDensityPass(settings);
        surfacePass = new SurfacePass(settings);
        compositePass = new CompositePass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material == null) settings.material = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/VolumetricLighting"));

        densityPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        compositePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        renderer.EnqueuePass(densityPass);
        renderer.EnqueuePass(surfacePass);
        renderer.EnqueuePass(compositePass);
    }
}