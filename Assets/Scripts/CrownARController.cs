using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.InferenceEngine;
using TMPro;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>
/// NusantaraCrown AR - Master Controller
/// Supports:
/// 1. Universal Live Camera (WebCamTexture on Laptop & Android/Phone) rendered on a 3D Background Quad at Z=12m
///    so 3D Crowns at Z=2m ALWAYS render in front of the camera feed.
/// 2. Real-Time AI Face & 5-Point Landmark Detection (OpenCV YuNet 640x640 ONNX via Unity.InferenceEngine)
///    that locks the 3D crown onto the user's forehead/top of head like a hat ("layaknya topi").
/// 3. Native ARCore / ARKit Face Tracking (ARFaceManager) when supported on mobile devices.
/// </summary>
public class CrownARController : MonoBehaviour
{
    public enum Gender { Female = 0, Male = 1 }
    public enum Island { Sumatra = 0, Java = 1, Kalimantan = 2, Sulawesi = 3, Papua = 4 }

    [System.Serializable]
    public class CrownData
    {
        public Island island;
        public Gender gender;
        public string crownName;
        public string shortSubtitle;
        public string headerPillText;
        public string originRegion;
        [TextArea(2, 4)] public string description;
        [TextArea(2, 3)] public string maknaSimbolik;
        [TextArea(2, 3)] public string bahanTradisional;
        [TextArea(2, 3)] public string momenPenggunaan;
        public GameObject crownPrefab;
    }

    [Header("AR Foundation & AI Model Reference")]
    [SerializeField] private ARFaceManager faceManager;
    [SerializeField] public ModelAsset faceDetectorModel;
    [SerializeField] private Vector3 arFaceHatOffset = new Vector3(0f, 0.065f, -0.02f);
    [SerializeField] private Vector3 arFaceCrownScale = new Vector3(0.15f, 0.15f, 0.15f);

    [Header("Webcam Head-Top Hat Calibration")]
    [Tooltip("Vertical offset multiplier above the eyes so the crown sits on the forehead/top of the head like a hat")]
    [SerializeField] private float foreheadHeightFactor = 0.34f;
    [Tooltip("Scale multiplier for the crown relative to detected head width")]
    [SerializeField] private float hatScaleMultiplier = 1.0f;
    [Tooltip("Local offset of the crown mesh relative to the head anchor")]
    [SerializeField] private Vector3 localHatOffset = new Vector3(0f, 0.0f, 0f);

    [Header("3D Live Camera Background")]
    [SerializeField] public MeshRenderer webcamBackgroundRenderer;

    [Header("Crown Database")]
    [SerializeField] public List<CrownData> crownList = new List<CrownData>();

    [Header("Top HUD & Tracking UI")]
    [SerializeField] public TextMeshProUGUI txtHeaderPill;
    [SerializeField] public TextMeshProUGUI txtTrackingStatus;
    [SerializeField] public GameObject faceGuideReticle;

    [Header("Gender Segmented Control")]
    [SerializeField] public Button btnFemale;
    [SerializeField] public Button btnMale;
    [SerializeField] public Image imgFemaleBg;
    [SerializeField] public Image imgMaleBg;
    [SerializeField] public TextMeshProUGUI txtFemaleLabel;
    [SerializeField] public TextMeshProUGUI txtMaleLabel;

    [Header("Island Carousel Cards (5 Islands)")]
    [SerializeField] public RectTransform[] islandCardRects = new RectTransform[5];
    [SerializeField] public Image[] islandCardBgs = new Image[5];
    [SerializeField] public TextMeshProUGUI[] islandCardSubtitles = new TextMeshProUGUI[5];
    [SerializeField] public Sprite spriteCardActive;
    [SerializeField] public Sprite spriteCardDefault;

    [Header("Modal Edukasi Budaya (Half-Sheet)")]
    [SerializeField] public GameObject infoModalPanel;
    [SerializeField] public TextMeshProUGUI txtCrownName;
    [SerializeField] public TextMeshProUGUI txtRegion;
    [SerializeField] public TextMeshProUGUI txtDescription;
    [SerializeField] public TextMeshProUGUI txtMaknaSimbolik;
    [SerializeField] public TextMeshProUGUI txtBahanTradisional;
    [SerializeField] public TextMeshProUGUI txtMomenPenggunaan;

    [Header("Capture Preview Overlay")]
    [SerializeField] public GameObject capturePreviewPanel;
    [SerializeField] public TextMeshProUGUI txtPreviewPill;
    [SerializeField] public RawImage imgCapturedPreview;

    [Header("Initial State")]
    [SerializeField] private Island currentIsland = Island.Sumatra;
    [SerializeField] private Gender currentGender = Gender.Female;

    // Runtime 3D & UI state
    private GameObject activeCrownInstance;
    private Transform activeFaceTransform;
    private GameObject headAnchorGO;
    private Texture2D lastCapturedTexture;

    // Webcam & 3D Background Quad state
    private WebCamTexture webCamTexture;
    private WebCamDevice[] webCamDevices;
    private int currentCamIndex = 0;
    private bool isUsingWebCamMode = false;
    private bool isFrontFacingOrMirrored = true;
    private Material runtimeWebcamMat;

