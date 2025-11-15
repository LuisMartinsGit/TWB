using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TheWaningBorder.Player.Selection;
using TheWaningBorder.Core.Components;

namespace TheWaningBorder.Player.PlayerController
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PlayerControllerSystem : SystemBase
    {
        private Camera _mainCamera;
        private int _localPlayerId = 0;
        
        protected override void OnCreate()
        {
            _mainCamera = Camera.main;
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
            
            // Zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                _mainCamera.transform.position += _mainCamera.transform.forward * scroll * 20f;
            }
        }
        
        private void HandleLeftClick()
        {
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                SelectUnit(hit.point);
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
        
        private void SelectUnit(Vector3 worldPos)
        {
            // Copy these to locals so the lambdas don’t capture 'this'
            var em = EntityManager;
            int localPlayerId = _localPlayerId;

            // Clear previous selection
            Entities
                .WithAll<SelectedTag>()
                .WithStructuralChanges()
                .ForEach((Entity entity, ref Core.GameManager selectable) =>
                {
                    selectable.IsSelected = false;
                    em.RemoveComponent<SelectedTag>(entity);
                })
                .Run();

            // Find closest selectable unit owned by local player
            float3 clickPos = new float3(worldPos.x, worldPos.y, worldPos.z);
            Entity closestEntity = Entity.Null;
            float closestDistance = float.MaxValue;

            Entities
                .ForEach((Entity entity,
                        in Core.GameManager.SelectableComponent selectable,
                        in PositionComponent position,
                        in Core.GameManager.OwnerComponent owner) =>
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
                em.AddComponent<SelectedTag>(closestEntity);

                var selectable = em.GetComponentData<Core.GameManager.SelectableComponent>(closestEntity);
                selectable.IsSelected = true;
                em.SetComponentData(closestEntity, selectable);
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
                }).Run();
        }
    }
}