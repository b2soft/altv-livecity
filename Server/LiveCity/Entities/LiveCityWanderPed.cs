// Licensed to b2soft under the MIT license

using System.Numerics;
using System.Threading.Tasks;
using AltV.Net.Async;
using AltV.Net.Data;
using LiveCity.Server.Generic;

namespace LiveCity.Server.LiveCity.Entities
{
	internal class LiveCityWanderPed : LiveCityEntity
	{
		public LiveCityPed Ped { get; set; }

		public static async Task<LiveCityWanderPed> Create(uint modelHash, Vector3 pos)
		{
			LiveCityWanderPed ped = new()
			{
				Ped = await AltAsync.CreatePed(modelHash, pos, Rotation.Zero) as LiveCityPed
			};

			if (ped.Ped == null)
			{
				return null;
			}

			ped.Ped.SetStreamSyncedMetaData("LiveCity", true);
			ped.Ped.SetStreamSyncedMetaData("LiveCity:WanderPed", true);

			return ped;
		}

		public override bool Exists()
		{
			return Ped is { Streamed: true, NetworkOwner: not null };
		}

		public override void Destroy()
		{
			base.Destroy();
			Ped?.Blip?.Destroy();
			Ped?.Destroy();
		}

		public override bool IsInRangeSquared(Vector3 position, float rangeSquared)
		{
			return Vector3.DistanceSquared(Ped.Position, position) <= rangeSquared;
		}

		public override Vector3 GetPosition()
		{
			return Ped.Position;
		}
	}
}
