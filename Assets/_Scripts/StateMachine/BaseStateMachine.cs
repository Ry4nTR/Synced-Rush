using System;
using System.Collections.Generic;
using UnityEngine;

namespace SyncedRush.Generics
{
    /// <summary>
    /// State Machine generica ed ereditabile. Overkill? Penso proprio di si, ma me ne sono accorto troppo tardi.<br/>
    /// Il valore di default di TStateEnum (cioè l'elemento 0) indica "Nessuno stato", e serve solo a <see cref="QueuedStateEnum"/>.
    /// </summary>
    public abstract class BaseStateMachine<TStateEnum, TState> : MonoBehaviour
        where TStateEnum : Enum
        where TState : BaseState<TStateEnum>
    {

        [SerializeField] private TStateEnum _startingState;
        [SerializeField] private Dictionary<TStateEnum, TState> _states = new();

        private bool _forcedEnterRequested = false;

        public TState CurrentState { get; private set; }
        public TStateEnum CurrentStateEnum { get; private set; }
        /// <summary> Serve agli stati in uscita per capire qual'è il prossimo stato </summary>
        public TStateEnum QueuedStateEnum { get; private set; }
        public TStateEnum PreviousStateEnum { get; private set; }
        public bool IsInitialized { get; private set; }
        public bool LockState { get; set; } = false;

        protected void OnDestroy()
        {
            UnsubscribeEvents();
        }

        public void Initialize(Dictionary<TStateEnum, TState> allStates, TStateEnum initialState)
        {
            if (IsInitialized)
            {
                Debug.LogError("State machine already initialized!");
                return;
            }
            _states = allStates;

            SubscribeEvents();

            IsInitialized = true;

            PreviousStateEnum = default;

            ChangeState(initialState);
        }

        public void ChangeState(TStateEnum state,
            bool forceEnter = false,
            bool ignoreEnter = false,
            bool ignoreExit = false)
        {
            if (LockState)
                return;

            if (_states.TryGetValue(state, out TState newState))
            {
                QueuedStateEnum = state;

                bool sameState = CurrentState == newState;
                if (!sameState)
                {
                    PreviousStateEnum = CurrentStateEnum;
                    if (!ignoreEnter)
                        CurrentState?.ExitState();
                }

                QueuedStateEnum = default;
                CurrentStateEnum = state;
                CurrentState = newState;

                if ((forceEnter || !sameState) && !ignoreEnter)
                {
                    CurrentState.EnterState();
                    _forcedEnterRequested = false;
                }
                //Debug.Log(CurrentState.ToString()); //TODO da rimuovere quando non serve più
            }
            else
                Debug.LogError("Stato non trovato!");
        }

        public void ProcessUpdate()
        {
            TStateEnum newState = CurrentState.ProcessUpdate();
            if (newState != null && !newState.Equals(default(TStateEnum)))
                ChangeState(newState, _forcedEnterRequested);
        }

        public void RequestForcedEnter()
        {
            _forcedEnterRequested = true;
        }

        protected void SubscribeEvents()
        {
            foreach (TState state in _states.Values)
            {
                state.RequestForcedEnter += RequestForcedEnter;
            }
        }

        protected void UnsubscribeEvents()
        {
            foreach (TState state in _states.Values)
            {
                state.RequestForcedEnter -= RequestForcedEnter;
            }
        }
    }
}