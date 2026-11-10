using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.XR.ARFoundation;
using Unity.InferenceEngine;
using TMPro;

public static class FigmaUISetup
{
    // Scale factor from Figma frame (414 x 736) to Portrait Full HD (1080 x 1920)
    // Note: 414 * (1080 / 414) = 1080, and 736 * (1080 / 414) = 1920.0 exactly!
    private const float SCALE = 1080f / 414f;

    private static float S(float val) => Mathf.Round(val * SCALE);
    private static Vector2 SV(float x, float y) => new Vector2(Mathf.Round(x * SCALE), Mathf.Round(y * SCALE));

    [MenuItem("Tools/NusantaraCrown/Build Figma UI")]
    public static void BuildFigmaUI()
    {
        SetGameViewPortrait1080x1920();
        SetupCrownMaterialsAndModels();
        EnsureFigmaSprites();
        BuildCanvasHierarchy();
        Debug.Log("<color=#D4AF37>[FigmaUISetup] NusantaraCrown AR Figma UI (Portrait 1080x1920) & 3D PBR Materials built and wired successfully!</color>");
    }

    [MenuItem("Tools/NusantaraCrown/Fix 3D Crown Materials")]
    public static void FixCrownMaterialsMenu()
    {
        SetupCrownMaterialsAndModels();
    }

