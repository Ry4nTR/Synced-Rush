using System;

namespace SyncedRush.Generics
{
    public abstract class BaseState<TStateEnum> where TStateEnum : Enum
    {
        private BaseStateMachine<TStateEnum, BaseState<TStateEnum>> _parentStateMachine;

        protected BaseStateMachine<TStateEnum, BaseState<TStateEnum>> ParentStateMachine => _parentStateMachine;

        public event Action RequestForcedEnter;

        public void SetParentStateMachine(BaseStateMachine<TStateEnum, BaseState<TStateEnum>> parentStateMachine)
        {
            if (_parentStateMachine == null)
                _parentStateMachine = parentStateMachine;
        }

        public virtual void EnterState() { }

        public virtual void ExitState() { }

        // la keyword default indica il primo elemento dell'enum (in questo caso significa "nessuno stato")
        public virtual TStateEnum ProcessUpdate() => default;

        public virtual TStateEnum ProcessFixedUpdate() => default;

        protected void RequestForcedStateEnter()
        {
            RequestForcedEnter?.Invoke();
        }

    }
}