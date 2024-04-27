using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace flare_csharp
{
	// Just the imitation of the server message
	public class ReceivedMessage
	{
		public string Data { get; set; }
		public ReceivedMessage()
		{
			Data = string.Empty;
		}
	}
}
