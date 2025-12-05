// RTSInputManager.cs
// Core input handler - routes all player commands through CommandRouter
// Part of: Input/

using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using TheWaningBorder.Core;
using TheWaningBorder.Core.Commands;     // ← ADD: for CommandRouter
using EntityWorld = Unity.Entities.World;

namespace TheWaningBorder.Input
{
    /// <summary>
    /// Handles player input and routes commands through CommandRouter.
    /// Works with SelectionSystem for entity selection.
    /// 
    /// Responsibilities:
    /// - Right-click command handling (move, attack, gather, heal)
    /// - Rally point setting
    /// - Formation movement
    /// - Input blocking when UI is active
    /// </summary>
    public class RTSInputManager : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════════════
        // CONFIGURATION
        // ═══════════════════════════════════════════════════════════════════════
        
        [Header("Raycasting")]
        [SerializeField] private LayerMask clickMask = ~0;
        
        [Header("Formation")]
        [SerializeField] private float formationSpacing = 2.0f;
        
        [Header("Debug")]
        [SerializeField] private bool showHelp = true;
        
        // ═══════════════════════════════════════════════════════════════════════
        // STATE
        // ═══════════════════════════════════════════════════════════════════════
        
        private EntityWorld _world;
        private EntityManager _em;
        private bool _rallyMode = false;
        
        /// <summary>
        /// Currently hovered entity (for UI highlighting).
        /// </summary>
        public static Entity CurrentHover { get; private set; }
        
        // ═══════════════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════════════════════
        
        void Awake()
        {
            _world = EntityWorld.DefaultGameObjectInjectionWorld;
            if (_world != null && _world.IsCreated)
                _em = _world.EntityManager;
        }

