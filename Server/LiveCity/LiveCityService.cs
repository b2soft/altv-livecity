// Licensed to b2soft under the MIT license

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AltV.Net;
using AltV.Net.Async;
using AltV.Net.Data;
using AltV.Net.Elements.Entities;
using AltV.Net.Enums;
using LiveCity.Server.Generic;
using LiveCity.Server.LiveCity.Entities;
using LiveCity.Server.Player;
using LiveCity.Shared;

namespace LiveCity.Server.LiveCity
{
	internal class LiveCityService : ILiveCityService
	{
		public ConcurrentDictionary<LiveCityPlayer, byte> TrackedPlayers = new();

		public ConcurrentDictionary<LiveCityWanderVehicle, byte> WanderVehicles = new();
		public ConcurrentDictionary<LiveCityParkedVehicle, byte> ParkedVehicles = new();

		public ConcurrentDictionary<LiveCityWanderPed, byte> WanderPeds = new();
		public ConcurrentDictionary<LiveCityScenarioPed, byte> ScenarioPeds = new();

		private ConcurrentDictionary<IEntity, LiveCityEntity> m_altvEntityToLiveCity = new();

		private readonly RandomProvider m_randomProvider;
		private readonly LiveCityDataService m_liveCityDataService;

		private const int k_intervalMsec = 100;

		private readonly CancellationTokenSource m_tokenSource;

		public LiveCityService(RandomProvider randomProvider, LiveCityDataService liveCityDataService)
		{
			if (!GlobalConfig.EnableLiveCity)
				return;

			Alt.OnClient(EventNames.LiveCity.s_playerSpawned, (LiveCityPlayer player) =>
			{
				player.SpawnCompleted = true;
			});

			Alt.OnClient(EventNames.LiveCity.s_clientRequestsDestroy, (IEntity entity) =>
			{
				if (entity != null)
				{
					if (m_altvEntityToLiveCity.TryGetValue(entity, out LiveCityEntity liveCityEntity))
					{
						liveCityEntity.Destroy();
					}
				}
			});

			// Driver of wander vehicle always follows vehicle's owner
			Alt.OnNetworkOwnerChange += (target, oldOwner, newOwner) =>
			{
				if (target != null && target.GetStreamSyncedMetaData("LiveCity:Driver", out LiveCityPed driver))
				{
					driver?.SetNetworkOwner(newOwner);
				}
			};

			m_randomProvider = randomProvider;
			m_liveCityDataService = liveCityDataService;

			m_tokenSource = new CancellationTokenSource();

			Task.Run(async () => await OnTick(), m_tokenSource.Token);
		}

		~LiveCityService()
		{
			using (m_tokenSource)
			{
				m_tokenSource.Cancel();
			}
		}

		private bool CanSpawnVehicleAtPosition(Vector3 position)
		{
			//TODO: use GetEntitiesInRange once it's fixed
			return Alt.GetAllVehicles().All(veh => !(veh.Position.Distance(position) < 10.0f));
		}
		private void SetupVehicle(LiveCityVehicle vehicle)
		{
			(byte, byte) colors = m_liveCityDataService.GetRandomCarColor(vehicle.Model);
			vehicle.PrimaryColor = colors.Item1;
			vehicle.SecondaryColor = colors.Item1; //TODO generate diff colors
			vehicle.NumberplateText = m_liveCityDataService.GenerateNumberPlate();
			vehicle.LockState = VehicleLockState.Locked;
		}

		private void SetupPed(LiveCityPed ped)
		{
			Dictionary<int, Tuple<int, int>> components = new();
			if (m_liveCityDataService.PedComponentVariations.TryGetValue(ped.Model, out Dictionary<int, Dictionary<int, int>> variation))
			{
				foreach ((int componentId, Dictionary<int, int> value) in variation)
				{
					if (componentId is 0 or 1 or 3 or 5 or 7 or 9 or 10)
					{
						components[componentId] = new Tuple<int, int>(0, 0);
						continue;
					}

					int chosenDrawableId = m_randomProvider.GetInt(value.Count);
					int chosenTextureId = m_randomProvider.GetInt(value[chosenDrawableId]);

					components[componentId] = new Tuple<int, int>(chosenDrawableId, chosenTextureId);
				}
			}

			ped.SetStreamSyncedMetaData("Ped:Components", JsonSerializer.Serialize(components));
		}

