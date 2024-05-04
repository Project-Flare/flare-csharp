using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace flare_csharp
{
	public class MainManager
	{
		public Uri ServerUri { get; set; }
		public GrpcChannel GrpcChannel { get; set; }
		public Credentials Credentials { get; set; }
		public MainManager()
		{
			ServerUri = new Uri("https://rpc.f2.project-flare.net");
			GrpcChannel = GrpcChannel.ForAddress(ServerUri);
			Credentials = new Credentials(1024 * 64, 3);
			// Get credential requirements -> kinda process
			// Username validation (GetUsernameOpinion, )
		}
	}
}
