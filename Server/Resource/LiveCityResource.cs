// Licensed to b2soft under the MIT license

using System;
using System.Reflection;
using AltV.Net;
using AltV.Net.Async;
using AltV.Net.Elements.Entities;
using LiveCity.Server.Generic;
using LiveCity.Server.Player;
using LiveCity.Shared;

namespace LiveCity.Server.Resource
{
	internal class LiveCityResource : AsyncResource
	{
		public LiveCityResource() :
			base(new ActionTickSchedulerFactory())
		{ }
		public override IEntityFactory<IPlayer> GetPlayerFactory()
		{
			return new LiveCityPlayerFactory();
		}

		public override IEntityFactory<IPed> GetPedFactory()
		{
			return new LiveCityPedFactory();
		}

		public override IEntityFactory<IVehicle> GetVehicleFactory()
		{
			return new LiveCityVehicleFactory();
		}

		public override void OnStart()
		{
			AssemblyConfigurationAttribute assemblyConfigurationAttribute = typeof(LiveCityResource).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
			string buildConfigurationName = assemblyConfigurationAttribute?.Configuration;

			LiveCityServiceProvider.RegisterServices();

			Console.WriteLine("[LiveCity Server] Starting");
			Console.WriteLine($"[LiveCity Server] Configuration: {buildConfigurationName}");
		}

		public override void OnStop()
		{
			Console.WriteLine("[LiveCity Server] Stopping");
		}
	}
}
