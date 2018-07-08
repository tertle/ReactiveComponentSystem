using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Entities;

namespace BovineLabs.Toolkit.ECS
{
    public abstract class ReactiveComponentSystem : ComponentSystem
    {
        private static ModuleBuilder _moduleBuilder;
        private readonly Dictionary<Type, IReactiveGroup> _reactiveGroups = new Dictionary<Type, IReactiveGroup>();

        private static ModuleBuilder ModuleBuilder
        {
            get
            {
                if (_moduleBuilder == null)
                {
                    var assemblyName = new AssemblyName("BovineLabsReactive");
                    var assemblyBuilder =
                        AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
                    _moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name, assemblyName.Name + ".dll");
                }

                return _moduleBuilder;
            }
        }

        protected ComponentGroup GetReactiveAddGroup(Type type)
        {
            if (!_reactiveGroups.TryGetValue(type, out var reactiveGroup))
                reactiveGroup = _reactiveGroups[type] = CreateReactiveGroup(type);

            return reactiveGroup.AddGroup;
        }

        protected ComponentGroup GetReactiveRemoveGroup(Type type)
        {
            if (!_reactiveGroups.TryGetValue(type, out var reactiveGroup))
                reactiveGroup = _reactiveGroups[type] = CreateReactiveGroup(type);

            return reactiveGroup.RemoveGroup;
        }

        private IReactiveGroup CreateReactiveGroup(Type reactiveComponent)
        {
            var reactiveComponentStateType = CreateReactiveTypeState(reactiveComponent);

            var addGroup = GetComponentGroup(reactiveComponent, ComponentType.Subtractive(reactiveComponentStateType));
            var removeGroup =
                GetComponentGroup(ComponentType.Subtractive(reactiveComponent), reactiveComponentStateType);

            var reactiveComponentState = Activator.CreateInstance(reactiveComponentStateType);
            var makeme = typeof(ReactiveGroup<>).MakeGenericType(reactiveComponentStateType);
            return (IReactiveGroup) Activator.CreateInstance(makeme, reactiveComponentState, addGroup, removeGroup);
        }

        private Type CreateReactiveTypeState(Type reactiveComponent)
        {
            var typeName = $"{GetType().Name}_{reactiveComponent.Name}";
            var typeBuilder = ModuleBuilder.DefineType(typeName, TypeAttributes.Public, typeof(ValueType));
            typeBuilder.AddInterfaceImplementation(typeof(ISystemStateComponentData));
            return typeBuilder.CreateType();
        }

        protected sealed override void OnUpdate()
        {
            OnReactiveUpdate();

            foreach (var kvp in _reactiveGroups)
            {
                var group = kvp.Value;

                var addGroupEntities = group.AddGroup.GetEntityArray();
                for (var index = 0; index < addGroupEntities.Length; index++)
                    group.AddComponent(PostUpdateCommands, addGroupEntities[index]);

                var removeGroupEntities = group.RemoveGroup.GetEntityArray();
                for (var index = 0; index < removeGroupEntities.Length; index++)
                    group.RemoveComponent(PostUpdateCommands, removeGroupEntities[index]);
            }
        }

        protected abstract void OnReactiveUpdate();

        private interface IReactiveGroup
        {
            ComponentGroup AddGroup { get; }
            ComponentGroup RemoveGroup { get; }
            void AddComponent(EntityCommandBuffer entityCommandBuffer, Entity entity);
            void RemoveComponent(EntityCommandBuffer entityCommandBuffer, Entity entity);
        }

        private class ReactiveGroup<T> : IReactiveGroup where T : struct, ISystemStateComponentData
        {
            private readonly T _stateComponent;

            public ReactiveGroup(T stateComponent, ComponentGroup addGroup, ComponentGroup removeGroup)
            {
                _stateComponent = stateComponent;
                AddGroup = addGroup;
                RemoveGroup = removeGroup;
            }

            public ComponentGroup AddGroup { get; }
            public ComponentGroup RemoveGroup { get; }

            public void AddComponent(EntityCommandBuffer entityCommandBuffer, Entity entity)
            {
                entityCommandBuffer.AddComponent(entity, _stateComponent);
            }

            public void RemoveComponent(EntityCommandBuffer entityCommandBuffer, Entity entity)
            {
                entityCommandBuffer.RemoveComponent<T>(entity);
            }
        }
    }
}