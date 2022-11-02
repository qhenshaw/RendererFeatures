using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class SunLightData : MonoBehaviour
{
    [SerializeField] private Light _light;

    private void OnValidate()
    {
        if (!_light) TryGetComponent(out _light);
    }

    private void Update()
    {
        if (!_light) return;
        Shader.SetGlobalVector("_SunDirection", transform.forward);
        Shader.SetGlobalVector("_SunColor", _light.color);
    }
}
