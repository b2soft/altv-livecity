// Licensed to b2soft under the MIT license

using System.Numerics;
using System.Threading.Tasks;
using AltV.Net.Async;
using LiveCity.Server.Generic;

namespace LiveCity.Server.LiveCity.Entities
{
	internal class LiveCityWanderVehicle : LiveCityEntity
	{
		public LiveCityVehicle Vehicle;
		public LiveCityPed Driver;

		public static async Task<LiveCityWanderVehicle> Create(uint modelHash, uint driverModelHash, Vector3 pos, Vector3 rot)
		{
			LiveCityWanderVehicle vehicle = new()
			{
				Vehicle = await AltAsync.CreateVehicle(modelHash, pos, rot) as LiveCityVehicle,
				Driver = await AltAsync.CreatePed(driverModelHash, pos, rot) as LiveCityPed
			};

			if (vehicle.Vehicle == null || vehicle.Driver == null)
			{
				return null;
			}

			vehicle.Vehicle.SetStreamSyncedMetaData("LiveCity", true);
			vehicle.Vehicle.SetStreamSyncedMetaData("LiveCity:Wander", true);
			vehicle.Vehicle.SetStreamSyncedMetaData("LiveCity:Driver", vehicle.Driver);

			vehicle.Driver.SetStreamSyncedMetaData("LiveCity", true);
			vehicle.Driver.SetStreamSyncedMetaData("LiveCity:Vehicle", vehicle.Vehicle);

			return vehicle;
		}

		public override bool Exists()
		{
			bool vehicleExists = Vehicle.Streamed && Vehicle.NetworkOwner != null;
			bool pedExists = Driver.Streamed && Driver.NetworkOwner != null;

			return vehicleExists && pedExists;
		}

		public override void Destroy()
		{
			base.Destroy();
			Vehicle?.Destroy();
			Vehicle?.Blip?.Destroy();
			Driver?.Destroy();
		}

		public override bool IsInRangeSquared(Vector3 position, float rangeSquared)
		{
			return Vector3.DistanceSquared(position, Vehicle.Position) <= rangeSquared;
		}

		public override Vector3 GetPosition()
		{
			return Vehicle.Position;
		}
	}
}