		private async Task SpawnWanderVehicle(LiveCityDataService.StreetNodeOption streetNodeOption)
		{
			int laneCountForward = streetNodeOption.ConnectedNode.LaneCountForward;
			Vector3 p0 = (Vector3)streetNodeOption.Node.Position;
			Vector3 p1 = (Vector3)streetNodeOption.ConnectedNode.Node.Position;
			Vector3 dv = p1 - p0;
			Vector3 dir = Vector3.Normalize(dv);

			Vector3 dup = Vector3.UnitZ;
			Vector3 right = Vector3.Cross(dir, dup);

			int lanesTotal = laneCountForward + streetNodeOption.ConnectedNode.LaneCountBackward;
			float laneWidth = 5.5f;
			float laneOffset = 0.0f; // TODO: Use real value, once DurtyFree updates the dump
			float inner = laneOffset * laneWidth;
			float outer = inner + MathF.Max(laneWidth * laneCountForward, 0.5f);

			float totalWidth = lanesTotal * laneWidth;
			float halfWidth = totalWidth * 0.5f;

			if (streetNodeOption.ConnectedNode.LaneCountBackward == 0)
			{
				inner -= halfWidth;
				outer -= halfWidth;
			}

			if (laneCountForward == 0)
			{
				inner += halfWidth;
				outer += halfWidth;
			}

			Vector3 v0 = p0 + right * inner;
			//Vector3 v1 = p0 + right * outer;
			//Vector3 v2 = p1 + right * inner;
			Vector3 v3 = p1 + right * outer;

			Vector3 middlePoint = Vector3.Lerp(v0, v3, 0.5f);
			Vector3 middleOrigin = middlePoint - 0.5f * laneWidth * laneCountForward * right;
			int chosenLane = m_randomProvider.GetInt(streetNodeOption.ConnectedNode.LaneCountForward);

			Position spawnPosition = middleOrigin + right * (chosenLane + 1) * laneWidth * 0.5f;

			if (!CanSpawnVehicleAtPosition(spawnPosition))
			{
				return;
			}

			LiveCityDataService.Zone zone = m_liveCityDataService.GetZoneByPosition(spawnPosition);
			if (zone == null)
			{
				//TODO: fallback for positions which are not inside any zone. like 1368.424 -881.748 13.843
				//trackedPlayer.Emit("debug", areaPosition.Value);
				return;
			}

			uint chosenVehModel = m_liveCityDataService.GetRandomVehicleModelByPosition(spawnPosition, true, false);
			uint chosenDriverModelHash = m_liveCityDataService.GetRandomPedModelByPosition(spawnPosition);

			// TODO: pitch & roll
			Vector3 rotation = new(0, 0, 0 - MathF.Atan2(p1.X - p0.X, p1.Y - p0.Y));

			LiveCityWanderVehicle wanderVehicle = await LiveCityWanderVehicle.Create(chosenVehModel, chosenDriverModelHash, spawnPosition, rotation);
			if (wanderVehicle == null)
			{
				return;
			}

			WanderVehicles.TryAdd(wanderVehicle, new byte());

			m_altvEntityToLiveCity.TryAdd(wanderVehicle.Vehicle, wanderVehicle);
			m_altvEntityToLiveCity.TryAdd(wanderVehicle.Driver, wanderVehicle);

			SetupVehicle(wanderVehicle.Vehicle);
			SetupPed(wanderVehicle.Driver);

			// Debug blip
			wanderVehicle.Vehicle.Blip = await AltAsync.CreateBlip(true, BlipType.Vehicle, wanderVehicle.Vehicle.Position, new IPlayer[] { });
			wanderVehicle.Vehicle.Blip.Sprite = 225;
			wanderVehicle.Vehicle.Blip.Color = 5;
		}

