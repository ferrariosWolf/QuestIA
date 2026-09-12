using System;
using UnityEngine;

namespace QuestAI.Core
{
    public sealed class AssistantStateController : MonoBehaviour
    {
        public AssistantState CurrentState { get; private set; } = AssistantState.Inactive;

        public event Action<AssistantState> StateChanged;

        public void Activate()
        {
            SetState(AssistantState.Active);
        }

        public void Deactivate()
        {
            SetState(AssistantState.Inactive);
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
