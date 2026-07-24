using Unity.Collections;
using Unity.Mathematics;

internal enum ReleasedBlockInteractionType : byte
{
    Conveyor,
    Clear,
    Suction
}

internal struct ReleasedBlockInteractionRequest
{
    public ReleasedBlockInteractionType Type;
    public float3 BoundsMin;
    public float3 BoundsMax;
    public float3 Origin;
    public quaternion InverseRotation;
    public float3 HalfSize;
    public float3 BoxOffset;
    public float3 Direction;
    public float Speed;
    public float Acceleration;
    public float Force;
    public float MaxVelocity;
    public float ArrivalDamping;
    public float DestroyRadius;
    public float WaypointRadius;
    public float PathLookAhead;
    public float RenderDepth;
    public float DeltaTime;
    public byte AllowSuctionCapture;
    public FixedList512Bytes<float3> SuctionPath;
}