		private async Task SpawnParkedVehicle(LiveCityDataService.CarGenerator carGenerator)
		{
			uint chosenVehModel = 0;
			if (carGenerator.Model != "0")
			{
				chosenVehModel = Alt.Hash(carGenerator.Model);
			}
			else if (carGenerator.PopGroup != "0")
			{
				if (m_liveCityDataService.VehGroups.ContainsKey(carGenerator.PopGroup))
				{
					chosenVehModel = Alt.Hash(m_liveCityDataService.VehGroups[carGenerator.PopGroup][m_randomProvider.GetInt(m_liveCityDataService.VehGroups[carGenerator.PopGroup].Count)]);
				}
			}

			Vector3 spawnPosition = carGenerator.Position;

			if (!CanSpawnVehicleAtPosition(spawnPosition))
			{
				return;
			}

			if (chosenVehModel == 0)
			{
				LiveCityDataService.Zone zone = m_liveCityDataService.GetZoneByPosition(spawnPosition);
				if (zone == null)
				{
					//TODO: fallback for positions which are not inside any zone. like 1368.424 -881.748 13.843
					//trackedPlayer.Emit("debug", areaPosition.Value);
					Alt.Log("Null Zone while spawning Parked Vehicle");
					return;
				}

				chosenVehModel = m_liveCityDataService.GetRandomVehicleModelByPosition(spawnPosition, false, true);
			}

			float yaw = 0 - MathF.Atan2(carGenerator.OrientX, carGenerator.OrientY);
			Rotation rot = new(0, 0, yaw);

			LiveCityParkedVehicle parkedVehicle = await LiveCityParkedVehicle.Create(chosenVehModel, spawnPosition, rot);
			if (parkedVehicle == null)
			{
				return;
			}

			ParkedVehicles.TryAdd(parkedVehicle, new byte());

			m_altvEntityToLiveCity.TryAdd(parkedVehicle.Vehicle, parkedVehicle);

			SetupVehicle(parkedVehicle.Vehicle);

			// Debug blip
			parkedVehicle.Vehicle.Blip = await AltAsync.CreateBlip(true, BlipType.Vehicle, parkedVehicle.Vehicle.Position, new IPlayer[] { });
			parkedVehicle.Vehicle.Blip.Sprite = 225;
			parkedVehicle.Vehicle.Blip.Color = 1; // red
		}

		private async Task<(int, int)> TryFillParkedVehicles(Vector3 origin, Vector3 forwardVector)
		{
			int parkedVehiclesClose = 0;
			int parkedVehiclesFar = 0;
			foreach ((LiveCityParkedVehicle vehicle, byte _) in ParkedVehicles)
			{
				if (vehicle.IsInRangeSquared(origin, GlobalConfig.LiveCity.CloseRangeSquared))
				{
					parkedVehiclesClose++;
				}
				else if (LiveCityDataService.IsPointInsideSector(origin, vehicle.GetPosition(), forwardVector, GlobalConfig.LiveCity.FarSectorHalfAngle, GlobalConfig.StreamingRange))
				{
					parkedVehiclesFar++;
				}
			}
			int totalParked = parkedVehiclesClose + parkedVehiclesFar;

			bool vehicleNeeded = totalParked < GlobalConfig.LiveCity.ParkedVehiclesBudget;
			bool closeRange = parkedVehiclesClose < GlobalConfig.LiveCity.ParkedVehiclesBudget / 2;

			if (vehicleNeeded)
			{
				LiveCityDataService.CarGenerator carGenerator = closeRange
					? m_liveCityDataService.GetRandomCarGenInRange(origin, GlobalConfig.LiveCity.CloseRange, GlobalConfig.LiveCity.MinimumRange)
					: m_liveCityDataService.GetRandomCarGenInSector(origin, forwardVector, GlobalConfig.LiveCity.FarSectorHalfAngle, GlobalConfig.StreamingRange, GlobalConfig.LiveCity.CloseRange);

				if (carGenerator != null)
				{
					await SpawnParkedVehicle(carGenerator);
				}
			}

			return (parkedVehiclesClose, parkedVehiclesFar);
		}

