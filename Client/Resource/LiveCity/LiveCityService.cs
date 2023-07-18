// Licensed to b2soft under the MIT license

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using AltV.Net.Client;
using AltV.Net.Client.Async;
using AltV.Net.Client.Elements.Interfaces;
using LiveCity.Shared;

namespace LiveCity.Client.Resource.LiveCity
{
	internal class LiveCityService : ILiveCityService
	{
		public LiveCityService()
		{
			Alt.OnNetOwnerChange += OnNetOwnerChange;

			Alt.OnPlayerSpawn += () =>
			{
				Alt.EmitServer(EventNames.LiveCity.s_playerSpawned);
			};
		}

		private void SetPedDrivingWander(IPed ped, IVehicle vehicle)
		{
			if (!Alt.Natives.IsPedInVehicle(ped.ScriptId, vehicle, false))
			{
				Alt.Natives.SetPedIntoVehicle(ped.ScriptId, vehicle, -1);
			}

			// https://forge.plebmasters.de/vehicleflags?category=DrivingStyleFlags&value=802987
			Alt.Natives.TaskVehicleDriveWander(ped.ScriptId, vehicle, 13.0f, 802987);
		}

		private async Task HandleVehicle(IVehicle vehicle)
		{
			try
			{
				await AltAsync.WaitFor(() => vehicle.ScriptId != 0 && Alt.Natives.HasModelLoaded(vehicle.Model), 10000);
			}
			catch
			{
				Alt.EmitServer(EventNames.LiveCity.s_clientRequestsDestroy, vehicle);
				return;
			}

			if (vehicle.GetStreamSyncedMetaData("LiveCity:Driver", out IPed driver))
			{
				try
				{
					await AltAsync.WaitFor(() => driver.Spawned, 3000);
				}
				catch
				{
					Alt.EmitServer(EventNames.LiveCity.s_clientRequestsDestroy, vehicle);
					return;
				}

				if (vehicle.Spawned && driver is { Spawned: true })
				{
					if (vehicle.NetworkOwner == driver.NetworkOwner)
					{
						await AltAsync.ReturnToMainThread();

						SetPedDrivingWander(driver, vehicle);
					}
					else
					{
						Alt.EmitServer(EventNames.LiveCity.s_clientRequestsDestroy, vehicle);
					}
				}
			}
		}

		private async Task HandlePed(IPed ped)
		{
			try
			{
				await AltAsync.WaitFor(() => ped.ScriptId != 0 && Alt.Natives.HasModelLoaded(ped.Model), 10000);
			}
			catch
			{
				Alt.EmitServer(EventNames.LiveCity.s_clientRequestsDestroy, ped);
				return;
			}

			if (ped.GetStreamSyncedMetaData("LiveCity:Vehicle", out IVehicle assignedVehicle))
			{
				try
				{
					await AltAsync.WaitFor(() => assignedVehicle.Spawned, 3000);
				}
				catch
				{
					Alt.EmitServer(EventNames.LiveCity.s_clientRequestsDestroy, ped);
					return;
				}
			}

			// Maybe despawned while waiting
			if (!ped.Spawned)
			{
				Alt.EmitServer(EventNames.LiveCity.s_clientRequestsDestroy, ped);
				return;
			}

			await AltAsync.ReturnToMainThread();

			if (ped.GetStreamSyncedMetaData("Ped:Components", out string componentsString))
			{
				Dictionary<int, Tuple<int, int>> components =
					JsonSerializer.Deserialize<Dictionary<int, Tuple<int, int>>>(componentsString);
				foreach (KeyValuePair<int, Tuple<int, int>> pair in components)
				{
					Alt.Natives.SetPedComponentVariation(ped.ScriptId, pair.Key, pair.Value.Item1,
						pair.Value.Item2, 2);
				}
			}

			if (ped.GetStreamSyncedMetaData("LiveCity:ScenarioPed", out string scenario))
			{
				Alt.Natives.TaskStartScenarioInPlace(ped.ScriptId, scenario, 0, false);
			}
			else if (ped.HasStreamSyncedMetaData("LiveCity:WanderPed"))
			{
				Alt.Natives.TaskWanderStandard(ped.ScriptId, 40000.0f, 0);
			}
			else if (assignedVehicle is { Spawned: true })
			{
				if (ped.NetworkOwner == assignedVehicle.NetworkOwner)
				{
					SetPedDrivingWander(ped, assignedVehicle);
				}
				else
				{
					Alt.EmitServer(EventNames.LiveCity.s_clientRequestsDestroy, ped);
				}
			}
		}

		private void OnNetOwnerChange(IEntity target, IPlayer newOwner, IPlayer oldOwner)
		{
			if (target == null)
			{
				return;
			}

			// Not a LiveCity
			if (!target.HasStreamSyncedMetaData("LiveCity"))
			{
				return;
			}

			// We are not owning it
			if (newOwner != Alt.LocalPlayer)
			{
				Alt.Natives.NetworkFadeOutEntity(target, false, false);
				return;
			}

			// Entity was just created (not migrated)
			if (oldOwner == null)
			{
				Alt.Natives.NetworkFadeInEntity(target, true, 0);
			}

			Task.Run(async () =>
			{
				switch (target)
				{
					case IVehicle vehicle:
						await HandleVehicle(vehicle);
						break;
					case IPed ped:
						await HandlePed(ped);
						break;
				}
			}).HandleError();
		}
	}
}
