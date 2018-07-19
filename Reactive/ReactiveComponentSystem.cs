using System.Collections.Generic;
using Unity.Entities;
using Unity.Jobs;

namespace BovineLabs.Toolkit.Reactive
{
    public abstract class ReactiveComponentSystem : ComponentSystem
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
            if (!_addRemoveGroups.TryGetValue(group, out var groups))
            {
                var addGroup = GetComponentGroup(group.AddComponents);
                var removeGroup = GetComponentGroup(group.RemoveComponents);

            groups = _addRemoveGroups[group] =
                    new KeyValuePair<ComponentGroup, ComponentGroup>(addGroup, removeGroup);
            }

            return groups;
        }

        protected sealed override void OnUpdate()
        {
            OnReactiveUpdate();

            foreach (var groups in _addRemoveGroups)
            {
                var group = groups.Key;

                var addEntities = groups.Value.Key.GetEntityArray();
                var removeEntities = groups.Value.Value.GetEntityArray();
                
                group.CreateAddJob(new JobHandle(), addEntities, PostUpdateCommands).Complete();
                group.CreateRemoveJob(new JobHandle(), removeEntities, PostUpdateCommands).Complete();

                var isUpdateGroup = _groupMap.TryGetValue(group, out var updateGroup);

                if (isUpdateGroup)
                {
                    updateGroup.CreateAddJob(new JobHandle(), addEntities, PostUpdateCommands).Complete();
                    updateGroup.CreateRemoveJob(new JobHandle(), removeEntities, PostUpdateCommands).Complete();
                }
            }
        }

        protected abstract void OnReactiveUpdate();

    }
}