		private async Task<(int, int)> TryFillWanderVehicles(Vector3 origin, Vector3 forwardVector)
		{
			int wanderVehiclesClose = 0;
			int wanderVehiclesFar = 0;
			foreach ((LiveCityWanderVehicle vehicle, byte _) in WanderVehicles)
			{
				if (vehicle.IsInRangeSquared(origin, GlobalConfig.LiveCity.CloseRangeSquared))
				{
					wanderVehiclesClose++;
				}
				else if (LiveCityDataService.IsPointInsideSector(origin, vehicle.GetPosition(), forwardVector, GlobalConfig.LiveCity.FarSectorHalfAngle, GlobalConfig.StreamingRange))
				{
					wanderVehiclesFar++;
				}
			}
			int totalWander = wanderVehiclesClose + wanderVehiclesFar;

			bool vehicleNeeded = totalWander < GlobalConfig.LiveCity.WanderVehiclesBudget;
			bool closeRange = wanderVehiclesClose < GlobalConfig.LiveCity.WanderVehiclesBudget / 2;

			if (vehicleNeeded)
			{
				LiveCityDataService.StreetNodeOption spawnNodeOption = closeRange
					? m_liveCityDataService.GetRandomStreetNodeInRange(origin, GlobalConfig.LiveCity.CloseRange, GlobalConfig.LiveCity.MinimumRange)
					: m_liveCityDataService.GetRandomStreetNodeInSector(origin, forwardVector, GlobalConfig.LiveCity.FarSectorHalfAngle, GlobalConfig.StreamingRange, GlobalConfig.LiveCity.CloseRange);

				if (spawnNodeOption != null)
				{
					await SpawnWanderVehicle(spawnNodeOption);
				}
			}

			return (wanderVehiclesClose, wanderVehiclesFar);
		}

		private async Task SpawnWanderPed(Vector3 position)
		{
			uint chosenModelHash = m_liveCityDataService.GetRandomPedModelByPosition(position);
			Vector3 spawnPos = position + new Vector3(0.0f, 0.0f, 1.0f);

			LiveCityWanderPed wanderPed = await LiveCityWanderPed.Create(chosenModelHash, spawnPos);
			if (wanderPed == null)
			{
				return;
			}

			WanderPeds.TryAdd(wanderPed, new byte());

			m_altvEntityToLiveCity.TryAdd(wanderPed.Ped, wanderPed);

			SetupPed(wanderPed.Ped);

			// Debug blip
			wanderPed.Ped.Blip = await AltAsync.CreateBlip(true, BlipType.Ped, wanderPed.Ped.Position, new IPlayer[] { });
			wanderPed.Ped.Blip.Sprite = 480;
		}

		private async Task SpawnScenarioPed(LiveCityDataService.ScenarioPoint scenarioPoint)
		{
			Vector3 spawnPos = new(scenarioPoint.Position.X, scenarioPoint.Position.Y, scenarioPoint.Position.Z);

			string chosenGroup = scenarioPoint.ModelType;
			uint chosenModelHash = chosenGroup == "none" || !m_liveCityDataService.PedModelGroups.ContainsKey(chosenGroup)
				? m_liveCityDataService.GetRandomPedModelByPosition(spawnPos)
				: Alt.Hash(m_liveCityDataService.PedModelGroups[chosenGroup][m_randomProvider.GetInt(m_liveCityDataService.PedModelGroups[chosenGroup].Count)]);

			Rotation rot = new(0, 0, scenarioPoint.Position.W);
			LiveCityScenarioPed scenarioPed = await LiveCityScenarioPed.Create(chosenModelHash, scenarioPoint.IType, spawnPos, rot);
			if (scenarioPed == null)
			{
				return;
			}

			ScenarioPeds.TryAdd(scenarioPed, new byte());

			m_altvEntityToLiveCity.TryAdd(scenarioPed.Ped, scenarioPed);

			SetupPed(scenarioPed.Ped);

			// Debug blip
			scenarioPed.Ped.Blip = await AltAsync.CreateBlip(true, BlipType.Ped, scenarioPed.Ped.Position, new IPlayer[] { });
			scenarioPed.Ped.Blip.Sprite = 480;
			scenarioPed.Ped.Blip.Color = 2; // green
		}

		private async Task<(int, int)> TryFillWanderPeds(Vector3 origin, Vector3 forwardVector)
		{
			int wanderPedsClose = 0;
			int wanderPedsFar = 0;
			foreach ((LiveCityWanderPed ped, byte _) in WanderPeds)
			{
				if (ped.IsInRangeSquared(origin, GlobalConfig.LiveCity.CloseRangeSquared))
				{
					wanderPedsClose++;
				}
				else if (LiveCityDataService.IsPointInsideSector(origin, ped.GetPosition(), forwardVector,
							 GlobalConfig.LiveCity.FarSectorHalfAngle, GlobalConfig.StreamingRange))
				{
					wanderPedsFar++;
				}
			}

			int totalWanderPeds = wanderPedsClose + wanderPedsFar;

			bool pedNeeded = totalWanderPeds < GlobalConfig.LiveCity.WanderPedsBudget;
			bool closeRange = wanderPedsClose < GlobalConfig.LiveCity.WanderPedsBudget / 2;

			if (pedNeeded)
			{
				Vector3? areaPosition = closeRange
									? m_liveCityDataService.GetRandomFootpathPointInRange(origin, GlobalConfig.LiveCity.CloseRange, GlobalConfig.LiveCity.MinimumRange)
									: m_liveCityDataService.GetRandomFootpathPointInSector(origin, forwardVector, GlobalConfig.LiveCity.FarSectorHalfAngle, GlobalConfig.StreamingRange, GlobalConfig.LiveCity.CloseRange);

				if (areaPosition != null)
				{
					await SpawnWanderPed(areaPosition.Value);
				}
			}

			return (wanderPedsClose, wanderPedsFar);
		}

