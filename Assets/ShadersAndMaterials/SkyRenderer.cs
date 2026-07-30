using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class MySkyRenderer : SkyRenderer
{
    Material material;

    public override void Build()
    {
        material = CoreUtils.CreateEngineMaterial(
            Shader.Find("Custom/Skybox"));
    }

    public override void Cleanup()
    {
        CoreUtils.Destroy(material);
    }

    public override void RenderSky(
        BuiltinSkyParameters builtinParams,
        bool renderForCubemap,
        bool renderSunDisk)
    {
        var sky = builtinParams.skySettings as MySkySettings;

        material.SetColor("_SunColor", sky.sunColor.value);
        material.SetFloat("_MoonExposure", sky.moonExposure.value);

        CoreUtils.DrawFullScreen(
            builtinParams.commandBuffer,
            material);
    }
}