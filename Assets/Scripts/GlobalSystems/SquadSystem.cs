using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public partial struct SquadSystem : ISystem
{
   
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (movementSpeed,entity)
                 in SystemAPI.Query<RefRW<MovementSpeed>>().WithEntityAccess().WithAll<Parent>())  //пришвидшення
        {
            var mainSquad = SystemAPI.GetComponent<Parent>(entity).Value;
            if (!SystemAPI.HasComponent<SquadSpeedMaxAcceleration>(mainSquad))
                continue;
            
            var speedMaxC = SystemAPI.GetComponent<SquadSpeedMaxAcceleration>(mainSquad);
            
            if (movementSpeed.ValueRO.value < speedMaxC.maxSpeed)
            {
                movementSpeed.ValueRW.value += Time.deltaTime * speedMaxC.acceleration;
            }
            
            //need
        }
        
        foreach (var (lastPlayerOrder, squadIndState, children, startPosition, entity)  //Setup
                 in SystemAPI.Query<RefRO<SquadLastPlayerOrder>,RefRW<SquadIndependentState>, DynamicBuffer<Child>, RefRO<SquadStartPosition>>().WithEntityAccess())
        {
            //NEED normal squad tools
            if (squadIndState.ValueRO.type != -21)
                continue;
            squadIndState.ValueRW.type = 1;
            foreach (var child in children)
            {
                SystemAPI.SetComponent(child.Value, new StateID{value = 1});
                SystemAPI.SetComponent(child.Value, new TargetPosition{value = lastPlayerOrder.ValueRO.targetPoz});
                SystemAPI.SetComponent(child.Value, new LocalTransform{Position = startPosition.ValueRO.value,Scale = 1,
                    Rotation = new quaternion(0, 0, 0, 1)});
            }
        }

        foreach (var (warriorTarget, localTransform, targetPosition, parent,stateID) in SystemAPI //start Attack
                     .Query<RefRW<WarriorTarget>, RefRO<LocalTransform>, RefRW<TargetPosition>,Parent,RefRW<StateID>>())
        {
            if (warriorTarget.ValueRO.targetEntity == Entity.Null)
                continue;
            var attackRange = SystemAPI.GetComponent<SquadAttackRange>(parent.Value).attackRange;
            var targetEntityPosition = SystemAPI.GetComponent<LocalTransform>(warriorTarget.ValueRO.targetEntity).Position;
            if (math.distance(localTransform.ValueRO.Position, targetEntityPosition) < attackRange)
            {
                stateID.ValueRW.value = 3;
            }
            else
            {
                stateID.ValueRW.value = 1;
                targetPosition.ValueRW.value = localTransform.ValueRO.Position +
                                               (math.normalize(targetEntityPosition - localTransform.ValueRO.Position) * attackRange);//need testing
            }
        }

        foreach (var (stateID, parent, warriorAttackCd, warriorTarget) //Attack
                 in SystemAPI.Query<RefRW<StateID>, RefRO<Parent>, RefRW<WarriorAttackCd>,RefRW<WarriorTarget>>())
        {
            if (stateID.ValueRO.value != 3)
                continue;
            var damage = SystemAPI.GetComponent<SquadDamage>(parent.ValueRO.Value).damage;
            if (warriorAttackCd.ValueRO.value == 0)
            {
                warriorAttackCd.ValueRW.value = 0.00000001f;
                var targetHealth = SystemAPI.GetComponent<WarriorHealth>(warriorTarget.ValueRO.targetEntity).health;
                SystemAPI.SetComponent(warriorTarget.ValueRO.targetEntity, new WarriorHealth{health = targetHealth - damage});
            }

            stateID.ValueRW.value = 1;
        }


        foreach (var (stateID, parent, warriorAttackCd)  //Attack Cd End
                 in SystemAPI.Query<RefRW<StateID>, RefRO<Parent>, RefRW<WarriorAttackCd>>())
        {
            if(warriorAttackCd.ValueRO.value <= 0f)
                continue;
            if (warriorAttackCd.ValueRO.value >= SystemAPI.GetComponent<SquadAttackCd>(parent.ValueRO.Value).value)
            {
                warriorAttackCd.ValueRW.value = 0;
            }
        }
        
        var job = new AttackCdJob
        {
            deltaTime = SystemAPI.Time.DeltaTime,
        };
        job.Run();
    }
}

    public partial struct AttackCdJob : IJobEntity
    {
        public float deltaTime;
        public void Execute(WarriorAttackCd warriorAttackCd)
        {
            if (warriorAttackCd.value > 0f) 
                warriorAttackCd.value += deltaTime;
        }
    }
