using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable, VolumeComponentMenuForRenderPipeline("Custom/Depth Fog", typeof(UniversalRenderPipeline))]
public class DepthFogComponent : VolumeComponent, IPostProcessComponent
{
    public ColorParameter Color = new ColorParameter(new Color(1f, 1f, 1f), true, false, false, false);

    [Header("Depth")]
    public ClampedFloatParameter DepthDensity = new ClampedFloatParameter(0f, 0f, 1f, false);
    public FloatParameter DepthStart = new FloatParameter(5f,false);
    public FloatParameter DepthEnd = new FloatParameter(100f,false);
    public ClampedFloatParameter DepthFalloff = new ClampedFloatParameter(2f, 1f, 4f, false);

    [Header("Height")]
    public ClampedFloatParameter HeightDensity = new ClampedFloatParameter(0f, 0f, 1f, false);
    public FloatParameter HeightStart = new FloatParameter(0f, false);
    public FloatParameter HeightEnd = new FloatParameter(15f, false);
    public ClampedFloatParameter HeightFalloff = new ClampedFloatParameter(2f, 1f, 4f, false);

    public bool IsActive() => DepthDensity.value + HeightDensity.value > 0f;
    public bool IsTileCompatible() => true;
}