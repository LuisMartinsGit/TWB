using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TheWaningBorder.Player.Selection;
using TheWaningBorder.Core.GameManager;
using TheWaningBorder.Units.Base;

namespace TheWaningBorder.Player.PlayerController
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PlayerControllerSystem : SystemBase
    {
        private Camera _mainCamera;
        private int _localPlayerId = 0;
        private EntityCommandBufferSystem _ecbSystem;
        
        protected override void OnCreate()
        {
            _mainCamera = Camera.main;
            _ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }
        
        protected override void OnUpdate()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }
            
            // Handle input
            HandleMouseInput();
            HandleKeyboardInput();
        }
        
        private void HandleMouseInput()
        {
            if (Input.GetMouseButtonDown(0)) // Left click
            {
                HandleLeftClick();
            }
            
            if (Input.GetMouseButtonDown(1)) // Right click
            {
                HandleRightClick();
            }
        }
        
        private void HandleKeyboardInput()
        {
            // Camera movement
            float moveSpeed = 10f * SystemAPI.Time.DeltaTime;
            Vector3 movement = Vector3.zero;
            
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                movement.z += moveSpeed;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                movement.z -= moveSpeed;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                movement.x -= moveSpeed;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                movement.x += moveSpeed;
            
            if (movement != Vector3.zero)
            {
                _mainCamera.transform.position += movement;
            }
        }
        
        private void HandleLeftClick()
        {
            if (Input.GetKey(KeyCode.LeftShift))
            {
                // Add to selection
                AddToSelection();
            }
            else
            {
                // New selection
                ClearSelection();
                SelectUnit();
            }
        }
        
        private void HandleRightClick()
        {
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                IssueCommand(hit.point);
            }
        }
        
        private void ClearSelection()
        {
            var entityManager = EntityManager;
            var ecb = _ecbSystem.CreateCommandBuffer();
            
            Entities
                .WithAll<SelectedTag>()
                .WithoutBurst() // Required for EntityManager access
                .ForEach((Entity entity, ref SelectableComponent selectable) =>
                {
                    selectable.IsSelected = false;
                    ecb.RemoveComponent<SelectedTag>(entity);
                })
                .Run();
        }
        
        private void SelectUnit()
        {
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return;
            
            float3 clickPos = new float3(hit.point.x, hit.point.y, hit.point.z);
            Entity closestEntity = Entity.Null;
            float closestDistance = float.MaxValue;
            var localPlayerId = _localPlayerId;
            var entityManager = EntityManager;
            var ecb = _ecbSystem.CreateCommandBuffer();
            
            Entities
                .WithoutBurst() // Required for local variables
                .ForEach((Entity entity, ref SelectableComponent selectable, 
                         in PositionComponent position, in OwnerComponent owner) =>
                {
                    if (owner.PlayerId != localPlayerId)
                        return;
                    
                    float distance = math.distance(clickPos, position.Position);
                    if (distance < selectable.SelectionRadius && distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestEntity = entity;
                    }
                })
                .Run();
            
            // Apply new selection
            if (closestEntity != Entity.Null)
            {
                ecb.AddComponent<SelectedTag>(closestEntity);
                
                var selectable = entityManager.GetComponentData<SelectableComponent>(closestEntity);
                selectable.IsSelected = true;
                entityManager.SetComponentData(closestEntity, selectable);
            }
        }
        
        private void AddToSelection()
        {
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return;
            
            float3 clickPos = new float3(hit.point.x, hit.point.y, hit.point.z);
            Entity closestEntity = Entity.Null;
            float closestDistance = float.MaxValue;
            var localPlayerId = _localPlayerId;
            var entityManager = EntityManager;
            var ecb = _ecbSystem.CreateCommandBuffer();
            
            Entities
                .WithNone<SelectedTag>()
                .WithoutBurst()
                .ForEach((Entity entity, ref SelectableComponent selectable, 
                         in PositionComponent position, in OwnerComponent owner) =>
                {
                    if (owner.PlayerId != localPlayerId)
                        return;
                    
                    float distance = math.distance(clickPos, position.Position);
                    if (distance < selectable.SelectionRadius && distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestEntity = entity;
                    }
                })
                .Run();
            
            if (closestEntity != Entity.Null)
            {
                ecb.AddComponent<SelectedTag>(closestEntity);
                
                var selectable = entityManager.GetComponentData<SelectableComponent>(closestEntity);
                selectable.IsSelected = true;
                entityManager.SetComponentData(closestEntity, selectable);
            }
        }
        
        private void IssueCommand(Vector3 worldPos)
        {
            float3 targetPos = new float3(worldPos.x, worldPos.y, worldPos.z);
            
            Entities
                .WithAll<SelectedTag>()
                .ForEach((Entity entity, ref MovementComponent movement) =>
                {
                    movement.Destination = targetPos;
                    movement.IsMoving = true;
                })
                .Schedule();
        }
    }
}
