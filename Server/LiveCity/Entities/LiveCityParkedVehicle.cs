// Licensed to b2soft under the MIT license

using System.Numerics;
using System.Threading.Tasks;
using AltV.Net.Async;
using LiveCity.Server.Generic;

namespace LiveCity.Server.LiveCity.Entities
{
	internal class LiveCityParkedVehicle : LiveCityEntity
	{
		public LiveCityVehicle Vehicle { get; set; }

		public static async Task<LiveCityParkedVehicle> Create(uint modelHash, Vector3 pos, Vector3 rot)
		{
			LiveCityParkedVehicle vehicle = new() { Vehicle = await AltAsync.CreateVehicle(modelHash, pos, rot) as LiveCityVehicle };

			if (vehicle.Vehicle == null)
			{
				return null;
			}

			vehicle.Vehicle.SetStreamSyncedMetaData("LiveCity", true);
			vehicle.Vehicle.SetStreamSyncedMetaData("LiveCity:Parked", true);
			return vehicle;
		}

		public override bool Exists()
		{
			return Vehicle is { Streamed: true, NetworkOwner: not null };
		}

		public override void Destroy()
		{
			base.Destroy();
			Vehicle?.Blip?.Destroy();
			Vehicle?.Destroy();
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