    private static void SetGameViewPortrait1080x1920()
    {
        try
        {
            Assembly editorAsm = typeof(Editor).Assembly;
            Type gvSizesType = editorAsm.GetType("UnityEditor.GameViewSizes");
            Type singleType = typeof(ScriptableSingleton<>).MakeGenericType(gvSizesType);
            object gvSizesInstance = singleType.GetProperty("instance")?.GetValue(null, null);
            if (gvSizesInstance == null) return;

            object currentGroupType = gvSizesType.GetProperty("currentGroupType")?.GetValue(gvSizesInstance, null);
            object group = gvSizesType.GetMethod("GetGroup")?.Invoke(gvSizesInstance, new object[] { currentGroupType });
            if (group == null) return;

            Type groupType = group.GetType();
            MethodInfo getTotalCount = groupType.GetMethod("GetTotalCount");
            MethodInfo getGameViewSize = groupType.GetMethod("GetGameViewSize");
            int totalCount = (int)getTotalCount.Invoke(group, null);

            int targetIndex = -1;
            for (int i = 0; i < totalCount; i++)
            {
                object gvSize = getGameViewSize.Invoke(group, new object[] { i });
                if (gvSize == null) continue;
                Type szType = gvSize.GetType();
                int w = (int)szType.GetProperty("width").GetValue(gvSize, null);
                int h = (int)szType.GetProperty("height").GetValue(gvSize, null);
                if (w == 1080 && h == 1920)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex == -1)
            {
                Type gvSizeType = editorAsm.GetType("UnityEditor.GameViewSize");
                Type gvSizeTypeEnum = editorAsm.GetType("UnityEditor.GameViewSizeType");
                object fixedRes = Enum.Parse(gvSizeTypeEnum, "FixedResolution");
                object newSize = Activator.CreateInstance(gvSizeType, new object[] { fixedRes, 1080, 1920, "Portrait 1080x1920" });
                groupType.GetMethod("AddCustomSize")?.Invoke(group, new object[] { newSize });
                targetIndex = (int)getTotalCount.Invoke(group, null) - 1;
            }

            Type gameViewType = editorAsm.GetType("UnityEditor.GameView");
            if (gameViewType != null && targetIndex >= 0)
            {
                UnityEngine.Object[] views = Resources.FindObjectsOfTypeAll(gameViewType);
                MethodInfo sizeCallback = gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                PropertyInfo selectedIdxProp = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                foreach (UnityEngine.Object v in views)
                {
                    if (sizeCallback != null)
                        sizeCallback.Invoke(v, new object[] { targetIndex, null });
                    else if (selectedIdxProp != null)
                        selectedIdxProp.SetValue(v, targetIndex, null);

                    if (v is EditorWindow ew)
                        ew.Repaint();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[FigmaUISetup] Could not auto-set GameView resolution: " + ex.Message);
        }
    }

    private static void EnsureFigmaSprites()
    {
        string folderPath = Path.Combine(Application.dataPath, "UI/Figma");
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        Color surfaceDark = new Color32(18, 18, 18, 240);
        Color borderSubtle = new Color(1f, 1f, 1f, 0.15f);
        Color gold = new Color32(212, 175, 55, 255);
        Color cardFillDefault = new Color32(30, 29, 28, 238);
        Color cardFillActive = new Color32(78, 64, 28, 242);

        // 1. Generate clean vector circular buttons (100% transparent outside circle)
        GenerateCircleIconButton(Path.Combine(folderPath, "Btn_Gear.png"), 132, surfaceDark, borderSubtle, Color.white, "sun");
        GenerateCircleIconButton(Path.Combine(folderPath, "Btn_Info.png"), 132, surfaceDark, borderSubtle, Color.white, "info");
        GenerateCircleIconButton(Path.Combine(folderPath, "Btn_Flip.png"), 144, new Color(1f, 1f, 1f, 0.12f), new Color(1f, 1f, 1f, 0.08f), Color.white, "flip");
        GenerateShutterButton(Path.Combine(folderPath, "Btn_Shutter.png"), 204, gold, Color.white);
        GenerateCircleIconButton(Path.Combine(folderPath, "Btn_Gallery.png"), 144, new Color(1f, 1f, 1f, 0.12f), new Color(1f, 1f, 1f, 0.08f), Color.white, "gallery");
        GenerateAudioPillButton(Path.Combine(folderPath, "Btn_Audio.png"), 232, 68, gold);

        // 2. Generate crisp vector Crown Icon (matching Figma Crown Logo node 4:5 / 2:32)
        GenerateCrownIcon(Path.Combine(folderPath, "Icon_Crown.png"), 240, 150);

        // 3. Generate Capture Preview & Reticle sprites matching Figma nodes 5:233, 5:241, 5:246, 5:251, 4:51
        GenerateCircleIconButton(Path.Combine(folderPath, "Btn_Close.png"), 132, surfaceDark, borderSubtle, Color.white, "close");
        GenerateCircleIconButton(Path.Combine(folderPath, "Btn_Retake.png"), 156, new Color(1f, 1f, 1f, 0.12f), Color.clear, Color.white, "retake");
        GenerateCircleIconButton(Path.Combine(folderPath, "Btn_Save.png"), 192, gold, Color.clear, new Color32(18, 18, 18, 255), "download");
        GenerateCircleIconButton(Path.Combine(folderPath, "Btn_Share.png"), 156, new Color(1f, 1f, 1f, 0.12f), Color.clear, Color.white, "share");
        GenerateFaceReticle(Path.Combine(folderPath, "Face_Reticle.png"), 240, 320);

        // 4. Generate exact aspect-ratio rounded UI containers (zero 9-slice distortion!)
        GenerateRoundedRect(Path.Combine(folderPath, "Pill_Dark.png"), 490, 68, 34f, surfaceDark, borderSubtle, 2.5f, false);
        GenerateRoundedRect(Path.Combine(folderPath, "Pill_Status.png"), 312, 68, 34f, surfaceDark, borderSubtle, 2.5f, false);
        GenerateRoundedRect(Path.Combine(folderPath, "Pill_Preview.png"), 420, 68, 34f, surfaceDark, borderSubtle, 2.5f, false);
        GenerateRoundedRect(Path.Combine(folderPath, "Pill_Gold.png"), 214, 58, 29f, gold, gold, 0f, false);
        GenerateRoundedRect(Path.Combine(folderPath, "Pill_GenderTrack.png"), 440, 70, 35f, new Color32(34, 34, 34, 248), new Color(1f, 1f, 1f, 0.08f), 2f, false);
        GenerateRoundedRect(Path.Combine(folderPath, "Pill_SecondaryBtn.png"), 736, 104, 52f, new Color32(36, 36, 36, 255), new Color(1f, 1f, 1f, 0.2f), 2.5f, false);
        GenerateRoundedRect(Path.Combine(folderPath, "Card_Active.png"), 236, 172, 32f, cardFillActive, gold, 4f, false);
        GenerateRoundedRect(Path.Combine(folderPath, "Card_Default.png"), 208, 156, 32f, cardFillDefault, borderSubtle, 2.5f, false);
        GenerateRoundedRect(Path.Combine(folderPath, "Dock_Surface.png"), 828, 536, 56f, new Color32(18, 18, 18, 246), borderSubtle, 2.5f, true);
        GenerateRoundedRect(Path.Combine(folderPath, "Drawer_Surface.png"), 828, 880, 56f, new Color32(20, 20, 20, 250), borderSubtle, 2.5f, true);
        GenerateRoundedRect(Path.Combine(folderPath, "Preview_Dock_Surface.png"), 828, 340, 56f, new Color32(18, 18, 18, 246), borderSubtle, 2.5f, true);
        GenerateRoundedRect(Path.Combine(folderPath, "Circle_GreenDot.png"), 64, 64, 32f, new Color32(46, 204, 113, 255), new Color32(46, 204, 113, 255), 0f, false);
        GenerateRoundedRect(Path.Combine(folderPath, "Grabber_Pill.png"), 132, 16, 8f, new Color(1f, 1f, 1f, 0.3f), Color.clear, 0f, false);

        AssetDatabase.Refresh();

        string[] allFiles = Directory.GetFiles(folderPath, "*.png");
        foreach (string f in allFiles)
        {
            string assetPath = "Assets/UI/Figma/" + Path.GetFileName(f);
            TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.spriteBorder = Vector4.zero;
                TextureImporterSettings tis = new TextureImporterSettings();
                ti.ReadTextureSettings(tis);
                tis.spriteMeshType = SpriteMeshType.FullRect;
                ti.SetTextureSettings(tis);
                ti.SaveAndReimport();
            }
        }
    }

    private static void GenerateShutterButton(string path, int size, Color innerGold, Color outerWhite)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float outerR = size * 0.47f;
        float ringInnerR = size * 0.415f;
        float innerDiscR = size * 0.365f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float r = Vector2.Distance(p, center);
                Color col = Color.clear;

                // Outer white ring
                if (r <= outerR + 1f && r >= ringInnerR - 1f)
                {
                    float aOuter = Mathf.Clamp01(outerR - r + 0.5f);
                    float aInner = Mathf.Clamp01(r - ringInnerR + 0.5f);
                    float ringA = aOuter * aInner;
                    col = new Color(outerWhite.r, outerWhite.g, outerWhite.b, ringA);
                }

                // Inner gold circle
                if (r <= innerDiscR + 1f)
                {
                    float discA = Mathf.Clamp01(innerDiscR - r + 0.5f);
                    col = Color.Lerp(col, new Color(innerGold.r, innerGold.g, innerGold.b, 1f), discA);
                    col.a = Mathf.Max(col.a, discA);
                }

                pixels[y * size + x] = col;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    private static void GenerateAudioPillButton(string path, int w, int h, Color gold)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];
        Color fill = new Color(gold.r, gold.g, gold.b, 0.14f);
        float radius = h * 0.5f;
        Vector2 iconCenter = new Vector2(w * 0.18f, h * 0.5f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dist = SignedDistanceRoundedRect(x + 0.5f, y + 0.5f, w, h, radius, false);
                if (dist > 0.5f)
                {
                    pixels[y * w + x] = Color.clear;
                    continue;
                }
                float outerAlpha = Mathf.Clamp01(0.5f - dist);
                Color c = fill;
                float borderMix = Mathf.Clamp01(dist + 2.5f + 0.5f);
                c = Color.Lerp(fill, new Color(gold.r, gold.g, gold.b, 1f), borderMix);
                c.a = Mathf.Lerp(fill.a, 1f, borderMix);

                // Speaker icon glyph on left side
                Vector2 np = (new Vector2(x + 0.5f, y + 0.5f) - iconCenter) / (h * 0.5f);
                float spkDist = 999f;
                // Speaker box + cone
                if (np.x >= -0.22f && np.x <= -0.06f && Mathf.Abs(np.y) <= 0.12f)
                    spkDist = 0f;
                if (np.x > -0.06f && np.x <= 0.12f && Mathf.Abs(np.y) <= Mathf.Lerp(0.12f, 0.28f, (np.x + 0.06f) / 0.18f))
                    spkDist = 0f;
                // Sound wave arc
                float waveR = Vector2.Distance(np, new Vector2(0.05f, 0f));
                if (np.x > 0.14f && Mathf.Abs(np.y) < 0.24f)
                    spkDist = Mathf.Min(spkDist, Mathf.Abs(waveR - 0.24f) - 0.03f);

                if (spkDist < 0.03f)
                {
                    float im = Mathf.Clamp01((0.03f - spkDist) * 20f);
                    c = Color.Lerp(c, gold, im);
                    c.a = Mathf.Max(c.a, im);
                }

                c.a *= outerAlpha;
                pixels[y * w + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    private static void GenerateCrownIcon(string path, int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];

        Color goldTop = new Color32(238, 212, 125, 255);
        Color goldBottom = new Color32(189, 146, 46, 255);
        Color goldBand = new Color32(218, 182, 78, 255);
        Color pearlCol = new Color32(242, 226, 162, 255);
        Color rubyCol = new Color32(128, 28, 38, 255);

        Vector2 leftTip = new Vector2(w * 0.14f, h * 0.72f);
        Vector2 midTip = new Vector2(w * 0.50f, h * 0.88f);
        Vector2 rightTip = new Vector2(w * 0.86f, h * 0.72f);
        Vector2 valleyL = new Vector2(w * 0.34f, h * 0.42f);
        Vector2 valleyR = new Vector2(w * 0.66f, h * 0.42f);
        float baseLeft = w * 0.10f;
        float baseRight = w * 0.90f;
        float bandBottom = h * 0.10f;
        float bandTop = h * 0.25f;

        Vector2 ruby1 = new Vector2(w * 0.33f, h * 0.33f);
        Vector2 ruby2 = new Vector2(w * 0.50f, h * 0.35f);
        Vector2 ruby3 = new Vector2(w * 0.67f, h * 0.33f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                Color col = Color.clear;

                if (p.y >= bandTop - 2f && p.y <= h * 0.88f)
                {
                    float tLeft = Mathf.InverseLerp(bandTop, leftTip.y, p.y);
                    float minX = Mathf.Lerp(baseLeft + 4f, leftTip.x, tLeft);
                    float maxX = Mathf.Lerp(baseRight - 4f, rightTip.x, tLeft);

                    if (p.x >= minX && p.x <= maxX)
                    {
                        float roofY;
                        if (p.x <= valleyL.x)
                            roofY = Mathf.Lerp(leftTip.y, valleyL.y, Mathf.InverseLerp(leftTip.x, valleyL.x, p.x));
                        else if (p.x <= midTip.x)
                            roofY = Mathf.Lerp(valleyL.y, midTip.y, Mathf.InverseLerp(valleyL.x, midTip.x, p.x));
                        else if (p.x <= valleyR.x)
                            roofY = Mathf.Lerp(midTip.y, valleyR.y, Mathf.InverseLerp(midTip.x, valleyR.x, p.x));
                        else
                            roofY = Mathf.Lerp(valleyR.y, rightTip.y, Mathf.InverseLerp(valleyR.x, rightTip.x, p.x));

                        float aa = Mathf.Clamp01(roofY - p.y + 1f);
                        if (aa > 0f)
                        {
                            float grad = Mathf.InverseLerp(bandTop, midTip.y, p.y);
                            col = Color.Lerp(goldBottom, goldTop, grad);
                            col.a = aa;
                        }
                    }
                }

                float rDist = Mathf.Min(Vector2.Distance(p, ruby1), Mathf.Min(Vector2.Distance(p, ruby2), Vector2.Distance(p, ruby3)));
                if (rDist < h * 0.055f)
                {
                    float ra = Mathf.Clamp01(h * 0.055f - rDist + 0.5f);
                    col = Color.Lerp(col, rubyCol, ra);
                }

                float pDist = Mathf.Min(Vector2.Distance(p, leftTip), Mathf.Min(Vector2.Distance(p, midTip), Vector2.Distance(p, rightTip)));
                if (pDist < h * 0.075f)
                {
                    float pa = Mathf.Clamp01(h * 0.075f - pDist + 0.5f);
                    col = Color.Lerp(col, pearlCol, pa);
                    col.a = Mathf.Max(col.a, pa);
                }

                float barCenterY = (bandBottom + bandTop) * 0.5f;
                float barHalfH = (bandTop - bandBottom) * 0.5f;
                float barHalfW = (baseRight - baseLeft) * 0.5f;
                float barCenterX = (baseLeft + baseRight) * 0.5f;
                float qx = Mathf.Abs(p.x - barCenterX) - (barHalfW - barHalfH);
                float qy = Mathf.Abs(p.y - barCenterY);
                float barDist = new Vector2(Mathf.Max(qx, 0f), qy).magnitude - barHalfH;
                if (barDist < 0.5f)
                {
                    float ba = Mathf.Clamp01(0.5f - barDist);
                    float vShade = 1f - 0.25f * Mathf.Abs((p.y - barCenterY) / barHalfH);
                    Color bc = goldBand * vShade;
                    bc.a = 1f;
                    col = Color.Lerp(col, bc, ba);
                    col.a = Mathf.Max(col.a, ba);
                }

                pixels[y * w + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    private static void GenerateCircleIconButton(string path, int size, Color bgFill, Color borderCol, Color iconCol, string iconType)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f - 1.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Vector2.Distance(p, center) - radius;
                if (d > 0.5f)
                {
                    pixels[y * size + x] = Color.clear;
                    continue;
                }
                float alpha = Mathf.Clamp01(0.5f - d);
                Color c = bgFill;
                if (borderCol.a > 0f && d > -3f)
                {
                    float bm = Mathf.Clamp01(d + 3f);
                    c = Color.Lerp(bgFill, new Color(borderCol.r, borderCol.g, borderCol.b, 1f), bm * borderCol.a);
                    c.a = Mathf.Lerp(bgFill.a, Mathf.Max(bgFill.a, borderCol.a), bm);
                }

                Vector2 np = (p - center) / (size * 0.5f); // -1..1
                float iconDist = 999f;

                if (iconType == "sun")
                {
                    float r = np.magnitude;
                    iconDist = Mathf.Abs(r - 0.16f);
                    for (int k = 0; k < 8; k++)
                    {
                        float ang = k * Mathf.PI * 0.25f;
                        Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        iconDist = Mathf.Min(iconDist, DistToSegment(np, dir * 0.25f, dir * 0.36f));
                    }
                }
                else if (iconType == "info")
                {
                    float r = np.magnitude;
                    iconDist = Mathf.Abs(r - 0.36f);
                    iconDist = Mathf.Min(iconDist, DistToSegment(np, new Vector2(0f, -0.18f), new Vector2(0f, 0.04f)));
                    iconDist = Mathf.Min(iconDist, Vector2.Distance(np, new Vector2(0f, 0.18f)) - 0.02f);
                }
                else if (iconType == "flip")
                {
                    float r = np.magnitude;
                    float ang = Mathf.Atan2(np.y, np.x) * Mathf.Rad2Deg;
                    if ((ang > 15f && ang < 155f) || (ang < -25f && ang > -165f))
                        iconDist = Mathf.Abs(r - 0.27f);
                    // Arrowheads
                    iconDist = Mathf.Min(iconDist, DistToSegment(np, new Vector2(-0.24f, 0.11f), new Vector2(-0.33f, 0.22f)));
                    iconDist = Mathf.Min(iconDist, DistToSegment(np, new Vector2(-0.24f, 0.11f), new Vector2(-0.14f, 0.21f)));
                    iconDist = Mathf.Min(iconDist, DistToSegment(np, new Vector2(0.24f, -0.11f), new Vector2(0.33f, -0.22f)));
                    iconDist = Mathf.Min(iconDist, DistToSegment(np, new Vector2(0.24f, -0.11f), new Vector2(0.14f, -0.21f)));
                }
                else if (iconType == "gallery")
                {
                    // Rounded square photo frame
                    Vector2 q = new Vector2(Mathf.Abs(np.x), Mathf.Abs(np.y)) - new Vector2(0.22f, 0.18f);
                    float boxDist = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - 0.06f;
                    iconDist = Mathf.Abs(boxDist);
                    // Mountain polyline inside frame
                    iconDist = Mathf.Min(iconDist, DistToSegment(np, new Vector2(-0.22f, -0.14f), new Vector2(-0.06f, 0.04f)));
                    iconDist = Mathf.Min(iconDist, DistToSegment(np, new Vector2(-0.06f, 0.04f), new Vector2(0.06f, -0.06f)));
                    iconDist = Mathf.Min(iconDist, DistToSegment(np, new Vector2(0.06f, -0.06f), new Vector2(0.14f, 0.02f)));
                    iconDist = Mathf.Min(iconDist, DistToSegment(np, new Vector2(0.14f, 0.02f), new Vector2(0.24f, -0.12f)));
                    // Sun dot
                    iconDist = Mathf.Min(iconDist, Vector2.Distance(np, new Vector2(0.11f, 0.11f)) - 0.025f);
                }
                else if (iconType == "close")
                {
                    if (Mathf.Abs(np.x) < 0.24f && Mathf.Abs(np.y) < 0.24f)
                        iconDist = Mathf.Min(Mathf.Abs(np.x - np.y), Mathf.Abs(np.x + np.y)) * 0.7071f;
                }
                else if (iconType == "download")
                {
                    if (Mathf.Abs(np.y - 0.04f) < 0.18f)
                        iconDist = Mathf.Min(iconDist, Mathf.Abs(np.x));
                    if (np.y < 0.04f && np.y > -0.14f && Mathf.Abs(np.x) < 0.16f)
                        iconDist = Mathf.Min(iconDist, Mathf.Abs(Mathf.Abs(np.x) - (np.y + 0.14f)) * 0.7071f);
                    if (Mathf.Abs(np.x) < 0.20f)
                        iconDist = Mathf.Min(iconDist, Mathf.Abs(np.y + 0.22f));
                }
                else if (iconType == "retake")
                {
                    float r = np.magnitude;
                    if (!(np.x > 0.05f && np.y > 0.05f))
                        iconDist = Mathf.Abs(r - 0.22f);
                }
                else if (iconType == "share")
                {
                    Vector2 p1 = new Vector2(-0.16f, 0f);
                    Vector2 p2 = new Vector2(0.14f, 0.16f);
                    Vector2 p3 = new Vector2(0.14f, -0.16f);
                    iconDist = Mathf.Min(Vector2.Distance(np, p1) - 0.04f,
                        Mathf.Min(Vector2.Distance(np, p2) - 0.04f, Vector2.Distance(np, p3) - 0.04f));
                    iconDist = Mathf.Min(iconDist, DistToSegment(np, p1, p2));
                    iconDist = Mathf.Min(iconDist, DistToSegment(np, p1, p3));
                }

                float strokeW = 3.2f / size;
                if (iconDist < strokeW)
                {
                    float im = Mathf.Clamp01((strokeW - iconDist) * size * 0.5f);
                    c = Color.Lerp(c, iconCol, im);
                    c.a = Mathf.Max(c.a, im);
                }

                c.a *= alpha;
                pixels[y * size + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude;
    }

    private static void GenerateFaceReticle(string path, int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];
        Color brass = new Color32(197, 160, 89, 220);
        Vector2 c = new Vector2(w * 0.5f, h * 0.5f);
        float rx = w * 0.46f;
        float ry = h * 0.46f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f - c.x) / rx;
                float dy = (y + 0.5f - c.y) / ry;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (angle < 0f) angle += 360f;
                bool dash = (Mathf.FloorToInt(angle / 7.5f) % 2) == 0;
                float dist = Mathf.Abs(r - 1f) * Mathf.Min(rx, ry);
                if (dash && dist < 2.2f)
                {
                    Color col = brass;
                    col.a *= Mathf.Clamp01(2.2f - dist);
                    pixels[y * w + x] = col;
                }
                else
                {
                    pixels[y * w + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    private static Sprite LoadFigmaSprite(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Figma/" + fileName);
    }

    private static void BuildCanvasHierarchy()
    {
        // Set Main Camera background to Figma Dark Studio (#1C1917) so Editor preview has no bright yellow screen
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color32(28, 25, 23, 255);
            EditorUtility.SetDirty(mainCam);
        }

        // Configure Directional Light for warm museum PBR lighting without blowing out metallic gold
        GameObject dirLightGO = GameObject.Find("Directional Light");
        if (dirLightGO != null)
        {
            Light l = dirLightGO.GetComponent<Light>();
            if (l != null)
            {
                l.intensity = 1.15f;
                l.color = new Color(1f, 0.96f, 0.88f, 1f);
                EditorUtility.SetDirty(l);
            }
            dirLightGO.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
            EditorUtility.SetDirty(dirLightGO.transform);
        }

        // Ensure Main Camera ARCameraManager defaults to Front (User) camera for Android/iOS
        Camera mainCamForAR = Camera.main;
        if (mainCamForAR != null)
        {
            ARCameraManager arCamMgr = mainCamForAR.GetComponent<ARCameraManager>();
            if (arCamMgr != null)
            {
                arCamMgr.requestedFacingDirection = CameraFacingDirection.User;
                EditorUtility.SetDirty(arCamMgr);
            }
        }

        // Find or create the foreground UI Canvas specifically by name "Canvas"
        GameObject fgCanvasGO = GameObject.Find("Canvas");
        Canvas canvas = fgCanvasGO != null ? fgCanvasGO.GetComponent<Canvas>() : null;
        if (canvas == null)
        {
            fgCanvasGO = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = fgCanvasGO.GetComponent<Canvas>();
        }
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Remove any legacy Canvas_CameraBG that would occlude 3D meshes in URP
        GameObject oldBgCanvasGO = GameObject.Find("Canvas_CameraBG");
        if (oldBgCanvasGO != null)
        {
            UnityEngine.Object.DestroyImmediate(oldBgCanvasGO);
        }

        // Create or update 3D Background Quad ("Webcam_3D_Background") parented to Main Camera at Z = 12f
        MeshRenderer bgQuadRenderer = null;
        if (mainCamForAR != null)
        {
            Transform existingQuad = mainCamForAR.transform.Find("Webcam_3D_Background");
            GameObject quadGO;
            if (existingQuad != null)
            {
                quadGO = existingQuad.gameObject;
            }
            else
            {
                quadGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quadGO.name = "Webcam_3D_Background";
                Collider col = quadGO.GetComponent<Collider>();
                if (col != null)
                    UnityEngine.Object.DestroyImmediate(col);
                quadGO.transform.SetParent(mainCamForAR.transform, false);
            }

            quadGO.transform.localPosition = new Vector3(0f, 0f, 12f);
            quadGO.transform.localRotation = Quaternion.identity;
            quadGO.transform.localScale = new Vector3(24.6f, 13.86f, 1f);

            bgQuadRenderer = quadGO.GetComponent<MeshRenderer>();
            bgQuadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bgQuadRenderer.receiveShadows = false;

            string webcamMatPath = "Assets/Crowns/Materials/Webcam_Unlit_Mat.mat";
            Material webcamMat = AssetDatabase.LoadAssetAtPath<Material>(webcamMatPath);
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            if (webcamMat == null)
            {
                webcamMat = new Material(unlitShader);
                AssetDatabase.CreateAsset(webcamMat, webcamMatPath);
            }
            else if (webcamMat.shader != unlitShader)
            {
                webcamMat.shader = unlitShader;
            }
            webcamMat.color = new Color32(28, 25, 23, 255);
            if (webcamMat.HasProperty("_BaseColor"))
                webcamMat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(webcamMat);
            bgQuadRenderer.sharedMaterial = webcamMat;
        }

        CrownARController controller = UnityEngine.Object.FindAnyObjectByType<CrownARController>();
        if (controller == null)
        {
            GameObject ctrlGO = GameObject.Find("AR_Controller") ?? new GameObject("AR_Controller");
            controller = ctrlGO.GetComponent<CrownARController>() ?? ctrlGO.AddComponent<CrownARController>();
        }

        controller.webcamBackgroundRenderer = bgQuadRenderer;
        controller.faceDetectorModel = AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/Models/face_detection_yunet_2023mar.onnx");

        PopulateCrownDatabase(controller);

        for (int i = canvas.transform.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(canvas.transform.GetChild(i).gameObject);
        }

        Sprite sprBtnGear = LoadFigmaSprite("Btn_Gear.png");
        Sprite sprBtnInfo = LoadFigmaSprite("Btn_Info.png");
        Sprite sprBtnShutter = LoadFigmaSprite("Btn_Shutter.png");
        Sprite sprBtnFlip = LoadFigmaSprite("Btn_Flip.png");
        Sprite sprBtnGallery = LoadFigmaSprite("Btn_Gallery.png");
        Sprite sprIconCrown = LoadFigmaSprite("Icon_Crown.png");
        Sprite sprBtnAudio = LoadFigmaSprite("Btn_Audio.png");
        Sprite sprBtnClose = LoadFigmaSprite("Btn_Close.png");
        Sprite sprBtnRetake = LoadFigmaSprite("Btn_Retake.png");
        Sprite sprBtnSave = LoadFigmaSprite("Btn_Save.png");
        Sprite sprBtnShare = LoadFigmaSprite("Btn_Share.png");
        Sprite sprFaceReticle = LoadFigmaSprite("Face_Reticle.png");

        Sprite sprPillDark = LoadFigmaSprite("Pill_Dark.png");
        Sprite sprPillStatus = LoadFigmaSprite("Pill_Status.png");
        Sprite sprPillPreview = LoadFigmaSprite("Pill_Preview.png");
        Sprite sprPillGold = LoadFigmaSprite("Pill_Gold.png");
        Sprite sprPillGenderTrack = LoadFigmaSprite("Pill_GenderTrack.png");
        Sprite sprPillSecondaryBtn = LoadFigmaSprite("Pill_SecondaryBtn.png");
        Sprite sprCardActive = LoadFigmaSprite("Card_Active.png");
        Sprite sprCardDefault = LoadFigmaSprite("Card_Default.png");
        Sprite sprDockSurface = LoadFigmaSprite("Dock_Surface.png");
        Sprite sprDrawerSurface = LoadFigmaSprite("Drawer_Surface.png");
        Sprite sprPreviewDockSurface = LoadFigmaSprite("Preview_Dock_Surface.png");
        Sprite sprGreenDot = LoadFigmaSprite("Circle_GreenDot.png");
        Sprite sprGrabber = LoadFigmaSprite("Grabber_Pill.png");

        Color colorGold = new Color32(212, 175, 55, 255);
        Color colorBrass = new Color32(197, 160, 89, 255);
        Color colorSecondaryText = new Color32(160, 160, 160, 255);
        Color colorDarkText = new Color32(18, 18, 18, 255);

        // =========================================================================
        // 1. TOP FLOATING HEADER (HUD_Top) — Portrait 1080x1920 scaled from 414x736
        // =========================================================================
        RectTransform hudTop = CreateRect("HUD_Top", canvas.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, SV(0f, 88f));

        RectTransform btnGearRT = CreateImage("Btn_Gear", hudTop, sprBtnGear, Image.Type.Simple, true,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), SV(16f, -44f), SV(44f, 44f));
        Button btnGear = btnGearRT.gameObject.AddComponent<Button>();
        UnityEventTools.AddPersistentListener(btnGear.onClick, controller.ToggleFlipOrRotation);

        RectTransform headerPillRT = CreateImage("Pill_HeaderTitle", hudTop, sprPillDark, Image.Type.Simple, false,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), SV(0f, -49f), SV(245f, 34f));
        TextMeshProUGUI txtHeaderPill = CreateTMP("Txt_HeaderTitle", headerPillRT, "Suntiang Gadang — Minangkabau", S(13f), FontStyles.Bold, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, SV(-24f, 0f));
        txtHeaderPill.enableAutoSizing = true;
        txtHeaderPill.fontSizeMin = S(10f);
        txtHeaderPill.fontSizeMax = S(13f);

        RectTransform btnInfoRT = CreateImage("Btn_Info", hudTop, sprBtnInfo, Image.Type.Simple, true,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), SV(-16f, -44f), SV(44f, 44f));
        Button btnInfo = btnInfoRT.gameObject.AddComponent<Button>();
        UnityEventTools.AddPersistentListener(btnInfo.onClick, controller.ToggleInfoModal);

        // =========================================================================
        // 2. FACE GUIDE RETICLE & TRACKING STATUS PILL
        // =========================================================================
        RectTransform reticleRT = CreateImage("Face_Guide_Reticle", canvas.transform, sprFaceReticle, Image.Type.Simple, false,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), SV(0f, 58f), SV(240f, 320f));
        reticleRT.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.8f);
        reticleRT.gameObject.SetActive(false);

