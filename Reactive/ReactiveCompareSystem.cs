using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace BovineLabs.Toolkit.Reactive
{
    public class ReactiveUpdateBarrier : BarrierSystem
    {
        
    }
    
    public class ReactiveCompareSystem<T, TC, TN> : JobComponentSystem 
        where T : struct, IComponentData
        where TC : struct, IReactiveCompare<T>, IComponentData
        where TN : struct, IComponentData
    {
        [Inject] private ReactiveUpdateBarrier _barrier;
        
        private ComponentGroup _group;

        protected override void OnCreateManager(int capacity)
        {
            _group = GetComponentGroup(ComponentType.ReadOnly<T>(), ComponentType.ReadOnly<TC>());//, ComponentType.Subtractive<ReactiveChanged>());
        }

        // [BurstCompile] Burst does not support EntityCommandBuffer yet
        private struct CompareJob : IJobParallelFor
        {
            public EntityCommandBuffer.Concurrent CommandBuffer;

            [ReadOnly] public EntityArray Entities;
            [ReadOnly] public ComponentDataArray<T> Components;
            [ReadOnly] public ComponentDataArray<TC> Previous;
            
            public void Execute(int index)
            {
                // Hasn't changed
                if (Previous[index].Equals(Components[index]))
                    return;
                
                var previous = Previous[index];
                previous.Set(Components[index]);
                CommandBuffer.SetComponent(Entities[index], previous);
                CommandBuffer.AddComponent(Entities[index], new TN());
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            //Debug.Log($"ReactiveCompareSystem {typeof(T)}");
            
            var compareJob = new CompareJob
            {
                Entities = _group.GetEntityArray(),
                Components = _group.GetComponentDataArray<T>(),
                Previous = _group.GetComponentDataArray<TC>(),
                CommandBuffer = _barrier.CreateCommandBuffer()
            };
            
            return compareJob.Schedule(_group.CalculateLength(), 64, inputDeps);
        }
    }

    public class RemoveReactiveBarrier : BarrierSystem
    {
        
    }
    
    public class RemoveReactiveSystem<T> : JobComponentSystem
        where T : struct, IComponentData
    {
        [Inject] private RemoveReactiveBarrier _barrier;
        private ComponentGroup _group;

        private struct RemoveReactiveChangedJob : IJobParallelFor
        {
            [ReadOnly] public EntityArray Entities; 
            public EntityCommandBuffer.Concurrent CommandBuffer;      
            
            public void Execute(int index)
            {
                CommandBuffer.RemoveComponent<T>(Entities[index]);
            }
        }
        
        protected override void OnCreateManager(int capacity)
        {
            _group = GetComponentGroup(ComponentType.ReadOnly<T>());
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var entities = _group.GetEntityArray();
            return new RemoveReactiveChangedJob
            {
                CommandBuffer = _barrier.CreateCommandBuffer(),
                Entities = entities
            }.Schedule(entities.Length, 64, inputDeps);
        }
    }
}