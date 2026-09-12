using System;
using UnityEngine;

namespace QuestAI.Core
{
    public sealed class AssistantStateController : MonoBehaviour
    {
        public AssistantState CurrentState { get; private set; } = AssistantState.Inactive;

        public event Action<AssistantState> StateChanged;
        public event Action ActivationRequested;
        public event Action DeactivationRequested;

        public void Activate()
        {
            if (CurrentState != AssistantState.Inactive && CurrentState != AssistantState.Error)
                return;

            SetState(AssistantState.Connecting);
            ActivationRequested?.Invoke();
        }

        public void Deactivate()
        {
            DeactivationRequested?.Invoke();
            SetState(AssistantState.Inactive);
        }

        public void SetConnectionState(AssistantState nextState)
        {
            if (nextState == AssistantState.Inactive)
            {
                Deactivate();
                return;
            }

            SetState(nextState);
        }

        private void SetState(AssistantState nextState)
        {
            if (CurrentState == nextState)
                return;

            CurrentState = nextState;
            StateChanged?.Invoke(CurrentState);
        }
    }
}
