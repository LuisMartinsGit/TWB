using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using UnityEngine;
using TheWaningBorder.Core.GameManager;
using TheWaningBorder.Core.Settings;

namespace TheWaningBorder.Map.FogOfWar
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class FogUpdateSystem : SystemBase
    {
        private NativeArray<FogCellComponent> _fogGrid;
        private int _gridSizeX;
        private int _gridSizeZ;
        private float _cellSize;
        private float3 _gridOrigin;
        private bool _initialized = false;


        protected override void OnCreate()
        {
            RequireForUpdate<FogSettingsComponent>();
        }


        public void InitializeFog()
        {
            if (!GameSettings.FogOfWarEnabled) return;


            _gridSizeX = GameSettings.FogGridSize;
            _gridSizeZ = GameSettings.FogGridSize;
            _cellSize = GameSettings.FogCellSize;


            float halfMapSize = GameSettings.MapHalfSize;
            _gridOrigin = new float3(-halfMapSize, 0, -halfMapSize);

            // Create fog grid

            _fogGrid = new NativeArray<FogCellComponent>(_gridSizeX * _gridSizeZ, Allocator.Persistent);


            for (int z = 0; z < _gridSizeZ; z++)
            {
                for (int x = 0; x < _gridSizeX; x++)
                {
                    int index = z * _gridSizeX + x;
                    _fogGrid[index] = new FogCellComponent
                    {
                        GridPosition = new int2(x, z),
                        VisibilityMask = 0,
                        ExploredMask = 0,
                        LastUpdateTime = 0
                    };
                }
            }

            // Create fog settings entity

            var settingsEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(settingsEntity, new FogSettingsComponent
            {
                FogEnabled = true,
                UpdateInterval = 0.1f,
                FadeSpeed = 2f,
                MaxPlayers = 8
            });


            EntityManager.AddComponentData(settingsEntity, new FogGridComponent
            {
                GridSizeX = _gridSizeX,
                GridSizeZ = _gridSizeZ,
                CellSize = _cellSize,
                GridOrigin = _gridOrigin
            });


            _initialized = true;
            Debug.Log($"[FOW] Initialized fog grid: {_gridSizeX}x{_gridSizeZ} cells");
        }


        protected override void OnUpdate()
        {
            if (!_initialized || !GameSettings.FogOfWarEnabled) 
                return;

            float currentTime = (float)SystemAPI.Time.ElapsedTime;

            // Clear visibility (but keep explored)
            for (int i = 0; i < _fogGrid.Length; i++)
            {
                var cell = _fogGrid[i];
                cell.VisibilityMask = 0;
                cell.LastUpdateTime = currentTime;
                _fogGrid[i] = cell;
            }

            // Update visibility from revealers
            Entities
                .WithAll<FogRevealerComponent>()
                .WithoutBurst() // <- important to allow capturing 'this'
                .ForEach((in FogRevealerComponent revealer, in Core.Components.PositionComponent position) =>
                {
                    if (!revealer.IsActive) 
                        return;

                    UpdateVisibilityAroundPoint(position.Position, revealer.RevealRadius, revealer.PlayerId);
                })
                .Run();
        }



        private void UpdateVisibilityAroundPoint(float3 worldPos, float radius, int playerId)
        {
            // Convert world position to grid coordinates
            float3 localPos = worldPos - _gridOrigin;
            int centerX = (int)(localPos.x / _cellSize);
            int centerZ = (int)(localPos.z / _cellSize);


            int cellRadius = (int)math.ceil(radius / _cellSize);
            byte playerBit = (byte)(1 << playerId);


            for (int z = -cellRadius; z <= cellRadius; z++)
            {
                for (int x = -cellRadius; x <= cellRadius; x++)
                {
                    int gridX = centerX + x;
                    int gridZ = centerZ + z;


                    if (gridX < 0 || gridX >= _gridSizeX || gridZ < 0 || gridZ >= _gridSizeZ)
                        continue;


                    float3 cellWorldPos = _gridOrigin + new float3(gridX * _cellSize, 0, gridZ * _cellSize);
                    float distance = math.distance(worldPos, cellWorldPos);


                    if (distance <= radius)
                    {
                        int index = gridZ * _gridSizeX + gridX;
                        var cell = _fogGrid[index];
                        cell.VisibilityMask |= playerBit;
                        cell.ExploredMask |= playerBit;
                        _fogGrid[index] = cell;
                    }
                }
            }
        }


        public bool IsPositionVisible(float3 worldPos, int playerId)
        {
            if (!_initialized || !GameSettings.FogOfWarEnabled) return true;


            float3 localPos = worldPos - _gridOrigin;
            int gridX = (int)(localPos.x / _cellSize);
            int gridZ = (int)(localPos.z / _cellSize);


            if (gridX < 0 || gridX >= _gridSizeX || gridZ < 0 || gridZ >= _gridSizeZ)
                return false;


            int index = gridZ * _gridSizeX + gridX;
            byte playerBit = (byte)(1 << playerId);


            return (_fogGrid[index].VisibilityMask & playerBit) != 0;
        }


        public bool IsPositionExplored(float3 worldPos, int playerId)
        {
            if (!_initialized || !GameSettings.FogOfWarEnabled) return true;


            float3 localPos = worldPos - _gridOrigin;
            int gridX = (int)(localPos.x / _cellSize);
            int gridZ = (int)(localPos.z / _cellSize);


            if (gridX < 0 || gridX >= _gridSizeX || gridZ < 0 || gridZ >= _gridSizeZ)
                return false;


            int index = gridZ * _gridSizeX + gridX;
            byte playerBit = (byte)(1 << playerId);


            return (_fogGrid[index].ExploredMask & playerBit) != 0;
        }


        protected override void OnDestroy()
        {
            if (_fogGrid.IsCreated)
                _fogGrid.Dispose();
        }
    }


    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(FogUpdateSystem))]
    public partial class FogRenderSystem : SystemBase
    {
        private Texture2D _fogTexture;
        private Material _fogMaterial;
        private GameObject _fogPlane;


        protected override void OnCreate()
        {
            RequireForUpdate<FogSettingsComponent>();
        }


        protected override void OnUpdate()
        {
            if (!GameSettings.FogOfWarEnabled) return;


            if (_fogTexture == null)
            {
                CreateFogVisuals();
            }


            UpdateFogTexture();
        }


        private void CreateFogVisuals()
        {
            int textureSize = GameSettings.FogGridSize;
            _fogTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            _fogTexture.filterMode = FilterMode.Bilinear;

            // Create fog plane

            _fogPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _fogPlane.name = "FogOfWar";
            _fogPlane.transform.position = new Vector3(0, 10, 0);
            _fogPlane.transform.localScale = new Vector3(
                GameSettings.MapHalfSize * 0.2f,
                1,
                GameSettings.MapHalfSize * 0.2f
            );

            // Create fog material

            _fogMaterial = new Material(Shader.Find("Unlit/Transparent"));
            _fogMaterial.mainTexture = _fogTexture;
            _fogPlane.GetComponent<Renderer>().material = _fogMaterial;
        }


        private void UpdateFogTexture()
        {
            // This would update the fog texture based on visibility data
            // For now, just a placeholder
        }
    }
}