		private async Task<(int, int)> TryFillScenarioPeds(Vector3 origin, Vector3 forwardVector)
		{
			int scenarioPedsClose = 0;
			int scenarioPedsFar = 0;
			foreach ((LiveCityScenarioPed ped, byte _) in ScenarioPeds)
			{
				if (ped.IsInRangeSquared(origin, GlobalConfig.LiveCity.CloseRangeSquared))
				{
					scenarioPedsClose++;
				}
				else if (LiveCityDataService.IsPointInsideSector(origin, ped.GetPosition(), forwardVector,
							 GlobalConfig.LiveCity.FarSectorHalfAngle, GlobalConfig.StreamingRange))
				{
					scenarioPedsFar++;
				}
			}

			int totalScenarioPeds = scenarioPedsClose + scenarioPedsFar;

			bool pedNeeded = totalScenarioPeds < GlobalConfig.LiveCity.ScenarioPedsBudget;
			bool closeRange = scenarioPedsClose < GlobalConfig.LiveCity.ScenarioPedsBudget / 2;

			if (pedNeeded)
			{
				LiveCityDataService.ScenarioPoint scenarioPoint = closeRange
					? m_liveCityDataService.GetRandomScenarioPointInRange(origin, GlobalConfig.LiveCity.CloseRange,
						GlobalConfig.LiveCity.MinimumRange)
					: m_liveCityDataService.GetRandomScenarioPointInSector(origin, forwardVector,
						GlobalConfig.LiveCity.FarSectorHalfAngle, GlobalConfig.StreamingRange,
						GlobalConfig.LiveCity.CloseRange);

				if (scenarioPoint != null)
				{
					await SpawnScenarioPed(scenarioPoint);
				}
			}

			return (scenarioPedsClose, scenarioPedsFar);
		}

