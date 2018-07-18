using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace BovineLabs.Toolkit.Reactive
{
    public abstract class ReactiveJobComponentSystem : JobComponentSystem
    {
        private readonly Dictionary<IReactiveAddRemoveGroup, KeyValuePair<ComponentGroup, ComponentGroup>> _addRemoveGroups =
            new Dictionary<IReactiveAddRemoveGroup, KeyValuePair<ComponentGroup, ComponentGroup>>();

        private readonly Dictionary<IReactiveAddRemoveGroup, IReactiveUpdateGroup> _groupMap =
            new Dictionary<IReactiveAddRemoveGroup, IReactiveUpdateGroup>();
        
        protected ComponentGroup GetReactiveAddGroup(params ComponentType[] componentTypes)
        {
            return GetReactiveAddGroup(componentTypes, new ComponentType[0]);
        }

        protected ComponentGroup GetReactiveRemoveGroup(params ComponentType[] componentTypes)
        {
            return GetReactiveRemoveGroup(componentTypes, new ComponentType[0]);
        }

        protected ComponentGroup GetReactiveAddGroup(ComponentType[] componentTypes, ComponentType[] conditionTypes)
        {
            return GetGroups(componentTypes, conditionTypes).Key;
        }

        protected ComponentGroup GetReactiveRemoveGroup(ComponentType[] componentTypes, ComponentType[] conditionTypes)
        {
            return GetGroups(componentTypes, conditionTypes).Value;
        }

        protected ComponentGroup GetReactiveUpdateGroup(ComponentType componentType)
        {
            var addRemove = ReactiveTypeHelper.GetReactiveAddRemoveGroup(new[] {componentType}, new ComponentType[0]);
            
            if (!_groupMap.TryGetValue(addRemove, out var update))
                update = _groupMap[addRemove] = ReactiveTypeHelper.GetReactiveUpdateGroup(World, addRemove);

            return GetComponentGroup(update.Components); 
        }
                
        private KeyValuePair<ComponentGroup, ComponentGroup> GetGroups(ComponentType[] componentTypes, ComponentType[] conditionTypes)
        {
            var group = ReactiveTypeHelper.GetReactiveAddRemoveGroup(componentTypes, conditionTypes);
            if (!_addRemoveGroups.TryGetValue(@group, out var groups))
            {
                var addGroup = GetComponentGroup(@group.AddComponents);
                var removeGroup = GetComponentGroup(@group.RemoveComponents);

                groups = _addRemoveGroups[@group] =
                    new KeyValuePair<ComponentGroup, ComponentGroup>(addGroup, removeGroup);
            }

            return groups;
        }

        protected sealed override JobHandle OnUpdate(JobHandle inputDeps)
        {
            inputDeps = OnReactiveUpdate(inputDeps);

            var barrierSystem = World.GetExistingManager<ReactiveBarrierSystem>();
            foreach (var groups in _addRemoveGroups)
            {
                var group = groups.Key;

                var addEntities = groups.Value.Key.GetEntityArray();
                var removeEntities = groups.Value.Key.GetEntityArray();
                
                inputDeps = group.CreateAddJob(inputDeps, addEntities, barrierSystem.CreateCommandBuffer());
                inputDeps = group.CreateRemoveJob(inputDeps, removeEntities, barrierSystem.CreateCommandBuffer());

                var isUpdateGroup = _groupMap.TryGetValue(group, out var updateGroup);

                if (isUpdateGroup)
                {
                    inputDeps = updateGroup.CreateAddJob(inputDeps, addEntities, barrierSystem.CreateCommandBuffer());
                    inputDeps = updateGroup.CreateRemoveJob(inputDeps, removeEntities,
                        barrierSystem.CreateCommandBuffer());
                }
            }
            
            //inputDeps.Complete();
            
            return inputDeps;
        }



        protected abstract JobHandle OnReactiveUpdate(JobHandle inputDeps);

        

        
    }
}