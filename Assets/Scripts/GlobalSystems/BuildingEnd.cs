using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public partial class BuildingEnd : SystemBase
{
    protected override void OnUpdate()
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        foreach (var (buildingProgress,constructionTime, buildEntity)
                 in SystemAPI.Query<RefRW<ConstructionProgress>,RefRO<ConstructionTime>>().WithEntityAccess())
        {
            if(constructionTime.ValueRO.value <= buildingProgress.ValueRO.progress)
            {
                ecb.RemoveComponent<ConstructionProgress>(buildEntity);
                
                var a = SystemAPI.GetComponent<LocalTransform>(buildEntity);
                var direction = math.mul(a.Rotation.value, new float3(0f, 0f, 10f));
                SystemAPI.SetComponent(buildEntity, new RallyPointComponent{position = new float3(a.Position + direction)});
                
                var builder = buildingProgress.ValueRO.myBilder;
                SystemAPI.SetComponent(builder, new ArrivalAction{value = -1});
                var builderMesh = SystemAPI.GetBuffer<Child>(builder)[0].Value;
                // Debug.Log(SystemAPI.GetComponent<MaterialMeshInfo>(builderMesh).Material);
                SystemAPI.SetComponent(builderMesh, new MaterialMeshInfo{Mesh = SystemAPI.GetComponent<NormalMesh>(builder).normalMesh,Material = SystemAPI.GetComponent<MaterialMeshInfo>(builderMesh).Material});
            }
        }
        
        Dependency.Complete();

        // Now that the job is completed, you can enact the changes.
        // Note that Playback can only be called on the main thread.
        ecb.Playback(EntityManager);

        // You are responsible for disposing of any ECB you create.
        ecb.Dispose();
    }
}