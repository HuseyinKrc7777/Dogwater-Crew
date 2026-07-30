using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[System.Serializable]
[VolumeComponentMenu("Sky/My Sky")]
public class MySkySettings : SkySettings
{
    public ColorParameter sunColor = new ColorParameter(Color.white);

    public CubemapParameter moonCubemap = new CubemapParameter(null);

    public FloatParameter moonExposure = new FloatParameter(0);

    public override Type GetSkyRendererType()
    {
        return typeof(SkyRenderer);
    }

    public override int GetHashCode()
    {
        int hash = base.GetHashCode();

        hash = hash * 23 + sunColor.value.GetHashCode();
        hash = hash * 23 + moonExposure.value.GetHashCode();

        return hash;
    }
}