    // Sentis / Unity.InferenceEngine YuNet (640x640) runtime
    private Model runtimeFaceModel;
    private Worker faceWorker;
    private Tensor<float> inputTensor;
    private RenderTexture detectorCropRT;
    private TextureTransform tensorTransform;
    private const int YUNET_SIZE = 640;
    private static readonly int[] STRIDES = { 8, 16, 32 };
    private static readonly string[] CLS_NAMES = { "cls_8", "cls_16", "cls_32" };
    private static readonly string[] OBJ_NAMES = { "obj_8", "obj_16", "obj_32" };
    private static readonly string[] BBOX_NAMES = { "bbox_8", "bbox_16", "bbox_32" };
    private static readonly string[] KPS_NAMES = { "kps_8", "kps_16", "kps_32" };

    // Smoothed head pose in Webcam Mode (default sits right at the top of the face guide circle)
    private Vector3 targetHeadLocalPos = new Vector3(0f, 0.34f, 2.0f);
    private Quaternion targetHeadLocalRot = Quaternion.Euler(0f, 180f, 0f);
    private Vector3 targetCrownScale = new Vector3(0.30f, 0.30f, 0.30f);
    private float currentCrownNormalizationFactor = 1.0f;
    private float lastFaceDetectedTime = -10f;
    private int frameCounter = 0;

    private static readonly Color ColorDarkText = new Color32(18, 18, 18, 255);
    private static readonly Color ColorWhiteText = Color.white;
    private const float UI_SCALE = 1080f / 414f;
    private const float HEAD_DEPTH_Z = 2.0f;
    private const float BG_QUAD_DEPTH_Z = 12.0f;

    private void Awake()
    {
        if (faceManager == null)
            faceManager = FindAnyObjectByType<ARFaceManager>();

        // Clean up any legacy Canvas_CameraBG that could occlude 3D meshes in URP
        GameObject oldBgCanvas = GameObject.Find("Canvas_CameraBG");
        if (oldBgCanvas != null)
            Destroy(oldBgCanvas);

#if UNITY_EDITOR || UNITY_STANDALONE
        // Prevent XR Simulation / ARSession from hijacking Main Camera on Laptop/PC
        ARSession arSession = FindAnyObjectByType<ARSession>();
        if (arSession != null)
            arSession.enabled = false;
        if (faceManager != null)
            faceManager.enabled = false;

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            ARCameraBackground camBg = mainCam.GetComponent<ARCameraBackground>();
            if (camBg != null) camBg.enabled = false;
            ARCameraManager camMgr = mainCam.GetComponent<ARCameraManager>();
            if (camMgr != null) camMgr.enabled = false;
            var behaviors = mainCam.GetComponents<MonoBehaviour>();
            foreach (var b in behaviors)
            {
                if (b != null && b.GetType().Name.Contains("TrackedPoseDriver"))
                    b.enabled = false;
            }
        }
#endif
    }

    private IEnumerator Start()
    {
        UpdateGenderUIState();
        UpdateIslandCardsUI();

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.nearClipPlane = 0.05f;
            mainCam.farClipPlane = 100f;
#if UNITY_EDITOR || UNITY_STANDALONE
            if (mainCam.transform.parent != null)
            {
                mainCam.transform.parent.localPosition = Vector3.zero;
                mainCam.transform.parent.localRotation = Quaternion.identity;
            }
            mainCam.transform.localPosition = Vector3.zero;
            mainCam.transform.localRotation = Quaternion.identity;
#endif
            headAnchorGO = new GameObject("Head_Hat_Anchor");
            headAnchorGO.transform.SetParent(mainCam.transform, false);
            headAnchorGO.transform.localPosition = targetHeadLocalPos;
            headAnchorGO.transform.localRotation = targetHeadLocalRot;
            activeFaceTransform = headAnchorGO.transform;
        }

        UpdateCrownVisual();

        // Request Camera Permissions on Android & Desktop
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Permission.RequestUserPermission(Permission.Camera);
            float waitTimer = 0f;
            while (!Permission.HasUserAuthorizedPermission(Permission.Camera) && waitTimer < 5f)
            {
                waitTimer += 0.2f;
                yield return new WaitForSeconds(0.2f);
            }
        }
#endif
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

        bool arFaceSubsystemActive = false;
#if !UNITY_EDITOR && !UNITY_STANDALONE
        if (faceManager != null && faceManager.enabled)
        {
            yield return new WaitForSeconds(0.5f);
            if (faceManager.subsystem != null && faceManager.subsystem.running)
            {
                arFaceSubsystemActive = true;
            }
        }
