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
			public virtual TChannel Channel { get; set; }
			public virtual TState State { get => Process.CurrentState; }
			public virtual Process<TState, TCommand> Process { get; protected set; }
			protected Service(Process<TState, TCommand> process, TChannel channel)
			{
				Process = process;
				Channel = channel;
				DefineWorkflow();
			}
			public abstract void RunServiceAsync();
			protected abstract void DefineWorkflow();
			protected abstract bool ServiceEnded();
			public abstract void StartService();
			public abstract void EndService();
		}

		
	}
}
