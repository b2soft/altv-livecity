// Licensed to b2soft under the MIT license

using System.Numerics;
using System.Threading.Tasks;
using AltV.Net.Async;
using LiveCity.Server.Generic;

namespace LiveCity.Server.LiveCity.Entities
{
	internal class LiveCityScenarioPed : LiveCityEntity
	{
		public LiveCityPed Ped { get; set; }

		public static async Task<LiveCityScenarioPed> Create(uint modelHash, string scenario, Vector3 pos, Vector3 rot)
		{
			LiveCityScenarioPed ped = new() { Ped = await AltAsync.CreatePed(modelHash, pos, rot) as LiveCityPed };

			if (ped.Ped == null)
			{
				return null;
			}

			ped.Ped.SetStreamSyncedMetaData("LiveCity", true);
			ped.Ped.SetStreamSyncedMetaData("LiveCity:ScenarioPed", scenario);

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
