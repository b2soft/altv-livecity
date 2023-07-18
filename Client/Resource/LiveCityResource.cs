// Licensed to b2soft under the MIT license

using AltV.Net.Client;
using AltV.Net.Client.Async;
using LiveCity.Shared;

namespace LiveCity.Client.Resource
{
	internal class LiveCityResource : AsyncResource
	{
		public override void OnStart()
		{
			Alt.LogInfo("Client C# Started!");

			LiveCityServiceProvider.RegisterServices();
		}

		public override void OnStop()
		{
			Alt.LogInfo("Client C# Stopped!");
		}
	}
}
