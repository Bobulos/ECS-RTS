using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class TurretLookAuthoring : MonoBehaviour
{
    public float rotSpeed;
}

class TurretLookAuthoringBaker : Baker<TurretLookAuthoring>
{
    public override void Bake(TurretLookAuthoring authoring)
    {
        GetEntity(authoring, TransformUsageFlags.Dynamic);
        AddComponent(new TurretLook { Speed = authoring.rotSpeed});
    }
}
public struct TurretLook : IComponentData
{
    public float Speed;
    //public float3 Target;
}