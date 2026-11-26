#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class iOSPostProcessBuild
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        if (!File.Exists(plistPath)) return;

        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        PlistElementDict rootDict = plist.root;

        rootDict.SetString("NSPhotoLibraryAddUsageDescription", "Aplikasi ini membutuhkan izin untuk menyimpan foto mahkota Nusantara ke Galeri Anda.");
        rootDict.SetString("NSPhotoLibraryUsageDescription", "Aplikasi ini membutuhkan akses Galeri untuk menyimpan dan melihat hasil foto AR.");

        File.WriteAllText(plistPath, plist.WriteToString());
    }
}
#endif
