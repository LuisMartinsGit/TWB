using UnityEngine;
using UnityEngine.UI;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine.EventSystems;

namespace TheWaningBorder.Map
{
    [DefaultExecutionOrder(2000)]
    public sealed class Minimap : MonoBehaviour
    {
        [Header("Placement")]
        public int sizePixels = 256;
        public Vector2 offsetBR = new Vector2(20, 20);

        [Header("Map")]
        public Vector2 worldMin = new Vector2(-125, -125);
        public Vector2 worldMax = new Vector2(125, 125);
        public int samples = 128;

        [Header("FOW + Factions")]
        public Faction humanFaction = Faction.Blue;

        [Header("Background Colors")]
        public Color colUnseen = new Color(0f, 0f, 0f, 1f);
        public Color colRevealed = new Color(0.18f, 0.18f, 0.18f, 1f);
        public Color colVisible = new Color(0.35f, 0.35f, 0.35f, 1f);

        [Header("Blip Radii")]
        public int unitRadiusPx = 2;
        public int buildingRadiusPx = 3;

        [Header("Update")]
        public float refreshInterval = 0.1f;

        [Header("Dependencies (assign or leave empty)")]
        [Tooltip("Canvas to host the minimap. If empty, a new Canvas will be created.")]
        public Canvas targetCanvas;

        [Tooltip("Optional explicit FogOfWarManager reference. If empty, uses FogOfWarManager.Instance if available.")]
        public FogOfWarManager fogOfWar;

        [Tooltip("The RTSCameraRig to move on minimap clicks. Must be assigned by Bootstrap or in the Inspector.")]
        public RTSCameraRig cameraRig;

        [Header("Debug")]
        public bool logClicks = false;

        // UI
        private RawImage _raw;
        private RectTransform _rawRect;
        private Texture2D _tex;

        // Camera view lines (4 edges)
        private Image[] _viewLines;

        // Buffers
        private Color[] _bgBuffer;
        private Color[] _frame;

        // ECS
        private World _world;
        private EntityManager _em;
        private EntityQuery _unitsQ;
        private EntityQuery _buildingsQ;

        // FoW
        private FogOfWarManager _fow;
        private float _timer;

        void Awake()
        {
            // Ensure EventSystem (no FindObjectOfType)
            if (EventSystem.current == null)
            {
                var esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                esGO.hideFlags = HideFlags.DontSave;
            }

            // Resolve FoW via serialized ref or singleton (no FindObjectOfType)
            _fow = fogOfWar != null ? fogOfWar : FogOfWarManager.Instance;
            if (_fow != null)
            {
                worldMin = _fow.WorldMin;
                worldMax = _fow.WorldMax;
                humanFaction = _fow.HumanFaction;
            }

            samples = Mathf.Clamp(samples, 64, Mathf.Min(512, sizePixels));

            _tex = new Texture2D(sizePixels, sizePixels, TextureFormat.RGBA32, false, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };

            EnsureCanvasAndImage(targetCanvas); // creates _raw/_rawRect and overlay lines

            _bgBuffer = new Color[samples * samples];
            _frame = new Color[sizePixels * sizePixels];

            _world = World.DefaultGameObjectInjectionWorld;
            _em = _world.EntityManager;

            _unitsQ = _em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitTag>(),
                ComponentType.ReadOnly<FactionTag>(),
                ComponentType.ReadOnly<LocalTransform>());

            _buildingsQ = _em.CreateEntityQuery(
                ComponentType.ReadOnly<BuildingTag>(),
                ComponentType.ReadOnly<FactionTag>(),
                ComponentType.ReadOnly<LocalTransform>());
        }

        void OnDestroy()
        {
            if (_tex != null) Destroy(_tex);
        }