		private async Task OnTick()
		{
			while (!m_tokenSource.IsCancellationRequested)
			{
				DateTime now = DateTime.Now;

				var allLiveCityEntities = ParkedVehicles.Keys.Cast<LiveCityEntity>().Concat(WanderVehicles.Keys).Concat(WanderPeds.Keys).Concat(ScenarioPeds.Keys).ToList();

				// Update lifetime states
				foreach (LiveCityEntity entity in allLiveCityEntities)
				{
					bool exists = entity.Exists();

					switch (entity.State)
					{
						case ELiveCityState.JustCreated:
							{
								if (exists)
								{
									entity.State = ELiveCityState.Active;
								}
								break;
							}
						case ELiveCityState.Active:
							{
								if (!exists)
								{
									// TODO: return to cache
									entity.Destroy();
								}
								break;
							}
						case ELiveCityState.Destroying:
							break;
					}
				}

				// Cleanup destroyed entities
				foreach (LiveCityEntity entity in allLiveCityEntities.Where(entity => entity.State == ELiveCityState.Destroying))
				{
					switch (entity)
					{
						case LiveCityParkedVehicle parkedVehicle:
							ParkedVehicles.Remove(parkedVehicle, out _);
							m_altvEntityToLiveCity.Remove(parkedVehicle.Vehicle, out _);
							break;
						case LiveCityWanderVehicle wanderVehicle:
							WanderVehicles.Remove(wanderVehicle, out _);
							m_altvEntityToLiveCity.Remove(wanderVehicle.Vehicle, out _);
							m_altvEntityToLiveCity.Remove(wanderVehicle.Driver, out _);
							break;
						case LiveCityWanderPed wanderPed:
							WanderPeds.Remove(wanderPed, out _);
							m_altvEntityToLiveCity.Remove(wanderPed.Ped, out _);
							break;
						case LiveCityScenarioPed scenarioPed:
							ScenarioPeds.Remove(scenarioPed, out _);
							m_altvEntityToLiveCity.Remove(scenarioPed.Ped, out _);
							break;
					}
				}

				// Update blips positions
				foreach (KeyValuePair<LiveCityWanderVehicle, byte> wanderVehicle in WanderVehicles)
				{
					wanderVehicle.Key.Vehicle.Blip.Position = wanderVehicle.Key.Vehicle.Position;
				}

				foreach (KeyValuePair<LiveCityWanderPed, byte> wanderPed in WanderPeds)
				{
					wanderPed.Key.Ped.Blip.Position = wanderPed.Key.Ped.Position;
				}

				IReadOnlyCollection<IVehicle> allVehicles = Alt.GetAllVehicles();
				IReadOnlyCollection<IPlayer> allPlayers = Alt.GetAllPlayers();
				IReadOnlyCollection<IPed> allPeds = Alt.GetAllPeds();
				List<IEntity> allEntities = allVehicles.Cast<IEntity>().Concat(allPlayers).Concat(allPeds).ToList();

				foreach (LiveCityPlayer trackedPlayer in TrackedPlayers.Keys)
				{
					if (!trackedPlayer.IsSpawned || !trackedPlayer.SpawnCompleted)
						continue;

					Vector3 playerPosition = trackedPlayer.Position;
					Vector3 playerForwardVector = MathUtils.GetForwardVector(trackedPlayer.Rotation);

					// Cull by sector
					foreach (LiveCityEntity entity in allLiveCityEntities)
					{
						if (entity.State != ELiveCityState.Active)
						{
							continue;
						}

						// Never cull close range
						if (entity.IsInRangeSquared(playerPosition, GlobalConfig.LiveCity.CloseRangeSquared))
						{
							continue;
						}

						if (!LiveCityDataService.IsPointInsideSector(playerPosition,
								entity.GetPosition(), playerForwardVector,
								GlobalConfig.LiveCity.FarSectorHalfAngle,
								GlobalConfig.StreamingRange))
						{
							// TODO: return to cache
							entity.Destroy();
						}
					}

					int entitiesInRange = allEntities.Count(entity =>
						entity.Position.Distance(playerPosition) < GlobalConfig.StreamingRange);

					trackedPlayer.Emit(EventNames.LiveCity.s_updateDebugEntityCount, entitiesInRange);

					// if we have no budget to spawn
					//TODO: think about destroying entities if we hit the limit
					if (entitiesInRange >= GlobalConfig.LiveCity.EntityBudget)
					{
						continue;
					}

					(int closeParked, int farParked) = await TryFillParkedVehicles(playerPosition, playerForwardVector);
					trackedPlayer.Emit(EventNames.LiveCity.s_updateDebugParkedData, closeParked, farParked);

					(int closeWander, int farWander) = await TryFillWanderVehicles(playerPosition, playerForwardVector);
					trackedPlayer.Emit(EventNames.LiveCity.s_updateDebugWanderData, closeWander, farWander);

					(int closeWanderPeds, int farWanderPeds) = await TryFillWanderPeds(playerPosition, playerForwardVector);
					trackedPlayer.Emit(EventNames.LiveCity.s_updateDebugWanderPedData, closeWanderPeds, farWanderPeds);

					(int closeScenarioPeds, int farScenarioPeds) = await TryFillScenarioPeds(playerPosition, playerForwardVector);
					trackedPlayer.Emit(EventNames.LiveCity.s_updateDebugScenarioPedData, closeScenarioPeds, farScenarioPeds);
				}

				TimeSpan tickTime = DateTime.Now - now;
				int delta = k_intervalMsec - tickTime.Milliseconds;
				if (delta < 0)
				{
					Alt.LogWarning($"LiveCityService::OnTick took too long: {tickTime.Milliseconds} msec!");
				}

				int delay = Math.Max(0, delta);
				await Task.Delay(delay);
			}
		}

		public void AddPlayer(LiveCityPlayer player)
		{
			TrackedPlayers.TryAdd(player, new byte());
			Alt.Log("[LiveCity] Player Connected");
		}

		public void RemovePlayer(LiveCityPlayer player)
		{
			TrackedPlayers.TryRemove(player, out _);
			Alt.Log("[LiveCity] Player Disconnected");
		}
	}
}