#endif

        if (arFaceSubsystemActive)
        {
            isUsingWebCamMode = false;
            if (webcamBackgroundRenderer != null)
                webcamBackgroundRenderer.gameObject.SetActive(false);
            Debug.Log("[NusantaraCrown] Menggunakan Native ARFaceManager (ARCore/ARKit).");
        }
        else
        {
            ActivateWebCamAndAITracker();
        }
    }

    private void ActivateWebCamAndAITracker()
    {
        isUsingWebCamMode = true;

        if (faceManager != null)
            faceManager.enabled = false;

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            ARCameraBackground camBg = mainCam.GetComponent<ARCameraBackground>();
            if (camBg != null) camBg.enabled = false;
            ARCameraManager camMgr = mainCam.GetComponent<ARCameraManager>();
            if (camMgr != null) camMgr.enabled = false;
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color32(20, 18, 16, 255);
        }

        InitializeYuNetTracker();
        StartWebCam(preferFrontFacing: true);
    }

    private void InitializeYuNetTracker()
    {
        if (faceDetectorModel == null)
        {
            Debug.LogWarning("[NusantaraCrown] ModelAsset YuNet belum di-assign di CrownARController.");
            return;
        }

        try
        {
            Model sourceModel = ModelLoader.Load(faceDetectorModel);

            // YuNet expects [0..255] BGR input of shape (1, 3, 640, 640) with TopLeft origin
            FunctionalGraph graph = new FunctionalGraph();
            FunctionalTensor[] inputs = graph.AddInputs(sourceModel);
            FunctionalTensor scaledInput = inputs[0] * 255f;
            FunctionalTensor[] outputs = Functional.Forward(sourceModel, new FunctionalTensor[] { scaledInput });
            for (int i = 0; i < outputs.Length; i++)
            {
                graph.AddOutput(outputs[i], sourceModel.outputs[i].name);
            }

            runtimeFaceModel = graph.Compile();
            faceWorker = new Worker(runtimeFaceModel, BackendType.CPU);
            inputTensor = new Tensor<float>(new TensorShape(1, 3, YUNET_SIZE, YUNET_SIZE));
            detectorCropRT = new RenderTexture(YUNET_SIZE, YUNET_SIZE, 0, RenderTextureFormat.ARGB32);
            detectorCropRT.Create();

            tensorTransform = new TextureTransform()
                .SetTensorLayout(TensorLayout.NCHW)
                .SetChannelSwizzle(ChannelSwizzle.BGRA)
                .SetCoordOrigin(CoordOrigin.TopLeft);

            Debug.Log("<color=#4cd964>[NusantaraCrown] AI Head Tracker (YuNet 640x640 + Sentis) aktif!</color>");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NusantaraCrown] Gagal menginisialisasi YuNet AI Tracker: {e.Message}");
        }
    }

    private void StartWebCam(bool preferFrontFacing)
    {
        EnsureWebcam3DQuadExists();

        webCamDevices = WebCamTexture.devices;
        if (webCamDevices == null || webCamDevices.Length == 0)
        {
            Debug.LogWarning("[NusantaraCrown] Tidak ada kamera/webcam yang terdeteksi di perangkat ini.");
            if (txtTrackingStatus != null)
                txtTrackingStatus.text = "Mode Pratinjau 3D (Tanpa Kamera)";
            if (faceGuideReticle != null)
                faceGuideReticle.SetActive(false);
            return;
        }

        int selectedIdx = 0;
        for (int i = 0; i < webCamDevices.Length; i++)
        {
            if (webCamDevices[i].isFrontFacing == preferFrontFacing)
            {
                selectedIdx = i;
                break;
            }
        }
        StartWebCamByIndex(selectedIdx);
    }

    private void StartWebCamByIndex(int index)
    {
        if (webCamDevices == null || webCamDevices.Length == 0) return;

        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            Destroy(webCamTexture);
        }

        currentCamIndex = ((index % webCamDevices.Length) + webCamDevices.Length) % webCamDevices.Length;
        WebCamDevice device = webCamDevices[currentCamIndex];

#if UNITY_EDITOR || UNITY_STANDALONE
        isFrontFacingOrMirrored = true;
#else
        isFrontFacingOrMirrored = device.isFrontFacing || webCamDevices.Length == 1;
