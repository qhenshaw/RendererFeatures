using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VolumetricLightingFeature : ScriptableRendererFeature
{
    class Pass : ScriptableRenderPass
    {
        public Settings Settings;

        RenderTargetIdentifier source;
        RenderTargetIdentifier fullResRT0;
        RenderTargetIdentifier downSampleRT0;
        RenderTargetIdentifier downSampleRT1;

        FilteringSettings filteringSettings;
        RenderStateBlock renderStateBlock;
        readonly List<ShaderTagId> _shaderTagIds = new List<ShaderTagId>();

        int fullResRT0ID = Shader.PropertyToID("fullResRT0");
        int downSampleRT0ID = Shader.PropertyToID("downSampleRT0");
        int downSampleRT1ID = Shader.PropertyToID("downSampleRt1");

        int drawLayer;

        public Pass(Settings settings)
        {
            Settings = settings;

            filteringSettings = new FilteringSettings(null, settings.layerMask);
            renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            _shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor blitTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            int width = blitTargetDescriptor.width / Settings.downSample;
            int height = blitTargetDescriptor.height / Settings.downSample;

            source = renderingData.cameraData.renderer.cameraColorTarget;
            cmd.GetTemporaryRT(fullResRT0ID, blitTargetDescriptor, FilterMode.Bilinear);
            cmd.GetTemporaryRT(downSampleRT0ID, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
            cmd.GetTemporaryRT(downSampleRT1ID, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);

            fullResRT0 = new RenderTargetIdentifier(fullResRT0ID);
            downSampleRT0 = new RenderTargetIdentifier(downSampleRT0ID);
            downSampleRT1 = new RenderTargetIdentifier(downSampleRT1ID);

            ConfigureTarget(fullResRT0);
            ConfigureTarget(downSampleRT0);
            ConfigureTarget(downSampleRT1);

            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Volumetric Lighting Feature");

            // draw volumetric areas
            SortingCriteria sortingCriteria = SortingCriteria.CommonOpaque;
            DrawingSettings drawingSettings = CreateDrawingSettings(_shaderTagIds, ref renderingData, sortingCriteria);
            context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);

            // blur volumetric color
            if (Settings.blurPasses > 0)
            {
                cmd.SetGlobalFloat("_offset", 0.5f);
                cmd.Blit(downSampleRT1, downSampleRT0, Settings.blurMaterial);

                for (int i = 1; i < Settings.blurPasses - 1; i++)
                {
                    cmd.SetGlobalFloat("_offset", 0.5f + i);
                    cmd.Blit(downSampleRT0, downSampleRT1, Settings.blurMaterial);

                    var temp = downSampleRT0;
                    downSampleRT0 = downSampleRT1;
                    downSampleRT1 = temp;
                }

                cmd.Blit(downSampleRT0, downSampleRT1, Settings.blurMaterial);
            }

            // copy low res depth
            cmd.Blit(source, downSampleRT0, Settings.depthMaterial, 0);
            cmd.SetGlobalTexture("_LowResDepth", downSampleRT0);

            // composite result
            cmd.SetGlobalTexture("_VolumetricLightingContribution", downSampleRT1);
            cmd.Blit(source, fullResRT0, Settings.compositeMaterial, 0);
            cmd.Blit(fullResRT0, source);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(fullResRT0ID);
            cmd.ReleaseTemporaryRT(downSampleRT0ID);
            cmd.ReleaseTemporaryRT(downSampleRT1ID);
        }
    }

    [System.Serializable]
    public class Settings
    {
        public LayerMask layerMask;
        public Material compositeMaterial;
        public Material depthMaterial;
        [Range(1, 8)] public int downSample = 4;
        [Range(0, 8)] public int blurPasses = 4;
        [HideInInspector] public Material blurMaterial;
    }

    public Settings settings = new Settings();
    Pass pass;

    public override void Create()
    {
        pass = new Pass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.compositeMaterial == null) return;
        if (settings.depthMaterial == null) return;
        if (settings.blurMaterial == null) settings.blurMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/KawaseBlur"));

        pass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        renderer.EnqueuePass(pass);
    }
}