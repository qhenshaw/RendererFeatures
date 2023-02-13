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

        RTHandle particleRT;

        FilteringSettings densityFilteringSettings;
        RenderStateBlock renderStateBlock;
        readonly List<ShaderTagId> _shaderTagIds = new List<ShaderTagId>();

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
            particleRT = RTHandles.Alloc(new RenderTargetIdentifier("_ParticleRT"), "_ParticleRT");
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            base.Configure(cmd, cameraTextureDescriptor);
            ConfigureTarget(particleRT);
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Volumetric Lighting Density Pass");

            RenderTextureDescriptor blitTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            blitTargetDescriptor.depthBufferBits = 0;

            cmd.GetTemporaryRT(Shader.PropertyToID(particleRT.name), blitTargetDescriptor, FilterMode.Bilinear);

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
            particleRT.Release();
        }
    }

    class SurfacePass : ScriptableRenderPass
    {
        public Settings Settings;

        RTHandle surfaceRT;

        FilteringSettings densityFilteringSettings;
        RenderStateBlock renderStateBlock;
        readonly List<ShaderTagId> _shaderTagIds = new List<ShaderTagId>();

        public SurfacePass(Settings settings)
        {
            Settings = settings;

            densityFilteringSettings = new FilteringSettings(null, settings.layerMask);
            renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            _shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForward"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            surfaceRT = RTHandles.Alloc(new RenderTargetIdentifier("_SurfaceRT"), "_SurfaceRT");
            ConfigureTarget(surfaceRT);
            //ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            base.Configure(cmd, cameraTextureDescriptor);
            
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Volumetric Lighting Surface Pass");

            RenderTextureDescriptor blitTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            int width = blitTargetDescriptor.width /= Settings.downSample;
            int height = blitTargetDescriptor.height /= Settings.downSample;

            cmd.GetTemporaryRT(Shader.PropertyToID(surfaceRT.name), width, height, 0, FilterMode.Bilinear);

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
            surfaceRT.Release();
        }
    }

    class CompositePass : ScriptableRenderPass
    {
        public Settings Settings;

        RTHandle source;
        RTHandle full0;
        RTHandle full1;
        RTHandle low0;
        RTHandle low1;
        RTHandle surface;
        RTHandle lowDepth;

        GlobalKeyword directionalLightKeyword;
        GlobalKeyword additionalLightsKeyword;

        public CompositePass(Settings settings)
        {
            Settings = settings;
            directionalLightKeyword = GlobalKeyword.Create("DIRECTIONAL_LIGHT_VOLUMETRICS");
            additionalLightsKeyword = GlobalKeyword.Create("ADDITIONAL_LIGHTS_VOLUMETRICS");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            source = renderingData.cameraData.renderer.cameraDepthTargetHandle;
            full0 = RTHandles.Alloc(new RenderTargetIdentifier("_Full0"), "_Full0");
            full1 = RTHandles.Alloc(new RenderTargetIdentifier("_Full1"), "_Full1");
            low0 = RTHandles.Alloc(new RenderTargetIdentifier("_Low0"), "_Low0");
            low1 = RTHandles.Alloc(new RenderTargetIdentifier("_Low1"), "_Low1");
            surface = RTHandles.Alloc(new RenderTargetIdentifier("_Surface"), "_Surface");
            lowDepth = RTHandles.Alloc(new RenderTargetIdentifier("_LowDepth"), "_LowDepth");

            // ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Volumetric Lighting Pass");
            cmd.Clear();

            RenderTextureDescriptor blitTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            int width = blitTargetDescriptor.width;
            int height = blitTargetDescriptor.height;

            if (Camera.current != null) //This is necessary so it uses the proper resolution in the scene window
            {
                width = (int)Camera.current.pixelRect.width / Settings.downSample;
                height = (int)Camera.current.pixelRect.height / Settings.downSample;
            }
            else //regular game window
            {
                width /= Settings.downSample;
                height /= Settings.downSample;
            }

            cmd.GetTemporaryRT(Shader.PropertyToID(full0.name), blitTargetDescriptor);
            cmd.GetTemporaryRT(Shader.PropertyToID(full1.name), blitTargetDescriptor);
            cmd.GetTemporaryRT(Shader.PropertyToID(low0.name), width, height);
            cmd.GetTemporaryRT(Shader.PropertyToID(low1.name), width, height);
            cmd.GetTemporaryRT(Shader.PropertyToID(surface.name), width, height);
            cmd.GetTemporaryRT(Shader.PropertyToID(lowDepth.name), width, height);

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
            Shader.SetKeyword(directionalLightKeyword, Settings.enableDirectionalLight);
            Shader.SetKeyword(additionalLightsKeyword, Settings.enableFogVolumes);

            // fog volume settings
            Shader.SetGlobalInt("_FogVolumetricSteps", Settings.fogVolumeSteps);
            Shader.SetGlobalFloat("_FogVolumeStepLength", Settings.fogVolumeStepLength);
            Shader.SetGlobalFloat("_FogVolumeMaxDistance", Settings.fogVolumeMaxDistance);

            // directional light raymarch
            if(Settings.enableDirectionalLight)
            {
                cmd.Blit(source, low0, Settings.material, 0);
                cmd.SetGlobalTexture("_mainLightVolumetric", low0);
            }

            // additional lights surface
            if(Settings.enableFogVolumes)
            {
                cmd.SetGlobalTexture("_additionalLightsVolumetric", surface);
            }

            // combine volumetric (main light + additional lights)
            cmd.Blit(source, low1, Settings.material, 5);

            // bilateral blur (horizontal then vertical)
            cmd.Blit(low1, low0, Settings.material, 1);
            cmd.Blit(low0, low1, Settings.material, 2);
            cmd.SetGlobalTexture("_combinedVolumetric", low1);

            // downsample depth
            cmd.Blit(source, lowDepth, Settings.material, 4);
            cmd.SetGlobalTexture("_LowResDepth", lowDepth);

            // upsample and composite
            cmd.Blit(source, full0, Settings.material, 3);
            cmd.Blit(full0, source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {

            full0.Release();
            full1.Release();
            low0.Release();
            low1.Release();
            surface.Release();
            lowDepth.Release();
        }
    }

    [System.Serializable]
    public class Settings
    {
        [Header("THIS IS AN EXPERIMENTAL FEATURE")]
        [Header("Layers")]
        public LayerMask layerMask = 1 << 1;
        [HideInInspector] public LayerMask densityMask;

        [Header("Blur")]
        [Range(1, 4)] public int downSample = 2;
        [Range(0, 8)] public int blurSamples = 4;
        public float blurAmount = 1f;

        [Header("Directional Light")]
        public bool enableDirectionalLight = true;
        public float intensity = 0.5f;
        public float maxDistance = 25f;
        public int steps = 24;

        [Header("Fog Volumes")]
        public bool enableFogVolumes = true;
        public int fogVolumeSteps = 64;
        public float fogVolumeStepLength = 0.2f;
        public float fogVolumeMaxDistance = 500f;

        [HideInInspector] public Material material;
        [HideInInspector] public Material blurMaterial;
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
        if (settings.blurMaterial == null) settings.blurMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/KawaseBlur"));

        densityPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        compositePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        //renderer.EnqueuePass(densityPass);
        if(settings.enableFogVolumes) renderer.EnqueuePass(surfacePass);
        renderer.EnqueuePass(compositePass);
    }
}