        void Update()
        {
            if (_world == null || !_world.IsCreated) return;
            
            // Refresh EntityManager if needed
            if (_em.Equals(default(EntityManager)))
                _em = _world.EntityManager;

            // Block input during UI interactions or building placement
            if (ShouldBlockInput())
                return;

            // Handle hotkeys
            HandleHotkeys();
            
            // Update hover state
            UpdateHover();
            
            // Handle right-click commands
            HandleRightClick();
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // INPUT BLOCKING
        // ═══════════════════════════════════════════════════════════════════════
        
        private bool ShouldBlockInput()
        {
            // One-frame suppression (after GUI button clicks)
            if (BuilderCommandPanel.SuppressClicksThisFrame)
            {
                BuilderCommandPanel.SuppressClicksThisFrame = false;
                return true;
            }

            // Block if mouse is over UI panels
            if (EntityActionPanel.IsPointerOver() || EntityInfoPanel.IsPointerOver())
                return true;

            // Block during building placement
            if (BuilderCommandPanel.IsPlacingBuilding)
                return true;

            return false;
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // HOTKEYS
        // ═══════════════════════════════════════════════════════════════════════
        
        private void HandleHotkeys()
        {
            // ESC - Clear selection and exit rally mode
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                SelectionSystem.ClearSelection();
                _rallyMode = false;
            }
            
            // R - Toggle rally point mode
            if (UnityEngine.Input.GetKeyDown(KeyCode.R))
                _rallyMode = !_rallyMode;
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // HOVER DETECTION
        // ═══════════════════════════════════════════════════════════════════════
        
        private void UpdateHover()
        {
            var hovered = RaycastPickEntity();
            CurrentHover = (_em.Exists(hovered)) ? hovered : Entity.Null;
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // RIGHT-CLICK COMMAND HANDLING
        // ═══════════════════════════════════════════════════════════════════════
        
        private void HandleRightClick()
        {
            if (!UnityEngine.Input.GetMouseButtonDown(1)) return;
            
            var selection = SelectionSystem.CurrentSelection;
            if (selection == null || selection.Count == 0) return;

            // Clean dead entities from selection
            SelectionSystem.CleanSelection();

            if (!TryGetClickPoint(out float3 clickWorld)) return;

            // Special mode: setting rally point for selected buildings
            if (_rallyMode && HasSelectedBuildings())
            {
                SetRallyPoints(clickWorld);
                return;
            }

            // Determine target and issue appropriate command
            var target = RaycastPickEntity();
            var targetType = DetermineTargetType(target);
            var capabilities = DetermineCapabilities();

            switch (targetType)
            {
                case TargetType.Enemy:
                    if (capabilities.CanAttack)
                        IssueAttackCommands(target);
                    break;

                case TargetType.FriendlyUnit:
                    if (capabilities.CanHeal)
                        IssueHealCommands(target);
                    else
                        IssueFormationMove(clickWorld);
                    break;

                case TargetType.Resource:
                    if (capabilities.CanGather)
                        IssueGatherCommands(target);
                    else
                        IssueFormationMove(clickWorld);
                    break;

                case TargetType.Ground:
                default:
                    IssueFormationMove(clickWorld);
                    break;
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // COMMAND ISSUANCE
        // ═══════════════════════════════════════════════════════════════════════
        
        private void SetRallyPoints(float3 position)
        {
            foreach (var e in SelectionSystem.CurrentSelection)
            {
                if (!_em.Exists(e)) continue;
                if (!_em.HasComponent<BuildingTag>(e)) continue;

                CommandRouter.SetRallyPoint(e, position, CommandRouter.CommandSource.LocalPlayer);
            }
        }
        
        private void IssueAttackCommands(Entity target)
        {
            foreach (var e in SelectionSystem.CurrentSelection)
            {
                if (!_em.Exists(e)) continue;
                if (_em.HasComponent<BuildingTag>(e)) continue; // Buildings can't attack-move

                CommandRouter.IssueAttack(e, target, CommandRouter.CommandSource.LocalPlayer);
            }
        }
        
        private void IssueHealCommands(Entity target)
        {
            foreach (var e in SelectionSystem.CurrentSelection)
            {
                if (!_em.Exists(e)) continue;
                if (!CanHeal(e)) continue;

                CommandRouter.IssueHeal(e, target, CommandRouter.CommandSource.LocalPlayer);
            }
        }
        
        private void IssueGatherCommands(Entity resourceNode)
        {
            Entity depositLocation = FindNearestGatherersHut();
            
            foreach (var e in SelectionSystem.CurrentSelection)
            {
                if (!_em.Exists(e)) continue;
                if (!_em.HasComponent<TheWaningBorder.Humans.MinerTag>(e)) continue;

                CommandRouter.IssueGather(e, resourceNode, depositLocation, CommandRouter.CommandSource.LocalPlayer);
            }
        }
        
        private void IssueFormationMove(float3 clickWorld)
        {
            var selection = SelectionSystem.CurrentSelection;
            
            // Count movable units
            int count = 0;
            foreach (var e in selection)
            {
                if (_em.Exists(e) && !_em.HasComponent<BuildingTag>(e))
                    count++;
            }

            if (count == 0) return;

            // Calculate formation grid
            int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt((float)count / cols);

            // Get camera-relative directions
            var cam = Camera.main;
            Vector3 camForward = cam 
                ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized 
                : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, camForward).normalized;
            
            float3 forward = new float3(camForward.x, camForward.y, camForward.z);
            float3 rightF3 = new float3(right.x, right.y, right.z);

            // Top-left of formation
            float3 topLeft = clickWorld 
                - rightF3 * ((cols - 1) * formationSpacing * 0.5f) 
                + forward * ((rows - 1) * formationSpacing * 0.5f);

            // Assign positions
            int idx = 0;
            foreach (var e in selection)
            {
                if (!_em.Exists(e) || _em.HasComponent<BuildingTag>(e))
                    continue;

                int row = idx / cols;
                int col = idx % cols;
                
                float3 slot = topLeft + rightF3 * (col * formationSpacing) - forward * (row * formationSpacing);
                
                CommandRouter.IssueMove(e, slot, CommandRouter.CommandSource.LocalPlayer);
                idx++;
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // TARGET TYPE DETECTION
        // ═══════════════════════════════════════════════════════════════════════
        
        private enum TargetType { Ground, Enemy, FriendlyUnit, Resource }

        private TargetType DetermineTargetType(Entity target)
        {
            if (target == Entity.Null || !_em.Exists(target))
                return TargetType.Ground;

            // Check if it's a resource node
            if (_em.HasComponent<TheWaningBorder.AI.IronMineTag>(target))
                return TargetType.Resource;

            // Check faction
            if (!_em.HasComponent<FactionTag>(target))
                return TargetType.Ground;

            var targetFaction = _em.GetComponentData<FactionTag>(target).Value;
            
            if (targetFaction != GameSettings.LocalPlayerFaction)
                return TargetType.Enemy;

            if (_em.HasComponent<UnitTag>(target))
                return TargetType.FriendlyUnit;

            return TargetType.Ground;
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // CAPABILITY DETECTION
        // ═══════════════════════════════════════════════════════════════════════
        
        private struct UnitCapabilities
        {
            public bool CanAttack;
            public bool CanGather;
            public bool CanHeal;
        }

        private UnitCapabilities DetermineCapabilities()
        {
            var caps = new UnitCapabilities();
            
            foreach (var e in SelectionSystem.CurrentSelection)
            {
                if (!_em.Exists(e)) continue;

                // Can attack if has Damage component
                if (_em.HasComponent<Damage>(e))
                    caps.CanAttack = true;

                // Can gather if is a miner
                if (_em.HasComponent<TheWaningBorder.Humans.MinerTag>(e))
                    caps.CanGather = true;

                // Can heal if has heal capability (Litharch, etc.)
                if (CanHeal(e))
                    caps.CanHeal = true;
            }

            return caps;
        }

        private bool CanHeal(Entity e)
        {
            // Check for healer tag or component
            // Litharch units can heal
            return _em.HasComponent<LitharchTag>(e);
        }
        
        private bool HasSelectedBuildings()
        {
            foreach (var e in SelectionSystem.CurrentSelection)
            {
                if (_em.Exists(e) && _em.HasComponent<BuildingTag>(e))
                    return true;
            }
            return false;
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // UTILITY METHODS
        // ═══════════════════════════════════════════════════════════════════════
        
        private Entity FindNearestGatherersHut()
        {
            Entity nearest = Entity.Null;
            float nearestDist = float.MaxValue;

            // Get average position of selected miners
            float3 avgPos = float3.zero;
            int count = 0;
            foreach (var e in SelectionSystem.CurrentSelection)
            {
                if (_em.Exists(e) && _em.HasComponent<LocalTransform>(e))
                {
                    avgPos += _em.GetComponentData<LocalTransform>(e).Position;
                    count++;
                }
            }
            if (count > 0) avgPos /= count;

            // Find nearest gatherer's hut
            var query = _em.CreateEntityQuery(typeof(GathererHutTag), typeof(LocalTransform), typeof(FactionTag));
            var ents = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                if (!_em.Exists(e)) continue;
                if (_em.GetComponentData<FactionTag>(e).Value != GameSettings.LocalPlayerFaction) continue;

                var pos = _em.GetComponentData<LocalTransform>(e).Position;
                float dist = math.distance(avgPos, pos);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = e;
                }
            }

            ents.Dispose();
            return nearest;
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // RAYCASTING
        // ═══════════════════════════════════════════════════════════════════════
        
        private Entity RaycastPickEntity()
        {
            var cam = Camera.main;
            if (!cam) return Entity.Null;

            Ray ray = cam.ScreenPointToRay(UnityEngine.Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, clickMask))
            {
                var go = hit.collider.gameObject;
                
                // Check for EntityReference component
                var link = go.GetComponent<EntityReference>();
                if (link != null && _em.Exists(link.Entity))
                    return link.Entity;
                
                // Check parent
                if (go.transform.parent != null)
                {
                    link = go.transform.parent.GetComponent<EntityReference>();
                    if (link != null && _em.Exists(link.Entity))
                        return link.Entity;
                }
            }
            return Entity.Null;
        }

        private bool TryGetClickPoint(out float3 point)
        {
            point = float3.zero;
            var cam = Camera.main;
            if (!cam) return false;

            Ray ray = cam.ScreenPointToRay(UnityEngine.Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, clickMask))
            {
                point = hit.point;
                return true;
            }
            return false;
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // DEBUG GUI
        // ═══════════════════════════════════════════════════════════════════════

        void OnGUI()
        {
            if (!showHelp) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("Controls:");
            GUILayout.Label("Left-click: Select unit");
            GUILayout.Label("Left-drag: Box select");
            GUILayout.Label("Right-click: Move/Attack/Gather");
            GUILayout.Label("R: Toggle rally point mode");
            GUILayout.Label("ESC: Clear selection");
            
            if (GameSettings.IsMultiplayer)
            {
                GUILayout.Label($"Faction: {GameSettings.LocalPlayerFaction}");
                GUILayout.Label("Multiplayer: Active");
            }
            GUILayout.EndArea();

            if (_rallyMode)
            {
                GUI.Label(new Rect(Screen.width / 2 - 50, 10, 100, 20), "RALLY MODE");
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════
    // HELPER COMPONENT
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Links a GameObject to an ECS Entity.
    /// Attach to visual representations of entities.
    /// </summary>
    public class EntityReference : MonoBehaviour
    {
        public Entity Entity;
    }
}