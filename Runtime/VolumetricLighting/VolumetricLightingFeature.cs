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
        RenderTargetIdentifier tmpRT0;
        RenderTargetIdentifier downSampleRT;

        FilteringSettings filteringSettings;
        RenderStateBlock renderStateBlock;
        readonly List<ShaderTagId> _shaderTagIds = new List<ShaderTagId>();

        int tmpRT0ID = Shader.PropertyToID("tmpRT0ID");
        int downSampleRTID = Shader.PropertyToID("downSampleRT");

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

            int downSample = 4;
            int width = blitTargetDescriptor.width / downSample;
            int height = blitTargetDescriptor.height / downSample;

            source = renderingData.cameraData.renderer.cameraColorTarget;
            cmd.GetTemporaryRT(tmpRT0ID, blitTargetDescriptor, FilterMode.Bilinear);
            cmd.GetTemporaryRT(downSampleRTID, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);

            tmpRT0 = new RenderTargetIdentifier(tmpRT0ID);
            downSampleRT = new RenderTargetIdentifier(downSampleRTID);

            ConfigureTarget(tmpRT0);
            ConfigureTarget(downSampleRT);

            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Volumetric Lighting Feature");

            SortingCriteria sortingCriteria = SortingCriteria.CommonOpaque;
            DrawingSettings drawingSettings = CreateDrawingSettings(_shaderTagIds, ref renderingData, sortingCriteria);

            context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);

            cmd.SetGlobalTexture("_VolumetricLightingContribution", downSampleRT);
            cmd.Blit(source, tmpRT0, Settings.compositeMaterial, 0);
            cmd.Blit(tmpRT0, source);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(tmpRT0ID);
            cmd.ReleaseTemporaryRT(downSampleRTID);
        }
    }

    [System.Serializable]
    public class Settings
    {
        public LayerMask layerMask;
        public Material compositeMaterial;
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

        pass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        renderer.EnqueuePass(pass);
    }
}