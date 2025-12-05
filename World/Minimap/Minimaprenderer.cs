using UnityEngine;
using UnityEngine.UI;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine.EventSystems;
using TheWaningBorder.World.FogOfWar;

namespace TheWaningBorder.World.Minimap
{
    /// <summary>
    /// Flat (no cameras) FoW-aware minimap rendered into a Texture2D and shown in the lower-right UI.
    /// Blips are colored per-faction using FactionColors.Get(faction).
    /// - Enemy/neutral UNITS: drawn only when VISIBLE.
    /// - Enemy/neutral BUILDINGS: drawn when VISIBLE (solid) or REVEALED (ghost).
    /// - Player-owned always drawn (solid).
    /// 
    /// Features:
    /// - White rectangle showing the main camera's ground footprint on the minimap.
    /// - Click anywhere on the minimap to snap the camera there.
    /// </summary>
    [DefaultExecutionOrder(2000)]
    public sealed class MinimapRenderer : MonoBehaviour
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

        [Header("Camera Snap on Click")]
        [Tooltip("The RTSCameraRig component to move on minimap clicks. Auto-found if null.")]
        public RTSCameraRig cameraRig;
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
        private Unity.Entities.World _world;
        private EntityManager _em;
        private EntityQuery _unitsQ;
        private EntityQuery _buildingsQ;

        // FoW
        private FogOfWarManager _fow;

        private float _timer;

        void Awake()
        {
            // Ensure EventSystem for click handling
            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.hideFlags = HideFlags.DontSave;
            }

            _fow = FindObjectOfType<FogOfWarManager>();
            if (_fow != null)
            {
                worldMin = _fow.WorldMin;
                worldMax = _fow.WorldMax;
                humanFaction = _fow.HumanFaction;
            }

            if (GameSettings.IsMultiplayer)
            {
                humanFaction = GameSettings.LocalPlayerFaction;
            }

            samples = Mathf.Clamp(samples, 64, Mathf.Min(512, sizePixels));

            _tex = new Texture2D(sizePixels, sizePixels, TextureFormat.RGBA32, false, false);
            _tex.wrapMode = TextureWrapMode.Clamp;
            _tex.filterMode = FilterMode.Point;

            EnsureCanvasAndImage();

            _bgBuffer = new Color[samples * samples];
            _frame = new Color[sizePixels * sizePixels];

            _world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
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
            if (_fow != null)
            {
                worldMin = _fow.WorldMin;
                worldMax = _fow.WorldMax;
                humanFaction = _fow.HumanFaction;
            }

            if (GameSettings.IsMultiplayer)
            {
                humanFaction = GameSettings.LocalPlayerFaction;
            }

