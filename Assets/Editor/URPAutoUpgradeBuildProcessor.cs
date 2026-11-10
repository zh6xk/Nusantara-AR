using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    class URPAutoUpgradeBuildProcessor : IPreprocessBuildWithReport
    {
        // Must run before URPPreprocessBuild (which is int.MinValue + 100)
        public int callbackOrder => int.MinValue;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("[URPAutoUpgrade] Running automated URP Asset & GlobalSettings upgrade before build...");
            
            // 1. Upgrade Global Settings
            var globalSettings = UniversalRenderPipelineGlobalSettings.Ensure();
            if (globalSettings != null)
            {
                UniversalRenderPipelineGlobalSettings.UpgradeAsset(globalSettings.GetInstanceID());
                EditorUtility.SetDirty(globalSettings);
            }

            // 2. Upgrade all UniversalRenderPipelineAsset in the project
            var guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset != null)
                {
                    UniversalRenderPipelineAsset.UpgradeAsset(asset.GetInstanceID());
                    EditorUtility.SetDirty(asset);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[URPAutoUpgrade] URP Assets successfully upgraded and saved.");
        }
    }
}
