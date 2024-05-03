using Flare.V1;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace flare_csharp
{
	namespace Services
	{
		public abstract class Service<TState, TCommand, TChannel> where TState : Enum where TCommand : Enum
		{
			public virtual TState State { get => Process.CurrentState; }
			public virtual Process<TState, TCommand> Process { get; protected set; }
			protected Service(Process<TState, TCommand> process)
			{
				Process = process;
				DefineWorkflow();
			}
			protected abstract void RunServiceAsync(TChannel channel, Process<TState, TCommand> process);
			protected abstract void DefineWorkflow();
			protected abstract bool ServiceEnded(TChannel channel);
			public abstract void RunService(TChannel channel);
			public abstract void EndService();
		}

		
	}
}