            UpdateCameraViewRect();
        }

        #region Background

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
                int sy = (int)((y / (float)H) * S);
                if (sy >= S) sy = S - 1;
                for (int x = 0; x < W; x++)
                {
                    int sx = (int)((x / (float)W) * S);
                    if (sx >= S) sx = S - 1;
                    _frame[y * W + x] = _bgBuffer[sy * S + sx];
                }
            }
        }

        #endregion

        #region Blips

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
                    Color c = vis ? baseCol : FactionColors.Ghost(baseCol, 0.5f);
                    int2 p = WorldToPixel(pos);
                    DrawDisc(p.x, p.y, buildingRadiusPx, c);
                }
            }
        }

        private int2 WorldToPixel(float3 pos)
        {
            float u = Mathf.InverseLerp(worldMin.x, worldMax.x, pos.x);
            float v = Mathf.InverseLerp(worldMin.y, worldMax.y, pos.z);
            int px = Mathf.Clamp(Mathf.FloorToInt(u * sizePixels), 0, sizePixels - 1);
            int py = Mathf.Clamp(Mathf.FloorToInt(v * sizePixels), 0, sizePixels - 1);
            return new int2(px, py);
        }

        private void DrawDisc(int cx, int cy, int r, Color col)
        {
            int r2 = r * r;
            for (int dy = -r; dy <= r; dy++)
            {
                int yy = cy + dy;
                if (yy < 0 || yy >= sizePixels) continue;
                for (int dx = -r; dx <= r; dx++)
                {
                    int xx = cx + dx;
                    if (xx < 0 || xx >= sizePixels) continue;
                    if (dx * dx + dy * dy <= r2)
                        _frame[yy * sizePixels + xx] = col;
                }
            }
        }

        #endregion

        #region Camera View Rectangle

        private void UpdateCameraViewRect()
        {
            if (_viewLines == null || _rawRect == null) return;
            var main = Camera.main;
            if (!main) return;

            Vector3 p00 = RayToGround(main, new Vector2(0f, 0f));
            Vector3 p10 = RayToGround(main, new Vector2(1f, 0f));
            Vector3 p11 = RayToGround(main, new Vector2(1f, 1f));
            Vector3 p01 = RayToGround(main, new Vector2(0f, 1f));

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
            float u = Mathf.InverseLerp(worldMin.x, worldMax.x, worldPos.x);
            float v = Mathf.InverseLerp(worldMin.y, worldMax.y, worldPos.z);

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
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            Ray r = cam.ViewportPointToRay(new Vector3(viewport01.x, viewport01.y, 0f));
            if (ground.Raycast(r, out float t)) return r.GetPoint(t);
            Vector3 p = r.origin + r.direction * 1000f;
            return new Vector3(p.x, 0f, p.z);
        }

        #endregion

        #region Click to Move Camera

        internal void HandleClick(PointerEventData eventData)
        {
            if (_rawRect == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rawRect, eventData.position, eventData.pressEventCamera, out Vector2 local);

            float w = _rawRect.rect.width;
            float h = _rawRect.rect.height;

            float u = (local.x + w) / w;
            float v = local.y / h;

            float worldX = Mathf.Lerp(worldMin.x, worldMax.x, u);
            float worldZ = Mathf.Lerp(worldMin.y, worldMax.y, v);

            worldX = Mathf.Clamp(worldX, worldMin.x, worldMax.x);
            worldZ = Mathf.Clamp(worldZ, worldMin.y, worldMax.y);

            if (logClicks)
                Debug.Log($"[Minimap] Click → World({worldX:F1}, {worldZ:F1})");

            if (cameraRig == null)
                cameraRig = FindObjectOfType<RTSCameraRig>();

            if (cameraRig != null)
                cameraRig.SnapTo(new Vector3(worldX, 0, worldZ));
        }

        #endregion

        #region UI Setup

        private void EnsureCanvasAndImage()
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var cGo = new GameObject("MinimapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = cGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
            }

            var rawGo = new GameObject("MinimapRaw", typeof(RawImage));
            rawGo.transform.SetParent(canvas.transform, false);

            _raw = rawGo.GetComponent<RawImage>();
            _raw.texture = _tex;

            _rawRect = _raw.rectTransform;
            _rawRect.anchorMin = new Vector2(1, 0);
            _rawRect.anchorMax = new Vector2(1, 0);
            _rawRect.pivot = new Vector2(1, 0);
            _rawRect.anchoredPosition = new Vector2(-offsetBR.x, offsetBR.y);
            _rawRect.sizeDelta = new Vector2(sizePixels, sizePixels);

            // Add click handler
            var proxy = rawGo.AddComponent<MinimapClickProxy>();
            proxy.minimap = this;

            // Create view lines
            _viewLines = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var lineGo = new GameObject($"ViewLine{i}", typeof(Image));
                lineGo.transform.SetParent(_rawRect, false);

                var lineImg = lineGo.GetComponent<Image>();
                lineImg.color = Color.white;
                lineImg.raycastTarget = false;

                var lineRect = lineImg.rectTransform;
                lineRect.anchorMin = new Vector2(1, 0);
                lineRect.anchorMax = new Vector2(1, 0);
                lineRect.pivot = new Vector2(0, 0.5f);

                _viewLines[i] = lineImg;
            }
        }

        #endregion
    }

    /// <summary>
    /// Proxy component to forward UI clicks to the minimap.
    /// </summary>
    public class MinimapClickProxy : MonoBehaviour, IPointerClickHandler
    {
        public MinimapRenderer minimap;

        public void OnPointerClick(PointerEventData eventData)
        {
            minimap?.HandleClick(eventData);
        }
    }
}