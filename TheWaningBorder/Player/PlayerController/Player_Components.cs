using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace TheWaningBorder.Player.PlayerController
{
    public struct PlayerInputComponent : IComponentData
    {
        public float2 MousePosition;
        public float2 MouseDelta;
        public bool LeftClick;
        public bool RightClick;
        public bool MiddleClick;
        public bool ShiftHeld;
        public bool CtrlHeld;
        public bool AltHeld;
    }
    
    public struct CameraComponent : IComponentData
    {
        public float3 Position;
        public quaternion Rotation;
        public float Zoom;
        public float MoveSpeed;
        public float RotateSpeed;
        public float ZoomSpeed;
    }
}