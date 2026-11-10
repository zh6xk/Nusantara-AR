using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityEditor.Rendering.Universal
{
    class URPAutoUpgradeBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => int.MinValue;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("[URPAutoUpgrade] Executing URP migration via Reflection...");

            try
            {
                // 1. Migrate UniversalRenderPipelineGlobalSettings
                Type globalSettingsType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings, Unity.RenderPipelines.Universal.Runtime");
                if (globalSettingsType != null)
                {
                    MethodInfo ensureMethod = globalSettingsType.GetMethod("Ensure", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    object globalSettings = ensureMethod?.Invoke(null, new object[] { true });

                    if (globalSettings != null)
                    {
                        MethodInfo upgradeMethod = globalSettingsType.GetMethod("UpgradeAsset", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        MethodInfo getInstanceIDMethod = globalSettingsType.GetMethod("GetInstanceID");
                        if (upgradeMethod != null && getInstanceIDMethod != null)
                        {
                            int id = (int)getInstanceIDMethod.Invoke(globalSettings, null);
                            upgradeMethod.Invoke(null, new object[] { id });
                            EditorUtility.SetDirty(globalSettings as UnityEngine.Object);
                            Debug.Log("[URPAutoUpgrade] UniversalRenderPipelineGlobalSettings upgraded.");
                        }
                    }
                }

                // 2. Migrate UniversalRenderPipelineAsset
                Type pipelineAssetType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime");
                if (pipelineAssetType != null)
                {
                    MethodInfo upgradePipelineMethod = pipelineAssetType.GetMethod("UpgradeAsset", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
                    foreach (string guid in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(path, pipelineAssetType);
                        if (asset != null && upgradePipelineMethod != null)
                        {
                            upgradePipelineMethod.Invoke(null, new object[] { asset.GetInstanceID() });
                            EditorUtility.SetDirty(asset);
                            Debug.Log($"[URPAutoUpgrade] Upgraded {path}");
                        }
                    }
                }

                AssetDatabase.SaveAssets();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[URPAutoUpgrade] Reflection migration non-fatal notice: {ex.Message}");
            }
        }
    }
}
