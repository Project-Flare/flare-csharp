using Flare.V1;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace flare_csharp
{
	public class UserService
	{
		Users.UsersClient _clientService;
		public UserService(GrpcChannel channel)
		{
			_clientService = new Users.UsersClient(channel);
		}

		public async Task<User> GetUser(string username)
		{
			GetUserRequest getUserRequest = new()
			{
				Username = username
			};
			CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
			GetUserResponse getUserResponse = await _clientService.GetUserAsync(getUserRequest, headers: null, deadline: null, cancellationTokenSource.Token);
			return getUserResponse.User;
		}
	}
}
