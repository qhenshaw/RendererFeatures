using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RendererFeatures.Editor
{
    [InitializeOnLoad]
    public class LightBakeBlitFix
    {
        private static int[] _enabledFeatures;

        static LightBakeBlitFix()
        {
            Lightmapping.bakeStarted += OnBakeStarted;
            Lightmapping.bakeCompleted += OnBakeCompleted;
        }

        private static void OnBakeStarted()
        {
            List<UniversalRendererData> rendererDatas = FindAllRendererDatas();
            _enabledFeatures = new int[rendererDatas.Count];

            for (int i = 0; i < rendererDatas.Count; i++)
            {
                UniversalRendererData rendererData = rendererDatas[i];
                Debug.Log($"[{rendererData.name}] Renderer Features disabled before light bake:", rendererData);

                if (rendererData != null)
                {
                    for (int j = 0; j < rendererData.rendererFeatures.Count; j++)
                    {
                        ScriptableRendererFeature feature = rendererData.rendererFeatures[j];
                        if (feature.isActive)
                        {
                            _enabledFeatures[i] |= 1 << j;
                            feature.SetActive(false);
                            Debug.Log($"    {feature.name}", feature);
                        }
                    }
                }
            }
        }

        private static void OnBakeCompleted()
        {
            List<UniversalRendererData> rendererDatas = FindAllRendererDatas();

            for (int i = 0; i < rendererDatas.Count; i++)
            {
                UniversalRendererData rendererData = rendererDatas[i];
                Debug.Log($"[{rendererData.name}] Renderer Features enabled after light bake:", rendererData);

                if (rendererData != null)
                {
                    for (int j = 0; j < rendererData.rendererFeatures.Count; j++)
                    {
                        if ((_enabledFeatures[i] & 1 << j) != 0)
                        {
                            ScriptableRendererFeature feature = rendererData.rendererFeatures[j];
                            feature.SetActive(true);
                            Debug.Log($"    {feature.name}", feature);
                        }
                    }
                }
            }
        }

        private static List<UniversalRendererData> FindAllRendererDatas()
        {
            List<UniversalRendererData> rendererDatas = new List<UniversalRendererData>();
            string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData");

            for (int i = 0; i < guids.Length; i++)
            {
                string guid = guids[i];
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UniversalRendererData rendererData = (UniversalRendererData)AssetDatabase.LoadAssetAtPath(path, typeof(UniversalRendererData));
                if (rendererData != null) rendererDatas.Add(rendererData);
            }

            return rendererDatas;
        }
    }
}