        RectTransform trackingPillRT = CreateImage("Pill_Status_Tracking", canvas.transform, sprPillStatus, Image.Type.Simple, false,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), SV(0f, 278f), SV(156f, 34f));
        CreateImage("Status_Dot", trackingPillRT, sprGreenDot, Image.Type.Simple, false,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), SV(18f, 0f), SV(10f, 10f));
        TextMeshProUGUI txtTracking = CreateTMP("Txt_TrackingStatus", trackingPillRT, "Wajah Terkunci", S(12.5f), FontStyles.Bold, colorBrass, TextAlignmentOptions.Left,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), SV(12f, 0f), SV(-36f, 0f));

        // =========================================================================
        // 3. BOTTOM INTERACTIVE DOCK (Controls_Bottom) — Figma node 5:90
        // =========================================================================
        RectTransform dockRT = CreateImage("Controls_Bottom", canvas.transform, sprDockSurface, Image.Type.Simple, true,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, SV(0f, 268f));

        // 3A. Gender Segmented Control (220 x 35, y=-15 from top of dock)
        RectTransform genderTrackRT = CreateImage("Gender_Segmented_Control", dockRT, sprPillGenderTrack, Image.Type.Simple, true,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), SV(0f, -15f), SV(220f, 35f));

        RectTransform segFemaleRT = CreateImage("Btn_Gender_Female", genderTrackRT, sprPillGold, Image.Type.Simple, true,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), SV(3f, 0f), SV(107f, 29f));
        Button btnFemale = segFemaleRT.gameObject.AddComponent<Button>();
        btnFemale.transition = Selectable.Transition.None;
        UnityEventTools.AddIntPersistentListener(btnFemale.onClick, controller.SetGender, 0);
        TextMeshProUGUI txtFemale = CreateTMP("Txt_Female", segFemaleRT, "Wanita", S(12f), FontStyles.Bold, colorDarkText, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        RectTransform segMaleRT = CreateImage("Btn_Gender_Male", genderTrackRT, sprPillGold, Image.Type.Simple, true,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), SV(110f, 0f), SV(107f, 29f));
        segMaleRT.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
        Button btnMale = segMaleRT.gameObject.AddComponent<Button>();
        btnMale.transition = Selectable.Transition.None;
        UnityEventTools.AddIntPersistentListener(btnMale.onClick, controller.SetGender, 1);
        TextMeshProUGUI txtMale = CreateTMP("Txt_Male", segMaleRT, "Pria", S(12f), FontStyles.Bold, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // 3B. Carousel Pulau (ScrollRect at y=-68 from top of dock, height 92)
        RectTransform scrollRT = CreateRect("Carousel_Pulau_ScrollView", dockRT,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), SV(0f, -68f), SV(0f, 92f));
        Image scrollTouchImg = scrollRT.gameObject.AddComponent<Image>();
        scrollTouchImg.color = new Color(0f, 0f, 0f, 0f);
        ScrollRect scrollRect = scrollRT.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.1f;
        scrollRect.scrollSensitivity = 25f;

        RectTransform viewportRT = CreateRect("Viewport", scrollRT, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        viewportRT.gameObject.AddComponent<RectMask2D>();
        scrollRect.viewport = viewportRT;

        RectTransform contentRT = CreateRect("Content", viewportRT,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, SV(602f, 92f));
        HorizontalLayoutGroup hlg = contentRT.gameObject.AddComponent<HorizontalLayoutGroup>();
        int padH = Mathf.RoundToInt(S(16f));
        hlg.padding = new RectOffset(padH, padH, 0, 0);
        hlg.spacing = S(10f);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        ContentSizeFitter csf = contentRT.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        scrollRect.content = contentRT;

        string[] islandTitles = { "Sumatera", "Jawa", "Kalimantan", "Sulawesi", "Papua" };
        string[] islandDefaultSubs = { "Suntiang", "Cunduk Mentul", "Lavung", "Aksesoris adat", "Aksesoris adat" };

        controller.islandCardRects = new RectTransform[5];
        controller.islandCardBgs = new Image[5];
        controller.islandCardSubtitles = new TextMeshProUGUI[5];
        controller.spriteCardActive = sprCardActive;
        controller.spriteCardDefault = sprCardDefault;

        for (int i = 0; i < 5; i++)
        {
            bool isFirst = (i == 0);
            Vector2 cardSize = isFirst ? SV(118f, 86f) : SV(104f, 78f);
            Sprite cardSprite = isFirst ? sprCardActive : sprCardDefault;

            RectTransform cardRT = CreateImage("Card_" + islandTitles[i], contentRT, cardSprite, Image.Type.Simple, true,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, cardSize);

            Button cardBtn = cardRT.gameObject.AddComponent<Button>();
            cardBtn.targetGraphic = cardRT.GetComponent<Image>();
            UnityEventTools.AddIntPersistentListener(cardBtn.onClick, controller.SelectIsland, i);

            RectTransform crownIconRT = CreateImage("Icon_Crown", cardRT, sprIconCrown, Image.Type.Simple, false,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), SV(0f, -10f), SV(40f, 25f));
            crownIconRT.GetComponent<Image>().preserveAspect = true;

            CreateTMP("Txt_IslandName", cardRT, islandTitles[i], S(12f), FontStyles.Bold, Color.white, TextAlignmentOptions.Center,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), SV(0f, -5f), SV(-8f, 18f));

            TextMeshProUGUI txtSub = CreateTMP("Txt_CrownSub", cardRT, islandDefaultSubs[i], S(10f), FontStyles.Normal, colorSecondaryText, TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), SV(0f, 9f), SV(-8f, 15f));

            controller.islandCardRects[i] = cardRT;
            controller.islandCardBgs[i] = cardRT.GetComponent<Image>();
            controller.islandCardSubtitles[i] = txtSub;
        }

        // 3C. Action Bar (y=19 from bottom of dock, height 68)
        RectTransform actionBarRT = CreateRect("Action_Bar", dockRT,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), SV(0f, 19f), SV(0f, 68f));

        RectTransform btnFlipRT = CreateImage("Btn_Flip", actionBarRT, sprBtnFlip, Image.Type.Simple, true,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), SV(36f, 0f), SV(48f, 48f));
        Button btnFlip = btnFlipRT.gameObject.AddComponent<Button>();
        UnityEventTools.AddPersistentListener(btnFlip.onClick, controller.ToggleFlipOrRotation);

        RectTransform btnShutterRT = CreateImage("Btn_Shutter", actionBarRT, sprBtnShutter, Image.Type.Simple, true,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, SV(68f, 68f));
        Button btnShutter = btnShutterRT.gameObject.AddComponent<Button>();
        UnityEventTools.AddPersistentListener(btnShutter.onClick, controller.CaptureScreen);

        RectTransform btnGalleryRT = CreateImage("Btn_Gallery", actionBarRT, sprBtnGallery, Image.Type.Simple, true,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), SV(-36f, 0f), SV(48f, 48f));
        Button btnGallery = btnGalleryRT.gameObject.AddComponent<Button>();
        UnityEventTools.AddPersistentListener(btnGallery.onClick, controller.ToggleInfoModal);

        // =========================================================================
        // 4. MODAL EDUKASI BUDAYA (HALF-SHEET) — Figma node 5:169
        // =========================================================================
        RectTransform modalRootRT = CreateRect("Modal_Info", canvas.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        RectTransform backdropRT = CreateImage("Backdrop_Dim", modalRootRT, null, Image.Type.Simple, true,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        backdropRT.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        Button btnBackdrop = backdropRT.gameObject.AddComponent<Button>();
        btnBackdrop.transition = Selectable.Transition.None;
        UnityEventTools.AddPersistentListener(btnBackdrop.onClick, controller.CloseInfoModal);

        RectTransform drawerRT = CreateImage("Half_Sheet_Drawer", modalRootRT, sprDrawerSurface, Image.Type.Simple, true,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, SV(0f, 440f));

        CreateImage("Grabber", drawerRT, sprGrabber, Image.Type.Simple, false,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), SV(0f, -13f), SV(44f, 5f));

        RectTransform headerModalRT = CreateRect("Header_Modal", drawerRT,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), SV(0f, -32f), SV(-46f, 48f));

        TextMeshProUGUI txtModalTitle = CreateTMP("Txt_CrownName", headerModalRT, "Suntiang Gadang", S(21f), FontStyles.Bold, Color.white, TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), Vector2.zero, SV(-124f, 28f));
        txtModalTitle.enableAutoSizing = true;
        txtModalTitle.fontSizeMin = S(15f);
        txtModalTitle.fontSizeMax = S(21f);

        TextMeshProUGUI txtModalRegion = CreateTMP("Txt_Region", headerModalRT, "Suku Minangkabau · Pulau Sumatera", S(12.5f), FontStyles.Normal, colorSecondaryText, TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), SV(0f, -30f), SV(-124f, 18f));

        RectTransform btnAudioRT = CreateImage("Btn_Audio_Dengarkan", headerModalRT, sprBtnAudio, Image.Type.Simple, true,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, SV(116f, 34f));
        btnAudioRT.gameObject.AddComponent<Button>();
        CreateTMP("Txt_Dengarkan", btnAudioRT, "Dengarkan", S(11.5f), FontStyles.Bold, colorGold, TextAlignmentOptions.Center,
            new Vector2(0.26f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        RectTransform dividerRT = CreateImage("Divider", drawerRT, null, Image.Type.Simple, false,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), SV(0f, -94f), SV(-46f, 1f));
        dividerRT.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);

        RectTransform filosofiRT = CreateRect("Teks_Filosofi", drawerRT,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), SV(0f, -109f), SV(-46f, 220f));

        CreateTMP("Lbl_MaknaSimbolik", filosofiRT, "1 · Makna Simbolik", S(12.5f), FontStyles.Bold, colorGold, TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), Vector2.zero, SV(0f, 18f));
        TextMeshProUGUI txtMakna = CreateTMP("Txt_MaknaSimbolik", filosofiRT,
            "Suntiang 9 tingkat melambangkan beban tanggung jawab ibu rumah tangga.", S(12.5f), FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), SV(0f, -20f), SV(0f, 44f));

        CreateTMP("Lbl_BahanTradisional", filosofiRT, "2 · Bahan Tradisional Asli", S(12.5f), FontStyles.Bold, colorGold, TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), SV(0f, -74f), SV(0f, 18f));
        TextMeshProUGUI txtBahan = CreateTMP("Txt_BahanTradisional", filosofiRT,
            "Emas tembaga, bunga melati hidup, bulu enggang gugur.", S(12.5f), FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), SV(0f, -94f), SV(0f, 34f));

        CreateTMP("Lbl_MomenPenggunaan", filosofiRT, "3 · Momen Penggunaan", S(12.5f), FontStyles.Bold, colorGold, TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), SV(0f, -134f), SV(0f, 18f));
        TextMeshProUGUI txtMomen = CreateTMP("Txt_MomenPenggunaan", filosofiRT,
            "Pernikahan adat ageng, penobatan raja, upacara perang/adat.", S(12.5f), FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), SV(0f, -154f), SV(0f, 44f));

        RectTransform btnCloseModalRT = CreateImage("Btn_CloseModal", drawerRT, sprPillSecondaryBtn, Image.Type.Simple, true,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), SV(0f, 24f), SV(-46f, 52f));
        Button btnCloseModal = btnCloseModalRT.gameObject.AddComponent<Button>();
        UnityEventTools.AddPersistentListener(btnCloseModal.onClick, controller.CloseInfoModal);
        CreateTMP("Txt_Tutup", btnCloseModalRT, "Tutup", S(15f), FontStyles.Bold, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        modalRootRT.gameObject.SetActive(false);

        // =========================================================================
        // 5. PREVIEW HASIL CAPTURE — Figma node 5:219
        // =========================================================================
        RectTransform previewRootRT = CreateRect("Modal_CapturePreview", canvas.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        GameObject rawImgGO = new GameObject("Captured_Photo", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        rawImgGO.transform.SetParent(previewRootRT, false);
        RectTransform rawImgRT = rawImgGO.GetComponent<RectTransform>();
        rawImgRT.anchorMin = Vector2.zero;
        rawImgRT.anchorMax = Vector2.one;
        rawImgRT.sizeDelta = Vector2.zero;
        RawImage rawPreview = rawImgGO.GetComponent<RawImage>();
        rawPreview.color = new Color32(24, 22, 20, 255);

        RectTransform btnClosePreviewRT = CreateImage("Btn_ClosePreview", previewRootRT, sprBtnClose, Image.Type.Simple, true,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), SV(16f, -44f), SV(44f, 44f));
        Button btnClosePreview = btnClosePreviewRT.gameObject.AddComponent<Button>();
        UnityEventTools.AddPersistentListener(btnClosePreview.onClick, controller.CloseCapturePreview);

        RectTransform previewPillRT = CreateImage("Pill_PreviewTitle", previewRootRT, sprPillPreview, Image.Type.Simple, false,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), SV(0f, -52f), SV(210f, 34f));
        TextMeshProUGUI txtPreviewPill = CreateTMP("Txt_PreviewTitle", previewPillRT, "Foto · Suntiang Gadang", S(13f), FontStyles.Bold, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, SV(-20f, 0f));

        RectTransform aksiPreviewRT = CreateImage("Aksi_Preview_Dock", previewRootRT, sprPreviewDockSurface, Image.Type.Simple, true,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, SV(0f, 170f));

        RectTransform retakeGroup = CreateRect("Aksi_Retake", aksiPreviewRT, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), SV(78f, 0f), SV(100f, 76f));
        RectTransform btnRetakeRT = CreateImage("Btn_Retake", retakeGroup, sprBtnRetake, Image.Type.Simple, true,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, SV(52f, 52f));
        Button btnRetake = btnRetakeRT.gameObject.AddComponent<Button>();
        UnityEventTools.AddPersistentListener(btnRetake.onClick, controller.CloseCapturePreview);
        CreateTMP("Lbl_Retake", retakeGroup, "Retake", S(12f), FontStyles.Bold, Color.white, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, SV(0f, 18f));

        RectTransform saveGroup = CreateRect("Aksi_Simpan", aksiPreviewRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, SV(110f, 88f));
        RectTransform btnSaveRT = CreateImage("Btn_Save", saveGroup, sprBtnSave, Image.Type.Simple, true,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, SV(64f, 64f));
        Button btnSave = btnSaveRT.gameObject.AddComponent<Button>();
        UnityEventTools.AddPersistentListener(btnSave.onClick, controller.SaveCapturedPhoto);
        CreateTMP("Lbl_Simpan", saveGroup, "Simpan ke Galeri", S(12f), FontStyles.Bold, Color.white, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, SV(0f, 18f));

        RectTransform shareGroup = CreateRect("Aksi_Share", aksiPreviewRT, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), SV(-78f, 0f), SV(100f, 76f));
        RectTransform btnShareRT = CreateImage("Btn_Share", shareGroup, sprBtnShare, Image.Type.Simple, true,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, SV(52f, 52f));
        Button btnShare = btnShareRT.gameObject.AddComponent<Button>();
        UnityEventTools.AddPersistentListener(btnShare.onClick, controller.SaveCapturedPhoto);
        CreateTMP("Lbl_Share", shareGroup, "Share", S(12f), FontStyles.Bold, Color.white, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, SV(0f, 18f));

        previewRootRT.gameObject.SetActive(false);

        // =========================================================================
        // 6. WIRE REFERENCES ON CROWNARCONTROLLER
        // =========================================================================
        controller.txtHeaderPill = txtHeaderPill;
        controller.txtTrackingStatus = txtTracking;
        controller.faceGuideReticle = reticleRT.gameObject;

        controller.btnFemale = btnFemale;
        controller.btnMale = btnMale;
        controller.imgFemaleBg = segFemaleRT.GetComponent<Image>();
        controller.imgMaleBg = segMaleRT.GetComponent<Image>();
        controller.txtFemaleLabel = txtFemale;
        controller.txtMaleLabel = txtMale;

        controller.infoModalPanel = modalRootRT.gameObject;
        controller.txtCrownName = txtModalTitle;
        controller.txtRegion = txtModalRegion;
        controller.txtMaknaSimbolik = txtMakna;
        controller.txtBahanTradisional = txtBahan;
        controller.txtMomenPenggunaan = txtMomen;

        controller.capturePreviewPanel = previewRootRT.gameObject;
        controller.txtPreviewPill = txtPreviewPill;
        controller.imgCapturedPreview = rawPreview;

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(canvas.gameObject);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
    }

    private struct MatSpec
    {
        public string name;
        public Color baseColor;
        public float metallic;
        public float roughness;
        public string baseMap;
        public string metaSmoothMap;
        public string normalMap;
        public float bumpScale;

        public MatSpec(string n, float r, float g, float b, float met, float rough, string bMap = null, string msMap = null, string nMap = null, float bump = 1f)
        {
            name = n;
            baseColor = new Color(r, g, b, 1f);
            metallic = met;
            roughness = rough;
            baseMap = bMap;
            metaSmoothMap = msMap;
            normalMap = nMap;
            bumpScale = bump;
        }
    }

    private static void SetupCrownMaterialsAndModels()
    {
        string crownsDir = Path.Combine(Application.dataPath, "Crowns");
        string texDir = Path.Combine(crownsDir, "Textures");
        string matDir = Path.Combine(crownsDir, "Materials");
        if (!Directory.Exists(matDir))
            Directory.CreateDirectory(matDir);

        AssetDatabase.Refresh();

        // 1. Configure Texture Importers for all extracted Blender PBR textures
        if (Directory.Exists(texDir))
        {
            string[] pngs = Directory.GetFiles(texDir, "*.png");
            foreach (string png in pngs)
            {
                string fileName = Path.GetFileName(png);
                string assetPath = "Assets/Crowns/Textures/" + fileName;
                TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (ti != null)
                {
                    bool changed = false;
                    if (fileName.EndsWith("_Normal.png"))
                    {
                        if (ti.textureType != TextureImporterType.NormalMap)
                        {
                            ti.textureType = TextureImporterType.NormalMap;
                            changed = true;
                        }
                    }
                    else if (fileName.EndsWith("_MetallicSmoothness.png") || fileName.EndsWith("_Metallic.png") || fileName.EndsWith("_Roughness.png"))
                    {
                        if (ti.textureType != TextureImporterType.Default || ti.sRGBTexture || ti.alphaIsTransparency)
                        {
                            ti.textureType = TextureImporterType.Default;
                            ti.sRGBTexture = false;
                            ti.alphaSource = TextureImporterAlphaSource.FromInput;
                            ti.alphaIsTransparency = false;
                            changed = true;
                        }
                    }
                    else
                    {
                        if (ti.textureType != TextureImporterType.Default || !ti.sRGBTexture)
                        {
                            ti.textureType = TextureImporterType.Default;
                            ti.sRGBTexture = true;
                            changed = true;
                        }
                    }
                    if (changed)
                        ti.SaveAndReimport();
                }
            }
        }

        // 2. Create or update all 45 Blender PBR materials as Unity URP Lit materials
        MatSpec[] specs = new MatSpec[]
        {
            // 01 Sumatera Male - Deta Saluak
            new MatSpec("Minang_Songket_MaroonGold", 1f, 1f, 1f, 0f, 0.5f, "Songket_BaseColor", "Minang_Songket_MaroonGold_MetallicSmoothness", "Songket_Normal", 1.35f),
            new MatSpec("Minang_Songket_InnerLining", 0.42f, 0.10f, 0.12f, 0f, 0.65f, "Songket_BaseColor", null, "Songket_Normal", 1.0f),
            new MatSpec("Minang_Ornate_Gold_Pin", 0.94f, 0.73f, 0.24f, 0.96f, 0.19f),
            new MatSpec("Minang_Pin_BurnishedGold", 0.85f, 0.58f, 0.14f, 0.96f, 0.24f),

            // 02 Jawa Male - Kuluk Kanigaran
            new MatSpec("Kuluk_BlackVelvet_GoldFiligree", 1f, 1f, 1f, 0f, 0.5f, "Kuluk_BaseColor", "Kuluk_BlackVelvet_GoldFiligree_MetallicSmoothness", "Kuluk_Normal", 1.45f),
            new MatSpec("Kuluk_Inner_Velvet_Lining", 0.04f, 0.035f, 0.045f, 0f, 0.88f),
            new MatSpec("Royal_24k_Gold", 0.98f, 0.78f, 0.22f, 0.99f, 0.16f),
            new MatSpec("Royal_24k_Gold_Deep", 0.76f, 0.52f, 0.11f, 0.99f, 0.28f),

            // 03 Kalimantan Male - Dayak Lavung
            new MatSpec("Lavung_Woven_Rattan_RedYellow", 1f, 1f, 1f, 0f, 0.5f, "Lavung_Rattan_BaseColor", "Lavung_Woven_Rattan_RedYellow_MetallicSmoothness", "Lavung_Rattan_Normal", 1.4f),
            new MatSpec("Lavung_Carved_Ivory_Skull", 1f, 1f, 1f, 0f, 0.28f, "Lavung_Ivory_BaseColor", null, "Lavung_Ivory_Normal", 1.2f),
            new MatSpec("Lavung_Hornbill_Casque_Red", 0.68f, 0.14f, 0.06f, 0f, 0.24f),
            new MatSpec("Lavung_Braided_Fiber_Tassel", 0.68f, 0.50f, 0.28f, 0f, 0.82f),
            new MatSpec("Lavung_Laced_Bark_Cord", 0.28f, 0.16f, 0.09f, 0f, 0.78f),
            new MatSpec("Lavung_Hornbill_Feather", 1f, 1f, 1f, 0f, 0.95f, "Lavung_Feather_BaseColor", null, "Lavung_Feather_Normal", 0.35f),

            // 04 Sulawesi Male - Bugis Passapu Patonro
            new MatSpec("Passapu_Maroon_GoldEmbroidery", 1f, 1f, 1f, 0f, 0.5f, "Passapu_BaseColor", "Passapu_Maroon_GoldEmbroidery_MetallicSmoothness", "Passapu_Normal", 1.4f),
            new MatSpec("Passapu_Inner_MaroonLining", 0.22f, 0.025f, 0.045f, 0f, 0.65f),
            new MatSpec("Passapu_Royal_Gold_Badge", 0.98f, 0.78f, 0.22f, 0.98f, 0.16f),
            new MatSpec("Passapu_Antique_Gold_Recess", 0.76f, 0.52f, 0.12f, 0.98f, 0.26f),

            // 05 Papua Male - Papuan Chief Crown
            new MatSpec("Papuan_WovenBark_TerracottaOchre", 1f, 1f, 1f, 0f, 0.5f, "Papuan_Bark_BaseColor", "Papuan_WovenBark_TerracottaOchre_MetallicSmoothness", "Papuan_Bark_Normal", 1.4f),
            new MatSpec("Papuan_White_Cowrie_Shell", 0.95f, 0.91f, 0.82f, 0f, 0.18f),
            new MatSpec("Papuan_Cowrie_Aperture_Dark", 0.18f, 0.11f, 0.07f, 0f, 0.55f),
            new MatSpec("Papuan_Rattan_Lattice_Cane", 0.66f, 0.44f, 0.20f, 0f, 0.46f),
            new MatSpec("Papuan_Twisted_Bark_String", 0.38f, 0.24f, 0.13f, 0f, 0.82f),
            new MatSpec("Papuan_Black_Cassowary_Feather", 0.035f, 0.032f, 0.030f, 0f, 0.42f, null, null, "Papuan_BoP_Normal", 1.4f),
            new MatSpec("Papuan_BirdOfParadise_GoldPlume", 1f, 1f, 1f, 0f, 0.32f, "Papuan_BoP_BaseColor", null, "Papuan_BoP_Normal", 1.1f),
            new MatSpec("Papuan_BoP_Emerald_Breast", 0.02f, 0.48f, 0.22f, 0.65f, 0.22f),

            // 06 Sumatera Female - Suntiang Gadang
            new MatSpec("Suntiang_24k_Gold_Filigree", 1f, 1f, 1f, 0.98f, 0.22f, "Suntiang_Gold_BaseColor", "Suntiang_24k_Gold_Filigree_MetallicSmoothness", "Suntiang_Gold_Normal", 1.45f),
            new MatSpec("Suntiang_24k_Polished_Gold", 0.99f, 0.81f, 0.23f, 0.99f, 0.15f),
            new MatSpec("Suntiang_Antique_Gold_Recess", 0.76f, 0.50f, 0.11f, 0.99f, 0.28f),
            new MatSpec("Suntiang_Ruby_Gem", 0.72f, 0.02f, 0.09f, 0.25f, 0.08f),

            // 07 Jawa Female - Javanese Bridal Headdress (Cunduk Mentul)
            new MatSpec("JavaBridal_24k_AntiqueGold_Filigree", 1f, 1f, 1f, 0.99f, 0.22f, "JavaBridal_Gold_BaseColor", "JavaBridal_24k_AntiqueGold_Filigree_MetallicSmoothness", "JavaBridal_Gold_Normal", 1.4f),
            new MatSpec("JavaBridal_24k_BrightGold", 0.98f, 0.78f, 0.22f, 0.99f, 0.15f),
            new MatSpec("JavaBridal_24k_BurnishedGold", 0.72f, 0.48f, 0.11f, 0.98f, 0.28f),
            new MatSpec("JavaBridal_Chignon_GoldLatticeNet", 1f, 1f, 1f, 0f, 0.5f, "JavaBridal_BunNet_BaseColor", "JavaBridal_Chignon_GoldLatticeNet_MetallicSmoothness", "JavaBridal_BunNet_Normal", 1.4f),
            new MatSpec("JavaBridal_Diamond_Inlay", 0.96f, 0.98f, 1.0f, 0.35f, 0.02f),
            new MatSpec("JavaBridal_Emerald_Inlay", 0.02f, 0.56f, 0.22f, 0.20f, 0.06f),

            // 08 Kalimantan Female - Dayak Lavung Manik
            new MatSpec("LavungManik_GlassSeedBeads_PBR", 1f, 1f, 1f, 0f, 0.5f, "LavungManik_Beads_BaseColor", "LavungManik_GlassSeedBeads_PBR_MetallicSmoothness", "LavungManik_Beads_Normal", 1.45f),
            new MatSpec("LavungManik_Rattan_Tie", 0.64f, 0.42f, 0.18f, 0f, 0.48f),
            new MatSpec("LavungManik_Red_GlassBead", 0.82f, 0.06f, 0.05f, 0f, 0.12f),
            new MatSpec("LavungManik_Yellow_GlassBead", 0.98f, 0.76f, 0.08f, 0f, 0.12f),
            new MatSpec("LavungManik_White_GlassBead", 0.96f, 0.94f, 0.88f, 0f, 0.12f),
            new MatSpec("LavungManik_Black_GlassBead", 0.04f, 0.04f, 0.05f, 0f, 0.12f),
            new MatSpec("LavungManik_Brass_Bell", 0.92f, 0.70f, 0.20f, 0.95f, 0.22f),

            // 09 Sulawesi Female - Bugis Saloko Poci
            new MatSpec("Saloko_24k_YellowGold_Filigree.001", 1f, 1f, 1f, 0.99f, 0.22f, "Saloko_Gold_BaseColor", "Saloko_24k_YellowGold_Filigree_001_MetallicSmoothness", "Saloko_Gold_Normal", 1.45f),
            new MatSpec("Saloko_24k_Polished_YellowGold", 0.99f, 0.82f, 0.20f, 0.99f, 0.14f),
            new MatSpec("Saloko_24k_Deep_Gold", 0.78f, 0.54f, 0.10f, 0.98f, 0.26f),
            new MatSpec("Saloko_White_Pearl_Tip", 0.97f, 0.95f, 0.90f, 0.08f, 0.12f),
            new MatSpec("Saloko_Chignon_GoldenMeshNet", 1f, 1f, 1f, 0f, 0.5f, "Saloko_BunMesh_BaseColor", "Saloko_Chignon_GoldenMeshNet_MetallicSmoothness", "Saloko_BunMesh_Normal", 1.4f),

            // 10 Papua Female - Papuan Female Crown
            new MatSpec("PapuanFemale_Woven_Bark_PBR", 1f, 1f, 1f, 0f, 0.5f, "PapuanFemale_Bark_BaseColor", "PapuanFemale_Woven_Bark_PBR_MetallicSmoothness", "PapuanFemale_Bark_Normal", 1.55f),
            new MatSpec("PapuanFemale_White_Cowrie_Shell_PBR", 1f, 1f, 1f, 0f, 0.5f, "PapuanFemale_Cowrie_BaseColor", "PapuanFemale_White_Cowrie_Shell_PBR_MetallicSmoothness", "PapuanFemale_Cowrie_Normal", 1.25f),
            new MatSpec("PapuanFemale_Black_Cassowary_Feather_PBR", 1f, 1f, 1f, 0f, 0.5f, "PapuanFemale_Feather_BaseColor", "PapuanFemale_Black_Cassowary_Feather_PBR_MetallicSmoothness", "PapuanFemale_Feather_Normal", 1.3f),
            new MatSpec("PapuanFemale_Braided_Bark_Cord_PBR", 0.44f, 0.28f, 0.15f, 0f, 0.78f),
            new MatSpec("PapuanFemale_Inner_Bark_Lining_PBR", 0.26f, 0.16f, 0.08f, 0f, 0.85f)
        };

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Dictionary<string, Material> matMap = new Dictionary<string, Material>();

        foreach (MatSpec s in specs)
        {
            string safeFileName = s.name.Replace(".", "_") + ".mat";
            string matAssetPath = "Assets/Crowns/Materials/" + safeFileName;
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matAssetPath);
            if (mat == null)
            {
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, matAssetPath);
            }
            else
            {
                mat.shader = urpLit;
            }

            mat.SetColor("_BaseColor", s.baseColor);
            mat.SetColor("_Color", s.baseColor);

            if (!string.IsNullOrEmpty(s.baseMap))
            {
                Texture2D bTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Crowns/Textures/" + s.baseMap + ".png");
                mat.SetTexture("_BaseMap", bTex);
                mat.SetTexture("_MainTex", bTex);
            }
            else
            {
                mat.SetTexture("_BaseMap", null);
                mat.SetTexture("_MainTex", null);
            }

            float smoothness = Mathf.Clamp01(1f - s.roughness);
            if (!string.IsNullOrEmpty(s.metaSmoothMap))
            {
                Texture2D msTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Crowns/Textures/" + s.metaSmoothMap + ".png");
                mat.SetTexture("_MetallicGlossMap", msTex);
                mat.SetFloat("_Metallic", s.metallic > 0.5f ? s.metallic : 1f);
                mat.SetFloat("_Smoothness", 1.0f);
                mat.SetFloat("_Glossiness", 1.0f);
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else
            {
                mat.SetTexture("_MetallicGlossMap", null);
                mat.SetFloat("_Metallic", s.metallic);
                mat.SetFloat("_Smoothness", smoothness);
                mat.SetFloat("_Glossiness", smoothness);
                mat.DisableKeyword("_METALLICSPECGLOSSMAP");
            }

            if (!string.IsNullOrEmpty(s.normalMap))
            {
                Texture2D nTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Crowns/Textures/" + s.normalMap + ".png");
                mat.SetTexture("_BumpMap", nTex);
                mat.SetFloat("_BumpScale", s.bumpScale);
                mat.EnableKeyword("_NORMALMAP");
            }
            else
            {
                mat.SetTexture("_BumpMap", null);
                mat.SetFloat("_BumpScale", 1.0f);
                mat.DisableKeyword("_NORMALMAP");
            }

            if (mat.HasProperty("_Cull"))
                mat.SetFloat("_Cull", 2f);

            if (s.name.Contains("Feather"))
            {
                mat.SetFloat("_SpecularHighlights", 0f);
                mat.SetFloat("_EnvironmentReflections", 0f);
            }

            EditorUtility.SetDirty(mat);
            matMap[s.name] = mat;
        }

        AssetDatabase.SaveAssets();

        // 3. Remap all 10 FBX models in Assets/Crowns/ to use the URP materials & bake axis conversion
        string[] fbxFiles = Directory.GetFiles(crownsDir, "*.fbx");
        foreach (string fbxFile in fbxFiles)
        {
            string fbxAssetPath = "Assets/Crowns/" + Path.GetFileName(fbxFile);
            ModelImporter mi = AssetImporter.GetAtPath(fbxAssetPath) as ModelImporter;
            if (mi != null)
            {
                mi.bakeAxisConversion = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                mi.materialLocation = ModelImporterMaterialLocation.InPrefab;

                foreach (var kvp in matMap)
                {
                    AssetImporter.SourceAssetIdentifier id = new AssetImporter.SourceAssetIdentifier(typeof(Material), kvp.Key);
                    mi.AddRemap(id, kvp.Value);
                }

                mi.SaveAndReimport();
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void PopulateCrownDatabase(CrownARController controller)
    {
        GameObject fbxSumatraMale = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Crowns/01_Sumatera_Deta_Saluak.fbx");
        GameObject fbxJavaMale = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Crowns/02_Jawa_Kuluk_Kanigaran.fbx");
        GameObject fbxKalimantanMale = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Crowns/03_Kalimantan_Dayak_Lavung.fbx");
        GameObject fbxSulawesiMale = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Crowns/04_Sulawesi_Bugis_Passapu.fbx");
        GameObject fbxPapuaMale = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Crowns/05_Papua_Chief_Crown.fbx");

        GameObject fbxSumatraFemale = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Crowns/06_Sumatera_Suntiang_Gadang.fbx") ?? fbxSumatraMale;
        GameObject fbxJavaFemale = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Crowns/07_Jawa_Bridal_Headdress.fbx") ?? fbxJavaMale;
        GameObject fbxKalimantanFemale = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Crowns/08_Kalimantan_Lavung_Manik.fbx") ?? fbxKalimantanMale;
        GameObject fbxSulawesiFemale = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Crowns/09_Sulawesi_Saloko_Poci.fbx") ?? fbxSulawesiMale;
        GameObject fbxPapuaFemale = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Crowns/10_Papua_Female_Crown.fbx") ?? fbxPapuaMale;

        controller.crownList = new List<CrownARController.CrownData>
        {
            new CrownARController.CrownData
            {
                island = CrownARController.Island.Sumatra,
                gender = CrownARController.Gender.Female,
                crownName = "Suntiang Gadang",
                shortSubtitle = "Suntiang",
                headerPillText = "Suntiang Gadang — Minangkabau",
                originRegion = "Suku Minangkabau · Pulau Sumatera",
                description = "Mahkota pengantin wanita Minangkabau bertingkat emas.",
                maknaSimbolik = "Suntiang 9 tingkat melambangkan beban tanggung jawab ibu rumah tangga.",
                bahanTradisional = "Emas tembaga, bunga melati hidup, bulu enggang gugur.",
                momenPenggunaan = "Pernikahan adat ageng, penobatan raja, upacara perang/adat.",
                crownPrefab = fbxSumatraFemale
            },
            new CrownARController.CrownData
            {
                island = CrownARController.Island.Sumatra,
                gender = CrownARController.Gender.Male,
                crownName = "Deta Saluak Batimba",
                shortSubtitle = "Deta Saluak",
                headerPillText = "Deta Saluak — Minangkabau",
                originRegion = "Suku Minangkabau · Pulau Sumatera",
                description = "Penutup kepala tradisional pria Minangkabau melambangkan hukum adat dan kepemimpinan.",
                maknaSimbolik = "Lipatan berjenjang melambangkan akal budi, musyawarah, dan keteguhan penghulu adat.",
                bahanTradisional = "Kain songket tenun benang emas, katun hitam berkanji alami.",
                momenPenggunaan = "Pengangkatan datuak/penghulu, baralek gadang, sidang kerapatan adat.",
                crownPrefab = fbxSumatraMale
            },
            new CrownARController.CrownData
            {
                island = CrownARController.Island.Java,
                gender = CrownARController.Gender.Female,
                crownName = "Cunduk Mentul Paes Ageng",
                shortSubtitle = "Cunduk Mentul",
                headerPillText = "Cunduk Mentul — Jawa",
                originRegion = "D.I. Yogyakarta & Jawa Tengah · Pulau Jawa",
                description = "Tusuk konde emas bergoyang di atas sanggul pengantin putri keraton Jawa.",
                maknaSimbolik = "Cunduk mentul berjumlah ganjil (5, 7, atau 9) melambangkan sinar matahari dan kesucian hidup.",
                bahanTradisional = "Kuningan sepuh emas murni, untaian ronce melati segar, permata yakut.",
                momenPenggunaan = "Upacara panggih pernikahan adat Paes Ageng dan Solo Putri.",
                crownPrefab = fbxJavaFemale
            },
            new CrownARController.CrownData
            {
                island = CrownARController.Island.Java,
                gender = CrownARController.Gender.Male,
                crownName = "Kuluk Kanigaran Ageng",
                shortSubtitle = "Kuluk Kanigaran",
                headerPillText = "Kuluk Kanigaran — Jawa",
                originRegion = "D.I. Yogyakarta & Jawa Tengah · Pulau Jawa",
                description = "Mahkota kebesaran sultan dan bangsawan keraton Jawa melambangkan wibawa dan spiritual.",
                maknaSimbolik = "Warna hitam beludru dan garis emas melambangkan ketenangan batin serta keluhuran budi raja.",
                bahanTradisional = "Beludru hitam pekat, bordir benang emas murni (gim), mika penguat.",
                momenPenggunaan = "Pernikahan agung keraton, garebeg kraton, dan penobatan bangsawan.",
                crownPrefab = fbxJavaMale
            },
            new CrownARController.CrownData
            {
                island = CrownARController.Island.Kalimantan,
                gender = CrownARController.Gender.Female,
                crownName = "Tapung Manik Enggang",
                shortSubtitle = "Lavung",
                headerPillText = "Tapung Manik — Dayak",
                originRegion = "Suku Dayak Kenyah · Pulau Kalimantan",
                description = "Ikat kepala manik-manik berhias bulu burung enggang khas putri Dayak.",
                maknaSimbolik = "Motif aso dan bulu enggang melambangkan keanggunan, kedamaian, dan hubungan dengan alam atas.",
                bahanTradisional = "Manik-manik kaca warna-warni, bulu ekor burung enggang, serat rotan.",
                momenPenggunaan = "Tari Kancet Papatai/Lasan, pesta panen Naik Dango, penyambutan tamu agung.",
                crownPrefab = fbxKalimantanFemale
            },
            new CrownARController.CrownData
            {
                island = CrownARController.Island.Kalimantan,
                gender = CrownARController.Gender.Male,
                crownName = "Lavung Baling Dayak",
                shortSubtitle = "Lavung",
                headerPillText = "Lavung Baling — Dayak",
                originRegion = "Suku Dayak Kenyah · Pulau Kalimantan",
                description = "Hiasan kepala panglima Dayak dengan taring pusaka dan bulu burung enggang.",
                maknaSimbolik = "Taring macan dahan dan bulu enggang melambangkan keberanian ksatria dan pelindung suku.",
                bahanTradisional = "Anyaman rotan hutan, kulit kayu nyamu, manik-manik tua, bulu enggang.",
                momenPenggunaan = "Upacara adat Mamat, tarian perang Kancet Papatai, pengukuhan kepala adat.",
                crownPrefab = fbxKalimantanMale
            },
            new CrownARController.CrownData
            {
                island = CrownARController.Island.Sulawesi,
                gender = CrownARController.Gender.Female,
                crownName = "Saloko Pinang Goyang",
                shortSubtitle = "Aksesoris adat",
                headerPillText = "Saloko Pinang — Bugis",
                originRegion = "Suku Bugis-Makassar · Pulau Sulawesi",
                description = "Mahkota emas kembang goyang khas mempelai wanita bangsawan Bugis-Makassar.",
                maknaSimbolik = "Kelopak bunga emas yang bergetar melambangkan kemakmuran, kehormatan (siri'), dan keanggunan.",
                bahanTradisional = "Lempeng emas tempa, kawat pegas perak, batu permata mirah delima.",
                momenPenggunaan = "Upacara pernikahan adat Mappacci dan Tudang Penni bangsawan Bugis.",
                crownPrefab = fbxSulawesiFemale
            },
            new CrownARController.CrownData
            {
                island = CrownARController.Island.Sulawesi,
                gender = CrownARController.Gender.Male,
                crownName = "Passapu Patonro",
                shortSubtitle = "Passapu",
                headerPillText = "Passapu Patonro — Bugis",
                originRegion = "Suku Bugis-Makassar · Pulau Sulawesi",
                description = "Ikat kepala kain kaku tegak menjulang melambangkan keteguhan dan keberanian ksatria Bugis.",
                maknaSimbolik = "Ujung passapu yang tegak ke atas melambangkan kejujuran, harga diri (siri' na pacce), dan keberanian.",
                bahanTradisional = "Tenun sutra lontara merah/emas dengan penguat serat daun lontar.",
                momenPenggunaan = "Upacara adat kerajaan Gowa-Bone, pernikahan adat, dan tari Pakarena/Angngaru.",
                crownPrefab = fbxSulawesiMale
            },
            new CrownARController.CrownData
            {
                island = CrownARController.Island.Papua,
                gender = CrownARController.Gender.Female,
                crownName = "Mahkota Cenderawasih",
                shortSubtitle = "Aksesoris adat",
                headerPillText = "Mahkota Kasuari — Papua",
                originRegion = "Suku Asmat & Dani · Pulau Papua",
                description = "Hiasan kepala anyaman serat alam berpadu bulu kasuari dan kerang laut.",
                maknaSimbolik = "Melambangkan harmoni manusia dengan hutan dan kehormatan perempuan dalam klan adat.",
                bahanTradisional = "Serat kulit kayu genemo, bulu kasuari gugur, cangkang kerang kauri putih.",
                momenPenggunaan = "Pesta budaya Bakar Batu, tarian Yospan, dan penyambutan tamu kehormatan.",
                crownPrefab = fbxPapuaFemale
            },
            new CrownARController.CrownData
            {
                island = CrownARController.Island.Papua,
                gender = CrownARController.Gender.Male,
                crownName = "Mahkota Bulu Kasuari",
                shortSubtitle = "Mahkota Kasuari",
                headerPillText = "Mahkota Kasuari — Papua",
                originRegion = "Suku Asmat & Dani · Pulau Papua",
                description = "Mahkota adat kepala suku dari jalinan serat kulit kayu genemo dan bulu burung kasuari lebat.",
                maknaSimbolik = "Melambangkan wibawa kepala suku (Ondoafi), kebijaksanaan, dan kekuatan menjaga tanah leluhur.",
                bahanTradisional = "Serat kulit kayu genemo (melinjo hutan), bulu burung kasuari, biji saga merah.",
                momenPenggunaan = "Penobatan Ondoafi/kepala suku, festival Lembah Baliem, dan upacara perdamaian adat.",
                crownPrefab = fbxPapuaMale
            }
        };
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        return rt;
    }

    private static RectTransform CreateImage(string name, Transform parent, Sprite sprite, Image.Type imgType, bool raycastTarget,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        RectTransform rt = CreateRect(name, parent, anchorMin, anchorMax, pivot, anchoredPos, sizeDelta);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = imgType;
        img.raycastTarget = raycastTarget;
        return rt;
    }

    private static TextMeshProUGUI CreateTMP(string name, Transform parent, string text, float fontSize, FontStyles style, Color color, TextAlignmentOptions align,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        RectTransform rt = CreateRect(name, parent, anchorMin, anchorMax, pivot, anchoredPos, sizeDelta);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    private static void GenerateRoundedRect(string path, int w, int h, float radius, Color fill, Color border, float borderThickness, bool flatBottom)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dist = SignedDistanceRoundedRect(x + 0.5f, y + 0.5f, w, h, radius, flatBottom);
                if (dist > 0.5f)
                {
                    pixels[y * w + x] = Color.clear;
                }
                else
                {
                    float outerAlpha = Mathf.Clamp01(0.5f - dist);
                    Color c = fill;
                    if (borderThickness > 0f)
                    {
                        float innerDist = dist + borderThickness;
                        float borderMix = Mathf.Clamp01(innerDist + 0.5f);
                        if (flatBottom && y < borderThickness + 2)
                            borderMix = 0f;
                        c = Color.Lerp(fill, new Color(border.r, border.g, border.b, 1f), borderMix * border.a);
                        c.a = Mathf.Lerp(fill.a, Mathf.Max(fill.a, border.a), borderMix);
                    }
                    c.a *= outerAlpha;
                    pixels[y * w + x] = c;
                }
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    private static float SignedDistanceRoundedRect(float x, float y, float w, float h, float r, bool flatBottom)
    {
        float halfW = w * 0.5f;
        float halfH = h * 0.5f;
        float px = Mathf.Abs(x - halfW);
        float py = y - halfH;
        if (flatBottom && py < 0f)
        {
            float dx = px - halfW;
            float dy = -halfH - y;
            return Mathf.Max(dx, dy);
        }
        py = Mathf.Abs(py);
        float qx = px - (halfW - r);
        float qy = py - (halfH - r);
        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
        return outside + inside - r;
    }
}
