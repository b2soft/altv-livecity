// Licensed to b2soft under the MIT license

using AltV.Net.Client;
using LiveCity.Shared;

namespace LiveCity.Client.Resource
{
	internal class PlayerService : ILiveCityService
	{
		public PlayerService()
		{
			Alt.OnConnectionComplete += Alt.LoadDefaultIpls;
		}
	}
}