#endif

        webCamTexture = new WebCamTexture(device.name, 1280, 720, 30);
        webCamTexture.Play();

        if (webcamBackgroundRenderer != null)
        {
            webcamBackgroundRenderer.gameObject.SetActive(true);
            if (runtimeWebcamMat == null)
            {
                Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
                runtimeWebcamMat = new Material(unlitShader);
                webcamBackgroundRenderer.sharedMaterial = runtimeWebcamMat;
            }
            runtimeWebcamMat.mainTexture = webCamTexture;
            if (runtimeWebcamMat.HasProperty("_BaseMap"))
                runtimeWebcamMat.SetTexture("_BaseMap", webCamTexture);
            if (runtimeWebcamMat.HasProperty("_BaseColor"))
                runtimeWebcamMat.SetColor("_BaseColor", Color.white);
        }

        Debug.Log($"<color=#4cd964>[NusantaraCrown] Kamera aktif: {device.name} (Mirrored: {isFrontFacingOrMirrored})</color>");
    }

    private void EnsureWebcam3DQuadExists()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        if (webcamBackgroundRenderer == null)
        {
            Transform existing = mainCam.transform.Find("Webcam_3D_Background");
            if (existing != null)
            {
                webcamBackgroundRenderer = existing.GetComponent<MeshRenderer>();
            }
            else
            {
                GameObject quadGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quadGO.name = "Webcam_3D_Background";
                Collider col = quadGO.GetComponent<Collider>();
                if (col != null) Destroy(col);

                quadGO.transform.SetParent(mainCam.transform, false);
                quadGO.transform.localPosition = new Vector3(0f, 0f, BG_QUAD_DEPTH_Z);
                quadGO.transform.localRotation = Quaternion.identity;

                webcamBackgroundRenderer = quadGO.GetComponent<MeshRenderer>();
                webcamBackgroundRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                webcamBackgroundRenderer.receiveShadows = false;
            }
        }

        if (webcamBackgroundRenderer != null)
        {
            webcamBackgroundRenderer.transform.SetParent(mainCam.transform, false);
            webcamBackgroundRenderer.transform.localPosition = new Vector3(0f, 0f, BG_QUAD_DEPTH_Z);
        }
    }

    private void Update()
    {
        if (isUsingWebCamMode)
        {
            UpdateWebCamFeedAndHeadTracking();
        }
        else
        {
            UpdateNativeARFaceTracking();
        }
    }

    private void UpdateWebCamFeedAndHeadTracking()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        // Always keep the 3D Crown active and smoothly interpolated in front of the camera (Z = 2.0m)
        bool isTrackingNow = (Time.time - lastFaceDetectedTime) < 1.5f;

        if (!isTrackingNow)
        {
            // When no face is locked yet, keep the crown resting above the face guide reticle so it is always visible
            targetHeadLocalPos = new Vector3(0f, 0.34f, HEAD_DEPTH_Z);
            targetHeadLocalRot = Quaternion.Euler(0f, 180f, 0f);
            targetCrownScale = new Vector3(0.30f, 0.30f, 0.30f);
        }

        if (headAnchorGO != null)
        {
            // Self-heal: webcam mode always uses the head anchor and must always have a crown instance
            if (activeFaceTransform != headAnchorGO.transform)
                activeFaceTransform = headAnchorGO.transform;
            if (activeCrownInstance == null)
                AttachCrownToFace(activeFaceTransform);

            float smoothSpeed = 14f * Time.deltaTime;
            headAnchorGO.transform.localPosition = Vector3.Lerp(headAnchorGO.transform.localPosition, targetHeadLocalPos, smoothSpeed);
            headAnchorGO.transform.localRotation = Quaternion.Slerp(headAnchorGO.transform.localRotation, targetHeadLocalRot, smoothSpeed);

            if (activeCrownInstance != null)
            {
                Vector3 normalizedTargetScale = targetCrownScale * currentCrownNormalizationFactor;
                activeCrownInstance.transform.localScale = Vector3.Lerp(activeCrownInstance.transform.localScale, normalizedTargetScale, smoothSpeed);
                if (!activeCrownInstance.activeSelf)
                    activeCrownInstance.SetActive(true);
            }
        }

        if (txtTrackingStatus != null)
        {
            txtTrackingStatus.text = isTrackingNow ? "Wajah Terkunci" : "Arahkan wajah ke kamera";
        }

        if (faceGuideReticle != null)
        {
            faceGuideReticle.SetActive(!isTrackingNow);
        }

        if (webCamTexture == null || !webCamTexture.isPlaying || webCamTexture.width <= 16)
        {
            return;
        }

        // 1. Size the 3D Background Quad at Z=12m to fill the camera frustum with Envelope aspect fit
        float halfFovRad = mainCam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float frustumH_Bg = 2f * BG_QUAD_DEPTH_Z * Mathf.Tan(halfFovRad);
        float screenAspect = (float)Screen.width / Mathf.Max(1f, (float)Screen.height);
        float frustumW_Bg = frustumH_Bg * screenAspect;

        int rotAngle = webCamTexture.videoRotationAngle;
        bool isRotated90 = (rotAngle == 90 || rotAngle == 270);
        float rawAspect = (float)webCamTexture.width / Mathf.Max(1f, (float)webCamTexture.height);
        float displayCamAspect = isRotated90 ? (1f / rawAspect) : rawAspect;

        float envW_Bg, envH_Bg;
        if (displayCamAspect > screenAspect)
        {
            envH_Bg = frustumH_Bg;
            envW_Bg = frustumH_Bg * displayCamAspect;
        }
        else
        {
            envW_Bg = frustumW_Bg;
            envH_Bg = frustumW_Bg / Mathf.Max(0.01f, displayCamAspect);
        }

        if (webcamBackgroundRenderer != null)
        {
            Transform quadT = webcamBackgroundRenderer.transform;
            quadT.localPosition = new Vector3(0f, 0f, BG_QUAD_DEPTH_Z);
            quadT.localEulerAngles = new Vector3(0f, 0f, -rotAngle);
            quadT.localScale = isRotated90
                ? new Vector3(envH_Bg, envW_Bg, 1f)
                : new Vector3(envW_Bg, envH_Bg, 1f);

            if (runtimeWebcamMat != null)
            {
                float sx = isFrontFacingOrMirrored ? -1f : 1f;
                float ox = isFrontFacingOrMirrored ? 1f : 0f;
                float sy = webCamTexture.videoVerticallyMirrored ? -1f : 1f;
                float oy = webCamTexture.videoVerticallyMirrored ? 1f : 0f;
                runtimeWebcamMat.mainTextureScale = new Vector2(sx, sy);
                runtimeWebcamMat.mainTextureOffset = new Vector2(ox, oy);
                if (runtimeWebcamMat.HasProperty("_BaseMap_ST"))
                    runtimeWebcamMat.SetVector("_BaseMap_ST", new Vector4(sx, sy, ox, oy));
            }
        }

        // 2. Run YuNet 640x640 AI Face & Landmark Detection every 2nd frame
        frameCounter++;
        if (faceWorker != null && inputTensor != null && detectorCropRT != null && (frameCounter % 2 == 0))
        {
            DetectFaceAndComputeHatPose(rawAspect, envW_Bg, envH_Bg, screenAspect, halfFovRad);
        }
    }

    private void DetectFaceAndComputeHatPose(float rawAspect, float envW_Bg, float envH_Bg, float screenAspect, float halfFovRad)
    {
        try
        {
            // Blit an un-stretched 1:1 center square from webCamTexture into detectorCropRT (640x640)
            Vector2 cropScale = Vector2.one;
            Vector2 cropOffset = Vector2.zero;
            if (rawAspect > 1f)
            {
                cropScale.x = 1f / rawAspect;
                cropOffset.x = (1f - cropScale.x) * 0.5f;
            }
            else if (rawAspect < 1f)
            {
                cropScale.y = rawAspect;
                cropOffset.y = (1f - cropScale.y) * 0.5f;
            }

            Vector2 blitScale = cropScale;
            Vector2 blitOffset = cropOffset;
            if (webCamTexture != null && webCamTexture.videoVerticallyMirrored)
            {
                blitOffset.y = cropOffset.y + cropScale.y;
                blitScale.y = -cropScale.y;
            }

            Graphics.Blit(webCamTexture, detectorCropRT, blitScale, blitOffset);
            TextureConverter.ToTensor(detectorCropRT, inputTensor, tensorTransform);
            faceWorker.Schedule(inputTensor);

            float bestScore = 0.72f;
            float bestBoxX = 0f, bestBoxY = 0f, bestBoxW = 0f, bestBoxH = 0f;
            float bestReX = 0f, bestReY = 0f;
            float bestLeX = 0f, bestLeY = 0f;
            float bestNtX = 0f, bestNtY = 0f;
            float bestRmX = 0f, bestRmY = 0f;
            float bestLmX = 0f, bestLmY = 0f;
            bool found = false;

            for (int s = 0; s < STRIDES.Length; s++)
            {
                int stride = STRIDES[s];
                int cols = YUNET_SIZE / stride;

                Tensor<float> clsT = faceWorker.PeekOutput(CLS_NAMES[s]) as Tensor<float>;
                Tensor<float> objT = faceWorker.PeekOutput(OBJ_NAMES[s]) as Tensor<float>;
                Tensor<float> bboxT = faceWorker.PeekOutput(BBOX_NAMES[s]) as Tensor<float>;
                Tensor<float> kpsT = faceWorker.PeekOutput(KPS_NAMES[s]) as Tensor<float>;

                if (clsT == null || objT == null || bboxT == null || kpsT == null)
                    continue;

                float[] cls = clsT.DownloadToArray();
                float[] obj = objT.DownloadToArray();
                float[] bbox = bboxT.DownloadToArray();
                float[] kps = kpsT.DownloadToArray();

                for (int idx = 0; idx < cls.Length; idx++)
                {
                    float score = Mathf.Sqrt(Mathf.Clamp01(cls[idx]) * Mathf.Clamp01(obj[idx]));
                    if (score <= bestScore)
                        continue;

                    int r = idx / cols;
                    int c = idx % cols;
                    int bIdx = idx * 4;
                    float cx = (c + bbox[bIdx + 0]) * stride;
                    float cy = (r + bbox[bIdx + 1]) * stride;
                    float w = Mathf.Exp(bbox[bIdx + 2]) * stride;
                    float h = Mathf.Exp(bbox[bIdx + 3]) * stride;

                    // Validate plausible face dimensions and aspect ratio in 640x640 space
                    if (w < 65f || h < 65f || w > 520f || h > 560f)
                        continue;
                    float aspect = h / Mathf.Max(1f, w);
                    if (aspect < 0.78f || aspect > 1.75f)
                        continue;

                    int kIdx = idx * 10;
                    float reX = (kps[kIdx + 0] + c) * stride;
                    float reY = (kps[kIdx + 1] + r) * stride;
                    float leX = (kps[kIdx + 2] + c) * stride;
                    float leY = (kps[kIdx + 3] + r) * stride;
                    float ntX = (kps[kIdx + 4] + c) * stride;
                    float ntY = (kps[kIdx + 5] + r) * stride;
                    float rmX = (kps[kIdx + 6] + c) * stride;
                    float rmY = (kps[kIdx + 7] + r) * stride;
                    float lmX = (kps[kIdx + 8] + c) * stride;
                    float lmY = (kps[kIdx + 9] + r) * stride;

                    // Validate upright human facial landmark geometry (eyes above nose, nose above mouth)
                    float avgEyeY = 0.5f * (reY + leY);
                    float avgMouthY = 0.5f * (rmY + lmY);
                    float eyeSpanX = leX - reX;
                    if (eyeSpanX < w * 0.18f || Mathf.Abs(leY - reY) > h * 0.30f)
                        continue;
                    if (ntY <= avgEyeY + h * 0.05f || avgMouthY <= ntY + h * 0.05f)
                        continue;
                    if (ntX < reX - w * 0.12f || ntX > leX + w * 0.12f)
                        continue;

                    bestScore = score;
                    found = true;
                    bestBoxX = cx - w * 0.5f;
                    bestBoxY = cy - h * 0.5f;
                    bestBoxW = w;
                    bestBoxH = h;
                    bestReX = reX; bestReY = reY;
                    bestLeX = leX; bestLeY = leY;
                    bestNtX = ntX; bestNtY = ntY;
                    bestRmX = rmX; bestRmY = rmY;
                    bestLmX = lmX; bestLmY = lmY;
                }
            }

            if (!found) return;

            lastFaceDetectedTime = Time.time;

            // Convert 640x640 detector coordinates directly to 3D Camera-Local position at depth HEAD_DEPTH_Z (2.0m)
            float depthScale = HEAD_DEPTH_Z / BG_QUAD_DEPTH_Z;
            float envW_Head = envW_Bg * depthScale;
            float envH_Head = envH_Bg * depthScale;

            Vector2 posRightEye = ToHeadPlanePoint(bestReX, bestReY, cropScale, cropOffset, envW_Head, envH_Head);
            Vector2 posLeftEye = ToHeadPlanePoint(bestLeX, bestLeY, cropScale, cropOffset, envW_Head, envH_Head);
            Vector2 posNose = ToHeadPlanePoint(bestNtX, bestNtY, cropScale, cropOffset, envW_Head, envH_Head);
            Vector2 posMouthMid = 0.5f * (
                ToHeadPlanePoint(bestRmX, bestRmY, cropScale, cropOffset, envW_Head, envH_Head) +
                ToHeadPlanePoint(bestLmX, bestLmY, cropScale, cropOffset, envW_Head, envH_Head));

            Vector2 posBoxTL = ToHeadPlanePoint(bestBoxX, bestBoxY, cropScale, cropOffset, envW_Head, envH_Head);
            Vector2 posBoxBR = ToHeadPlanePoint(bestBoxX + bestBoxW, bestBoxY + bestBoxH, cropScale, cropOffset, envW_Head, envH_Head);

            float faceWorldW = Mathf.Abs(posBoxBR.x - posBoxTL.x);
            float faceWorldH = Mathf.Abs(posBoxTL.y - posBoxBR.y);

            Vector2 posEyeMid = 0.5f * (posRightEye + posLeftEye);

            // Compute head Roll angle from the eye line in world units (already aspect-correct!)
            Vector2 leftScreenEye = (posRightEye.x <= posLeftEye.x) ? posRightEye : posLeftEye;
            Vector2 rightScreenEye = (posRightEye.x <= posLeftEye.x) ? posLeftEye : posRightEye;

            float dx = rightScreenEye.x - leftScreenEye.x;
            float dy = rightScreenEye.y - leftScreenEye.y;
            float rollDeg = Mathf.Atan2(dy, Mathf.Max(0.001f, dx)) * Mathf.Rad2Deg;
            rollDeg = Mathf.Clamp(rollDeg, -45f, 45f);

            float rollRad = rollDeg * Mathf.Deg2Rad;
            Vector2 headUpWorld = new Vector2(-Mathf.Sin(rollRad), Mathf.Cos(rollRad));

            // Place Hat Anchor on the forehead/brow line above the eyes so the crown sits ON TOP of the head like a hat
            Vector2 foreheadPos2D = posEyeMid + headUpWorld * (faceWorldH * foreheadHeightFactor);
            float clampedX = Mathf.Clamp(foreheadPos2D.x, -0.42f, 0.42f);
            float clampedY = Mathf.Clamp(foreheadPos2D.y, -0.05f, 0.54f);
            targetHeadLocalPos = new Vector3(clampedX, clampedY, HEAD_DEPTH_Z);

            // Compute 3D Head Yaw (turning left/right) and Pitch (nodding up/down)
            float eyeWorldDist = Mathf.Max(0.03f, Vector2.Distance(leftScreenEye, rightScreenEye));
            float noseOffsetNorm = (posNose.x - posEyeMid.x) / eyeWorldDist;
            float yawDeg = Mathf.Clamp(-noseOffsetNorm * 55f, -45f, 45f);

            float eyeToNoseY = posEyeMid.y - posNose.y;
            float noseToMouthY = Mathf.Max(0.01f, posNose.y - posMouthMid.y);
            float pitchRatio = (eyeToNoseY / noseToMouthY) - 1.15f;
            float pitchDeg = Mathf.Clamp(pitchRatio * 22f, -25f, 25f);

            targetHeadLocalRot = Quaternion.Euler(pitchDeg, 180f + yawDeg, -rollDeg);

            // Compute dynamic 3D scale (normalized to 2.0m reference mesh width) so the crown fits the head like a hat
            float desiredCrownWorldWidth = Mathf.Max(faceWorldW * 1.25f, eyeWorldDist * 2.8f) * hatScaleMultiplier;
            float uniformScale = Mathf.Clamp(desiredCrownWorldWidth * 0.5f, 0.22f, 0.42f);
            targetCrownScale = new Vector3(uniformScale, uniformScale, uniformScale);

            if (frameCounter % 30 == 0)
            {
                Debug.Log($"[HatTrack] score={bestScore:F2} box=({bestBoxX:F0},{bestBoxY:F0},{bestBoxW:F0},{bestBoxH:F0}) re=({bestReX:F0},{bestReY:F0}) le=({bestLeX:F0},{bestLeY:F0}) eyeMid={posEyeMid} faceWH=({faceWorldW:F2},{faceWorldH:F2}) forehead={foreheadPos2D} scale={uniformScale:F2} vMir={webCamTexture.videoVerticallyMirrored} rot={webCamTexture.videoRotationAngle}");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NusantaraCrown] YuNet frame warning: {e.Message}");
        }
    }

    private Vector2 ToHeadPlanePoint(float x640, float y640, Vector2 cropScale, Vector2 cropOffset, float envW_Head, float envH_Head)
    {
        float uSq = x640 / (float)YUNET_SIZE;
        float vSq = 1f - (y640 / (float)YUNET_SIZE);

        float uRaw = cropOffset.x + uSq * cropScale.x;
        float vRaw = cropOffset.y + vSq * cropScale.y;

        if (isFrontFacingOrMirrored)
            uRaw = 1f - uRaw;

        float worldX = (uRaw - 0.5f) * envW_Head;
        float worldY = (vRaw - 0.5f) * envH_Head;
        return new Vector2(worldX, worldY);
    }

    private void UpdateNativeARFaceTracking()
    {
        // Do nothing until a native AR face subsystem is actually running
        // (prevents wiping the head anchor during startup / on Laptop before webcam mode activates)
        if (faceManager == null || !faceManager.enabled || faceManager.subsystem == null || !faceManager.subsystem.running)
            return;

        bool faceFound = false;
        foreach (var face in faceManager.trackables)
        {
            if (face.trackingState == TrackingState.Tracking)
            {
                faceFound = true;
                if (activeFaceTransform != face.transform)
                {
                    activeFaceTransform = face.transform;
                    AttachCrownToFace(activeFaceTransform);
                }
                break;
            }
        }

        if (txtTrackingStatus != null)
            txtTrackingStatus.text = faceFound ? "Wajah Terkunci" : "Arahkan wajah ke kamera";

        if (faceGuideReticle != null)
            faceGuideReticle.SetActive(!faceFound);

        if (!faceFound && activeCrownInstance != null && activeCrownInstance.activeSelf)
        {
            activeCrownInstance.SetActive(false);
            activeFaceTransform = null;
        }
    }

    private void OnDestroy()
    {
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            Destroy(webCamTexture);
        }
        if (detectorCropRT != null)
        {
            detectorCropRT.Release();
            Destroy(detectorCropRT);
        }
        if (faceWorker != null)
        {
            faceWorker.Dispose();
        }
        if (inputTensor != null)
        {
            inputTensor.Dispose();
        }
    }

    // ================= PUBLIC UI CALLBACKS =================

    public void SelectIsland(int islandIndex)
    {
        currentIsland = (Island)Mathf.Clamp(islandIndex, 0, 4);
        UpdateIslandCardsUI();
        UpdateCrownVisual();
    }

    public void SetGender(int genderIndex)
    {
        currentGender = (Gender)Mathf.Clamp(genderIndex, 0, 1);
        UpdateGenderUIState();
        UpdateIslandCardsUI();
        UpdateCrownVisual();
    }

    public void ToggleInfoModal()
    {
        if (infoModalPanel != null)
            infoModalPanel.SetActive(!infoModalPanel.activeSelf);
    }

    public void CloseInfoModal()
    {
        if (infoModalPanel != null)
            infoModalPanel.SetActive(false);
    }

    public void ToggleFlipOrRotation()
    {
        if (isUsingWebCamMode && webCamDevices != null && webCamDevices.Length > 1)
        {
            StartWebCamByIndex(currentCamIndex + 1);
        }
        else if (isUsingWebCamMode)
        {
            isFrontFacingOrMirrored = !isFrontFacingOrMirrored;
        }
    }

    public void CaptureScreen()
    {
        StartCoroutine(CaptureAndShowPreviewRoutine());
    }

    public void CloseCapturePreview()
    {
        if (capturePreviewPanel != null)
            capturePreviewPanel.SetActive(false);
    }

    public void SaveCapturedPhoto()
    {
        string filename = $"NusantaraAR_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        if (lastCapturedTexture != null)
        {
            string fullPath = System.IO.Path.Combine(Application.persistentDataPath, filename);
            System.IO.File.WriteAllBytes(fullPath, lastCapturedTexture.EncodeToPNG());
            Debug.Log($"[NusantaraAR] Foto tersimpan di: {fullPath}");
        }
        else
        {
            ScreenCapture.CaptureScreenshot(filename);
            Debug.Log($"[NusantaraAR] Screenshot: {filename}");
        }
        CloseCapturePreview();
    }

    private IEnumerator CaptureAndShowPreviewRoutine()
    {
        yield return new WaitForEndOfFrame();

        if (lastCapturedTexture != null)
            Destroy(lastCapturedTexture);

        lastCapturedTexture = ScreenCapture.CaptureScreenshotAsTexture();
        if (imgCapturedPreview != null && lastCapturedTexture != null)
        {
            imgCapturedPreview.texture = lastCapturedTexture;
            imgCapturedPreview.color = Color.white;
        }

        CrownData data = GetActiveCrownData();
        if (txtPreviewPill != null && data != null)
        {
            txtPreviewPill.text = $"Foto · {data.crownName}";
        }

        if (capturePreviewPanel != null)
            capturePreviewPanel.SetActive(true);
    }

    // ================= CORE AR & UI SYNC LOGIC =================

    private CrownData GetActiveCrownData()
    {
        CrownData data = crownList.Find(c => c.island == currentIsland && c.gender == currentGender);
        if (data == null)
            data = crownList.Find(c => c.island == currentIsland);
        return data;
    }

    private void UpdateCrownVisual()
    {
        CrownData data = GetActiveCrownData();
        if (data == null) return;

        string headerText = !string.IsNullOrEmpty(data.headerPillText)
            ? data.headerPillText
            : $"{data.crownName} — {data.originRegion}";

        if (txtHeaderPill != null) txtHeaderPill.text = headerText;
        if (txtCrownName != null) txtCrownName.text = data.crownName;
        if (txtRegion != null) txtRegion.text = data.originRegion;
        if (txtDescription != null) txtDescription.text = data.description;

        if (txtMaknaSimbolik != null && !string.IsNullOrEmpty(data.maknaSimbolik))
            txtMaknaSimbolik.text = data.maknaSimbolik;
        if (txtBahanTradisional != null && !string.IsNullOrEmpty(data.bahanTradisional))
            txtBahanTradisional.text = data.bahanTradisional;
        if (txtMomenPenggunaan != null && !string.IsNullOrEmpty(data.momenPenggunaan))
            txtMomenPenggunaan.text = data.momenPenggunaan;

        if (activeCrownInstance != null)
            Destroy(activeCrownInstance);
        activeCrownInstance = null;

        // Fall back to the webcam head anchor whenever no native AR face is assigned
        if (activeFaceTransform == null && headAnchorGO != null)
            activeFaceTransform = headAnchorGO.transform;

        if (data.crownPrefab != null && activeFaceTransform != null)
        {
            AttachCrownToFace(activeFaceTransform);
        }
    }

    private void AttachCrownToFace(Transform faceTransform)
    {
        CrownData data = GetActiveCrownData();
        if (data == null || data.crownPrefab == null || faceTransform == null) return;

        if (activeCrownInstance != null)
            Destroy(activeCrownInstance);

        // Ensure no lingering clones under the anchor
        foreach (Transform child in faceTransform)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        activeCrownInstance = Instantiate(data.crownPrefab, faceTransform);

        // Normalize across all 10 FBX models (01..05 have mesh size ~2.2, 06..10 have mesh size ~0.02 with rootScale 100)
        // Measure the combined renderer bounds of the whole model at unit scale.
        currentCrownNormalizationFactor = 1.0f;
        activeCrownInstance.transform.localPosition = Vector3.zero;
        activeCrownInstance.transform.localRotation = Quaternion.identity;
        activeCrownInstance.transform.localScale = Vector3.one;
        Renderer[] rends = activeCrownInstance.GetComponentsInChildren<Renderer>(true);
        if (rends.Length > 0)
        {
            Bounds total = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) total.Encapsulate(rends[i].bounds);
            float parentScale = Mathf.Max(0.0001f, Mathf.Abs(faceTransform.lossyScale.x));
            float rawSpan = Mathf.Max(total.size.x, Mathf.Max(total.size.y, total.size.z)) / parentScale;
            if (rawSpan > 0.00001f)
                currentCrownNormalizationFactor = 2.0f / rawSpan;
        }

        if (isUsingWebCamMode || faceTransform == (headAnchorGO != null ? headAnchorGO.transform : null))
        {
            activeCrownInstance.transform.localPosition = localHatOffset;
            activeCrownInstance.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            activeCrownInstance.transform.localScale = targetCrownScale * currentCrownNormalizationFactor;
        }
        else
        {
            activeCrownInstance.transform.localPosition = arFaceHatOffset;
            activeCrownInstance.transform.localRotation = Quaternion.Euler(-90f, 180f, 0f);
            activeCrownInstance.transform.localScale = arFaceCrownScale * currentCrownNormalizationFactor;
        }

        activeCrownInstance.SetActive(true);
    }

    private void UpdateGenderUIState()
    {
        bool isFemale = (currentGender == Gender.Female);

        if (imgFemaleBg != null)
            imgFemaleBg.color = isFemale ? Color.white : new Color(1f, 1f, 1f, 0f);
        if (imgMaleBg != null)
            imgMaleBg.color = !isFemale ? Color.white : new Color(1f, 1f, 1f, 0f);

        if (txtFemaleLabel != null)
            txtFemaleLabel.color = isFemale ? ColorDarkText : ColorWhiteText;
        if (txtMaleLabel != null)
            txtMaleLabel.color = !isFemale ? ColorDarkText : ColorWhiteText;
    }

    private void UpdateIslandCardsUI()
    {
        for (int i = 0; i < 5; i++)
        {
            bool isSelected = ((int)currentIsland == i);
            if (islandCardBgs != null && i < islandCardBgs.Length && islandCardBgs[i] != null)
            {
                if (spriteCardActive != null && spriteCardDefault != null)
                    islandCardBgs[i].sprite = isSelected ? spriteCardActive : spriteCardDefault;
            }
            if (islandCardRects != null && i < islandCardRects.Length && islandCardRects[i] != null)
            {
                islandCardRects[i].sizeDelta = isSelected
                    ? new Vector2(Mathf.Round(118f * UI_SCALE), Mathf.Round(86f * UI_SCALE))
                    : new Vector2(Mathf.Round(104f * UI_SCALE), Mathf.Round(78f * UI_SCALE));
            }
            if (islandCardSubtitles != null && i < islandCardSubtitles.Length && islandCardSubtitles[i] != null)
            {
                Island isl = (Island)i;
                CrownData d = crownList.Find(c => c.island == isl && c.gender == currentGender);
                if (d == null) d = crownList.Find(c => c.island == isl);
                if (d != null && !string.IsNullOrEmpty(d.shortSubtitle))
                    islandCardSubtitles[i].text = d.shortSubtitle;
            }
        }
    }
}