        void Update()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < refreshInterval) return;
            _timer = 0f;

            BuildBackground();
            BlitBackgroundToFrame();
            DrawBlips();

            _tex.SetPixels(_frame);
            _tex.Apply(false, false);
        }

        void LateUpdate()
        {
            // Refresh FoW bounds if the singleton becomes available later (no FindObjectOfType)
            if (_fow == null)
            {
                _fow = FogOfWarManager.Instance;
                if (_fow != null)
                {
                    worldMin = _fow.WorldMin;
                    worldMax = _fow.WorldMax;
                    humanFaction = _fow.HumanFaction;
                }
            }

            UpdateCameraViewRect();
        }

        // ---------------- Background ----------------
        private void BuildBackground()
        {
            float minX = worldMin.x, minZ = worldMin.y;
            float maxX = worldMax.x, maxZ = worldMax.y;

            for (int y = 0; y < samples; y++)
            {
                float vz = Mathf.Lerp(minZ, maxZ, (y + 0.5f) / samples);
                for (int x = 0; x < samples; x++)
                {
                    float vx = Mathf.Lerp(minX, maxX, (x + 0.5f) / samples);

                    bool vis = FogOfWarSystem.IsVisibleToFaction(humanFaction, new float3(vx, 0f, vz));
                    bool rev = vis || FogOfWarSystem.IsRevealedToFaction(humanFaction, new float3(vx, 0f, vz));

                    _bgBuffer[y * samples + x] = vis ? colVisible : (rev ? colRevealed : colUnseen);
                }
            }
        }

        private void BlitBackgroundToFrame()
        {
            int W = sizePixels, H = sizePixels, S = samples;
            for (int y = 0; y < H; y++)
            {
                int sy = (int)((y / (float)H) * S); if (sy >= S) sy = S - 1;
                for (int x = 0; x < W; x++)
                {
                    int sx = (int)((x / (float)W) * S); if (sx >= S) sx = S - 1;
                    _frame[y * W + x] = _bgBuffer[sy * S + sx];
                }
            }
        }

        // ---------------- Blips ----------------
        private void DrawBlips()
        {
            // Units
            using (var ents = _unitsQ.ToEntityArray(Allocator.Temp))
            using (var facs = _unitsQ.ToComponentDataArray<FactionTag>(Allocator.Temp))
            using (var xfs = _unitsQ.ToComponentDataArray<LocalTransform>(Allocator.Temp))
            {
                for (int i = 0; i < ents.Length; i++)
                {
                    var pos = xfs[i].Position;
                    Faction fac = facs[i].Value;
                    bool mine = fac == humanFaction;

                    bool show = mine || FogOfWarSystem.IsVisibleToFaction(humanFaction, pos);
                    if (!show) continue;

                    Color c = FactionColors.Get(fac);
                    int2 p = WorldToPixel(pos);
                    DrawDisc(p.x, p.y, unitRadiusPx, c);
                }
            }

            // Buildings
            using (var ents = _buildingsQ.ToEntityArray(Allocator.Temp))
            using (var facs = _buildingsQ.ToComponentDataArray<FactionTag>(Allocator.Temp))
            using (var xfs = _buildingsQ.ToComponentDataArray<LocalTransform>(Allocator.Temp))
            {
                for (int i = 0; i < ents.Length; i++)
                {
                    var pos = xfs[i].Position;
                    Faction fac = facs[i].Value;
                    bool mine = fac == humanFaction;

                    bool vis = FogOfWarSystem.IsVisibleToFaction(humanFaction, pos);
                    bool rev = vis || FogOfWarSystem.IsRevealedToFaction(humanFaction, pos);
                    if (!mine && !rev) continue;

                    Color baseCol = FactionColors.Get(fac);
                    Color c = vis ? baseCol : FactionColors.Ghost(baseCol, 0.55f);

                    int2 p = WorldToPixel(pos);
                    DrawDisc(p.x, p.y, buildingRadiusPx, c);
                }
            }
        }

        // ---------------- Helpers ----------------
        private int2 WorldToPixel(float3 world)
        {
            float u = Mathf.InverseLerp(worldMin.x, worldMax.x, world.x);
            float v = Mathf.InverseLerp(worldMin.y, worldMax.y, world.z);

            int x = Mathf.Clamp(Mathf.RoundToInt(u * (sizePixels - 1)), 0, sizePixels - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(v * (sizePixels - 1)), 0, sizePixels - 1);
            return new int2(x, y);
        }

        private void DrawDisc(int cx, int cy, int r, Color c)
        {
            int r2 = r * r;
            int W = sizePixels, H = sizePixels;

            int x0 = Mathf.Max(0, cx - r);
            int x1 = Mathf.Min(W - 1, cx + r);
            int y0 = Mathf.Max(0, cy - r);
            int y1 = Mathf.Min(H - 1, cy + r);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int dx = x - cx;
                    int dy = y - cy;
                    if (dx * dx + dy * dy <= r2)
                    {
                        ref Color dst = ref _frame[y * W + x];
                        float a = c.a;
                        dst = new Color(
                            dst.r * (1 - a) + c.r * a,
                            dst.g * (1 - a) + c.g * a,
                            dst.b * (1 - a) + c.b * a,
                            1f);
                    }
                }
            }
        }

        private void EnsureCanvasAndImage(Canvas canvas)
        {
            // Use provided canvas or create a dedicated one (no FindObjectOfType)
            if (canvas == null)
            {
                var cgo = new GameObject("MinimapCanvas");
                cgo.layer = LayerMask.NameToLayer("UI");
                canvas = cgo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                cgo.AddComponent<CanvasScaler>();
                cgo.AddComponent<GraphicRaycaster>();
                targetCanvas = canvas;
            }
            else
            {
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            // RawImage host
            var go = new GameObject("MinimapFlat", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(canvas.transform, false);

            _rawRect = go.GetComponent<RectTransform>();
            _raw = go.GetComponent<RawImage>();
            _raw.texture = _tex;
            _raw.raycastTarget = true;

            _rawRect.sizeDelta = new Vector2(sizePixels, sizePixels);
            _rawRect.anchorMin = new Vector2(1, 0);
            _rawRect.anchorMax = new Vector2(1, 0);
            _rawRect.pivot = new Vector2(1, 0);
            _rawRect.anchoredPosition = new Vector2(-offsetBR.x, offsetBR.y);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(1, 1, 1, 0.5f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Click proxy lives on the RawImage object
            var proxy = go.AddComponent<MinimapClickProxy>();
            proxy.owner = this;

            // Create 4 thin white lines to show camera frustum edges
            _viewLines = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var lineGO = new GameObject($"CameraViewLine{i}", typeof(RectTransform), typeof(Image));
                lineGO.transform.SetParent(go.transform, false);

                var lineRect = lineGO.GetComponent<RectTransform>();
                var lineImg = lineGO.GetComponent<Image>();

                lineImg.color = Color.white;
                lineImg.raycastTarget = false;

                // Match parent's anchor/pivot for consistent positioning
                lineRect.anchorMin = new Vector2(1, 0);
                lineRect.anchorMax = new Vector2(1, 0);
                lineRect.pivot = new Vector2(0, 0.5f);

                _viewLines[i] = lineImg;
            }
        }

        // ---------------- Camera view rectangle ----------------
        private void UpdateCameraViewRect()
        {
            if (_viewLines == null || _rawRect == null) return;
            var main = Camera.main;
            if (!main) return;

            Vector3 p00 = RayToGround(main, new Vector2(0f, 0f)); // Bottom-left
            Vector3 p10 = RayToGround(main, new Vector2(1f, 0f)); // Bottom-right
            Vector3 p11 = RayToGround(main, new Vector2(1f, 1f)); // Top-right
            Vector3 p01 = RayToGround(main, new Vector2(0f, 1f)); // Top-left

            Vector2 px00 = WorldToMinimapPixel(p00);
            Vector2 px10 = WorldToMinimapPixel(p10);
            Vector2 px11 = WorldToMinimapPixel(p11);
            Vector2 px01 = WorldToMinimapPixel(p01);

            DrawLine(0, px00, px10);
            DrawLine(1, px10, px11);
            DrawLine(2, px11, px01);
            DrawLine(3, px01, px00);
        }

        private Vector2 WorldToMinimapPixel(Vector3 worldPos)
        {
            float x = worldPos.x;
            float z = worldPos.z;

            float u = Mathf.InverseLerp(worldMin.x, worldMax.x, x);
            float v = Mathf.InverseLerp(worldMin.y, worldMax.y, z);

            float w = _rawRect.rect.width;
            float h = _rawRect.rect.height;

            float pixelX = -(w - u * w);
            float pixelY = v * h;

            return new Vector2(pixelX, pixelY);
        }

        private void DrawLine(int lineIndex, Vector2 start, Vector2 end)
        {
            if (lineIndex < 0 || lineIndex >= _viewLines.Length) return;

            var lineRect = _viewLines[lineIndex].rectTransform;

            Vector2 diff = end - start;
            float length = diff.magnitude;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            lineRect.anchoredPosition = start;
            lineRect.sizeDelta = new Vector2(length, 2f);
            lineRect.localRotation = Quaternion.Euler(0, 0, angle);
        }

        private static Vector3 RayToGround(Camera cam, Vector2 viewport01)
        {
            Plane ground = new Plane(Vector3.up, Vector3.zero); // Y=0
            Ray r = cam.ViewportPointToRay(new Vector3(viewport01.x, viewport01.y, 0f));
            if (ground.Raycast(r, out float t)) return r.GetPoint(t);
            Vector3 p = r.origin + r.direction * 1000f;
            return new Vector3(p.x, 0f, p.z);
        }

        // ---------------- Click-to-move camera ----------------
        internal void HandleClick(PointerEventData eventData)
        {
            if (_rawRect == null) return;

            // Only act if inside the minimap rect
            if (!RectTransformUtility.RectangleContainsScreenPoint(_rawRect, eventData.position, eventData.pressEventCamera))
                return;

            // No search: require explicit cameraRig assignment (via Inspector or Bootstrap)
            if (cameraRig == null) return;

            Rect rect = _rawRect.rect;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rawRect, eventData.position, eventData.pressEventCamera, out local);

            Vector2 bottomLeftPx = local + Vector2.Scale(rect.size, _rawRect.pivot);
            float u = Mathf.Clamp01(bottomLeftPx.x / Mathf.Max(1e-6f, rect.width));
            float v = Mathf.Clamp01(bottomLeftPx.y / Mathf.Max(1e-6f, rect.height));

            float targetX = Mathf.Lerp(worldMin.x, worldMax.x, u);
            float targetZ = Mathf.Lerp(worldMin.y, worldMax.y, v);

            cameraRig.MoveToPosition(new Vector3(targetX, 0f, targetZ), instant: false);

            if (logClicks)
            {
                Debug.Log($"[Minimap] Click -> ({targetX:F1}, {targetZ:F1})");
            }
        }

        // ---------- proxy lives on the RawImage object to forward UI clicks here ----------
        private sealed class MinimapClickProxy : MonoBehaviour, IPointerClickHandler
        {
            public Minimap owner;
            public void OnPointerClick(PointerEventData eventData) => owner?.HandleClick(eventData);
        }
    }
}
