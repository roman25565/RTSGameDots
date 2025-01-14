using Unity.Entities;
using UnityEngine;

public class SquadAuthoring : MonoBehaviour
{
    public int maxHealth;
    public float attackRange;
    public float attackSpeed3;
    public float attackDamage;
    public float startSpeed;
    public float maxSpeed;
    public float acceleration;
}

public class SquadBaker : Baker<SquadAuthoring>
{
    public override void Bake(SquadAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        
        AddComponent(entity, new SquadDirectionOfView());
        AddComponent(entity, new SquadSpeedMaxAcceleration{maxSpeed = authoring.startSpeed, acceleration = authoring.acceleration});
        AddComponent(entity, new SquadStartSpeed{startSpeed = authoring.maxSpeed});
        AddComponent(entity, new SquadLastPlayerOrder());
        AddComponent(entity, new SquadIndependentState{type = -21});
        AddComponent(entity, new SquadMaxHealth{maxHealth = authoring.maxHealth});
        AddComponent(entity, new SquadExperience());
        AddComponent(entity, new SquadDamage{value = authoring.attackDamage});
        AddComponent(entity, new SquadAttackRange{attackRange = authoring.attackRange});
        AddComponent(entity, new SquadAttackCd{value = authoring.attackSpeed3});
        AddComponent(entity, new SquadStartPosition());
        AddComponent(entity, new ReadyForInitializeCommand());
        AddComponent(entity, new SelectedFormationsId{value = 0});
        AddComponent(entity, new ReadySoldiersCount());
    }
}