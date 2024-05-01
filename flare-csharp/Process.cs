using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace flare_csharp
{
	public class Process<T>
	{
		public T CurrentState { get; set; }
		public enum Command { Begin, End, Pause, Resume, Abort, Exit }
		public Dictionary<StateTransition, T> StateTransitions { get; set; }
		public Thread ProcessThread { get; set; }
		public class StateTransition
		{
			public readonly T CurrentState;
			public readonly Command Command;
			public StateTransition(T currentState, Command command)
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
				return otherState is null ? false : otherState.CurrentState!.Equals(this.CurrentState);
			}
			public override string ToString()
			{
				return $"{Command}|{CurrentState}|{this.GetHashCode()}";
			}
		}
		public Process(T initialState, Thread processThread)
		{
			CurrentState = initialState;
			StateTransitions = new Dictionary<StateTransition, T>();
			ProcessThread = processThread;
		}
		public void AddStateTransition(StateTransition transition, T processState)
		{
			if (!StateTransitions.Contains(
				new KeyValuePair<StateTransition, T>(transition, processState)))
				StateTransitions.Add(transition, processState);
		}
		public T MoveToNextState(Command command)
		{
			var transition = new StateTransition(CurrentState, command);
			T? nextProcessState;
			if (!StateTransitions.TryGetValue(transition, out nextProcessState))
			{
				throw new KeyNotFoundException($"State transition from {CurrentState} with {command} was not defined");
			}
			CurrentState = nextProcessState!;
			return nextProcessState!;
		}
	}
}
