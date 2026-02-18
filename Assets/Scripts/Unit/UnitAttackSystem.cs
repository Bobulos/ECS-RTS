using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateAfter(typeof(UnitStateSystem)),UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
partial struct UnitAttackSystem : ISystem
{

    private void OnCreate(ref SystemState state)
    {
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecbSys = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        var job = new AttackJob
        {
            EntityInfo = state.GetEntityStorageInfoLookup(),
            Ecb = ecbSys.CreateCommandBuffer(state.WorldUnmanaged),
            El = (float)SystemAPI.Time.ElapsedTime,
            Hp = SystemAPI.GetComponentLookup<UnitHP>(true),
        };
        state.Dependency = job.Schedule(state.Dependency);
    }
}
[BurstCompile]
public partial struct AttackJob : IJobEntity
{
    public EntityCommandBuffer Ecb;
    [ReadOnly] public float El;
    [ReadOnly] public EntityStorageInfoLookup EntityInfo;
    [ReadOnly] public ComponentLookup<UnitHP> Hp;
    [BurstCompile]
    public void Execute(
        //RefRO<LocalTransform> transform,
        RefRO<UnitState> state,
        RefRW<UnitAttack> atk, 
        RefRO<UnitTarget> targ)
    {
        var t = targ.ValueRO.Targ;
        //no targ
        if (!IsEntityValid(t)) return;
        
        if (!Hp.TryGetComponent(t, out var tHp)) return;
        
        // ignore state logic
        if (atk.ValueRO.ShootWhileMoveing)
        {
            //UnityEngine.Debug.Log("Attacks");
            // in range and in time
            if (targ.ValueRO.DistSq <= atk.ValueRO.RangeSq &&
            atk.ValueRO.Last + atk.ValueRO.Rate <= El)
            {
                atk.ValueRW.Last = El;

                Ecb.SetComponent(t, new
                UnitHP {
                    HP = tHp.HP - atk.ValueRO.Dmg
                });
            }
        } else if (state.ValueRO.State == UnitStates.Attack)
        {
            if (targ.ValueRO.DistSq <= atk.ValueRO.RangeSq &&
            atk.ValueRO.Last + atk.ValueRO.Rate <= El)
            {
                atk.ValueRW.Last = El;

                Ecb.SetComponent(t, new
                UnitHP {
                    HP = tHp.HP - atk.ValueRO.Dmg
                });
            }
        }
    }
    private bool IsEntityValid(Entity e)
    {
        return e != Entity.Null && EntityInfo.Exists(e);
    }
}