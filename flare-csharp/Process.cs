using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace flare_csharp
{
	public sealed class Process<TState, TCommand> where TState : Enum where TCommand : Enum
	{
		public TState CurrentState { get; set; }
		public Dictionary<StateTransition, TState> StateTransitions { get; private set; }
		public Thread? ProcessThread { get; set; }
		public sealed class StateTransition
		{
			public readonly TState CurrentState;
			public readonly TCommand Command;
			public StateTransition(TState currentState, TCommand command)
			{
				CurrentState = currentState;
				Command = command;
			}
			public override int GetHashCode()
			{
				return 17 + 31 * CurrentState!.GetHashCode() + 31 * Command.GetHashCode();
			}
			public override bool Equals(object? obj)
			{
				StateTransition? otherState = obj as StateTransition;
				return otherState is not null
					&& otherState.CurrentState.Equals(CurrentState)
					&& Command.Equals(otherState.Command);
			}
			public override string ToString()
			{
				return $"{Command}|{CurrentState}|{GetHashCode()}";
			}
		}
		public Process(TState initialState)
		{
			CurrentState = initialState;
			StateTransitions = new Dictionary<StateTransition, TState>();
			ProcessThread = null;
		}
		public void AddStateTransition(StateTransition transition, TState processState)
		{
			if (!StateTransitions.Contains(
				new KeyValuePair<StateTransition, TState>(transition, processState)))
				StateTransitions.Add(transition, processState);
		}
		public TState MoveToNextState(TCommand command)
		{
			var transition = new StateTransition(CurrentState, command);
			TState? nextProcessState;
			if (!StateTransitions.TryGetValue(transition, out nextProcessState))
			{
				throw new KeyNotFoundException($"State transition from {CurrentState} with {command} was not defined");
			}
			CurrentState = nextProcessState!;
			return nextProcessState!;
		}
		/// <summary>
		/// ONLY used when certain unexpected error occur, the process directly goes to the specified state.
		/// </summary>
		/// <param name="gotoState">State to go to without any conditions</param>
		public void GoTo(TState gotoState)
		{
			CurrentState = gotoState;
		}
	}
}
