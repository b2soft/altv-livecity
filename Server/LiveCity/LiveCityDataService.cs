// Licensed to b2soft under the MIT license

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;
using AltV.Net;
using AltV.Net.Data;
using AltV.Net.Enums;
using LiveCity.Server.Navigation;
using LiveCity.Shared;

namespace LiveCity.Server.LiveCity
{
	internal class LiveCityDataService : ILiveCityService
	{
		public List<StreetNode> StreetNodes = new();
		private List<StreetNode> m_vehicleNodes = new();
		private readonly Dictionary<CellCoord, List<StreetNode>> m_vehicleNodesGrid = new();

		private List<StreetNode> m_waterNodes = new();

		private List<string> m_allowedScenarios = new();
		private readonly Dictionary<CellCoord, List<ScenarioPoint>> m_scenarioMap = new();

		private readonly List<CarGenerator> m_carGenerators = new();
		private readonly Dictionary<CellCoord, List<CarGenerator>> m_carGeneratorsGrid = new();

		private readonly List<Zone> m_zones = new();

		private List<string> m_carModels = new();
		private readonly List<uint> m_colorlessCars = new();
		private List<int> m_carColorsNum = new();

		private readonly List<string> m_carGenProhibitedModels = new();
		private readonly List<string> m_ambientCarProhibitedModels = new();

		private readonly RandomProvider m_randomProvider;
		private readonly NavigationMeshProvider m_navigationMeshProvider;

		public Dictionary<uint, Dictionary<int, Dictionary<int, int>>> PedComponentVariations = new();

		public Dictionary<string, List<string>> PedGroups { get; } = new();
		public Dictionary<string, List<string>> VehGroups { get; } = new();
		public Dictionary<string, List<string>> PedModelGroups = new();
		public Dictionary<string, PopSchedule> ZoneSchedules { get; } = new();
		public List<ScenarioPoint> ScenarioPoints { get; } = new();

		public class VehicleEntry
		{
			public string Name { get; set; }
			public uint Hash { get; set; }
			public float BoundingSphereRadius { get; set; }
			public List<string> Flags { get; set; }
		}

		public class StreetNodeConnected
		{
			public StreetNode Node { get; set; }
			public int LaneCountForward { get; set; }
			public int LaneCountBackward { get; set; }
		}

		public class StreetNode
		{
			public int Id { get; set; }
			public string StreetName { get; set; }
			public bool IsValidForGps { get; set; }
			public bool IsJunction { get; set; }
			public bool IsFreeway { get; set; }
			public bool IsGravelRoad { get; set; }
			public bool IsBackroad { get; set; }
			public bool IsOnWater { get; set; }
			public bool IsPedCrossway { get; set; }
			public bool TrafficlightExists { get; set; }
			public bool LeftTurnNoReturn { get; set; }
			public bool RightTurnNoReturn { get; set; }
			public CustomVector3 Position { get; set; }
			public List<StreetNodeConnected> ConnectedNodes { get; set; }
			public int UniqueId { get; set; }
			public float Heading { get; set; }
			public int NumLanes { get; set; }
			public int NodeFlags { get; set; }
		}

		public class Zone
		{
			public string ZoneName;
			public Vector3 Min;
			public Vector3 Max;
			public string AreaName;
			public string SpName;
			public string MpName;
		}

		public class CustomVector3
		{
			public float X { get; set; }
			public float Y { get; set; }
			public float Z { get; set; }

			public static explicit operator Vector3(CustomVector3 v)
			{
				return new Vector3(v.X, v.Y, v.Z);
			}
		}

		public class CustomVector4
		{
			public float X { get; set; }
			public float Y { get; set; }
			public float Z { get; set; }
			public float W { get; set; }
		}

		public class ScenarioPoint
		{
			public CustomVector4 Position { get; set; }
			public string IType { get; set; }
			public int TimeStart { get; set; }
			public int TimeEnd { get; set; }
			public string ModelType { get; set; }
			public List<ScenarioPoint> NearScenarioPoints { get; set; }
		}

		public class PopScheduleEntry
		{
			public int MaxAmbientPeds { get; set; }
			public int MaxScenarioPeds { get; set; }
			public int MaxCars { get; set; }
			public int MaxParkedCars { get; set; }
			public int MaxLowParkedCars { get; set; }
			public float CopsCarPercentage { get; set; }
			public float CopsPedPercentage { get; set; }
			public int MaxScenPedsStreamedUnused { get; set; }
			public int MaxScenVehiclesUnused { get; set; }
			public int MaxPreassignedParkedUnused { get; set; }
			public Dictionary<string, float> PedGroupProbs { get; set; }
			public List<(string, float)> PedGroupProbsSorted { get; set; }
			public Dictionary<string, float> VehGroupProbs { get; set; }
			public List<(string, float)> VehGroupProbsSorted { get; set; }

		}

		public class PopSchedule
		{
			public PopScheduleEntry[] Entries = new PopScheduleEntry[12];
		}

		private static readonly string[] s_numberPlateChars =
		{
			"0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K",
			"L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"
		};

		public LiveCityDataService(RandomProvider randomProvider, NavigationMeshProvider navigationMeshProvider)
		{
			if (!GlobalConfig.EnableNavigationDataLoad)
				return;

			m_randomProvider = randomProvider;
			m_navigationMeshProvider = navigationMeshProvider;

			ParseZones();
			LoadPopCycles();
			LoadPopGroups();
			LoadCarGenerators();
			LoadCarGenProhibitedModels();
			LoadCarData();
			LoadScenarioPoints();
			LoadStreetNodes();
			LoadPedComponentVariations();
		}

		private void LoadStreetNodes()
		{
			StreetNodes = NavigationMeshProvider.LoadDataFromJsonFile<List<StreetNode>>("LiveCity\\ExtendedNodes.json");
			Alt.Log("Successfully loaded dump file LiveCity\\ExtendedNodes.json.");
			foreach (StreetNode streetNode in StreetNodes)
			{
				if (!streetNode.IsPedCrossway && !streetNode.IsOnWater && streetNode.StreetName != "0" && !streetNode.IsBackroad)
				{
					m_vehicleNodes.Add(streetNode);
					int x = (int)(streetNode.Position.X / 100);
					int y = (int)(streetNode.Position.Y / 100);
					CellCoord key = new() { X = x, Y = y };
					if (!m_vehicleNodesGrid.ContainsKey(key))
					{
						m_vehicleNodesGrid.Add(key, new List<StreetNode>());
					}

					m_vehicleNodesGrid[key].Add(streetNode);
				}
				else if (streetNode.IsOnWater)
				{
					m_waterNodes.Add(streetNode);
				}
			}
		}

		private void LoadPedComponentVariations()
		{
			PedComponentVariations = NavigationMeshProvider.LoadDataFromJsonFile<Dictionary<uint, Dictionary<int, Dictionary<int, int>>>>("LiveCity\\pedComponentVariations.json");
		}

		private void LoadCarGenProhibitedModels()
		{
			List<VehicleEntry> allVehicles = NavigationMeshProvider.LoadDataFromJsonFile<List<VehicleEntry>>("LiveCity\\vehicles.json");
			foreach (VehicleEntry vehicle in allVehicles)
			{
				if (vehicle.Flags.Contains("FLAG_DONT_SPAWN_IN_CARGEN"))
				{
					m_carGenProhibitedModels.Add(vehicle.Name.ToLower());
				}

				if (vehicle.Flags.Contains("FLAG_DONT_SPAWN_AS_AMBIENT"))
				{
					m_ambientCarProhibitedModels.Add(vehicle.Name.ToLower());
				}
			}
		}

		public class CarGenerator
		{
			public Vector3 Position { get; set; }
			public float OrientX { get; set; }
			public float OrientY { get; set; }
			public string Model { get; set; }
			public string PopGroup { get; set; }
		}

		private void LoadCarGenerators()
		{
			string carGensPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "LiveCity\\CarGenerators.xml");
			XElement items = XElement.Load(carGensPath);

			Alt.Log("Successfully loaded dump file LiveCity\\CarGenerators.xml.");

			List<XElement> carGeneratorElements = items.Elements("CarGenerator").ToList();
			foreach (XElement xElement in carGeneratorElements)
			{
				CarGenerator carGenerator = new()
				{
					Position = new Vector3(Convert.ToSingle(xElement.Element("Position")?.Element("X")?.Value),
					Convert.ToSingle(xElement.Element("Position")?.Element("Y")?.Value), Convert.ToSingle(xElement.Element("Position")?.Element("Z")?.Value)),
					Model = xElement.Element("Model")?.Value,
					PopGroup = xElement.Element("PopGroup")?.Value,
					OrientX = Convert.ToSingle(xElement.Element("OrientX")?.Value),
					OrientY = Convert.ToSingle(xElement.Element("OrientY")?.Value)
				};

				m_carGenerators.Add(carGenerator);
			}

			foreach (CarGenerator carGenerator in m_carGenerators)
			{
				int x = (int)(carGenerator.Position.X / 100);
				int y = (int)(carGenerator.Position.Y / 100);
				CellCoord key = new() { X = x, Y = y };
				if (!m_carGeneratorsGrid.ContainsKey(key))
				{
					m_carGeneratorsGrid.Add(key, new List<CarGenerator>());
				}

				m_carGeneratorsGrid[key].Add(carGenerator);
			}
		}

		public string GenerateNumberPlate()
		{
			string numberplate = "";

			for (int i = 0; i < 8; i++)
			{
				numberplate += m_randomProvider.Choice(s_numberPlateChars);
			}

			return numberplate;
		}

		void ParseZones()
		{
			Dictionary<string, Tuple<string, string>> zoneBindings = new();
			string zoneBindPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "LiveCity\\ZoneBind.ymt");
			XElement items = XElement.Load(zoneBindPath);

			Alt.Log("Successfully loaded dump file LiveCity\\ZoneBind.ymt.");

			List<XElement> a = items.Descendants("Item").ToList();
			foreach (XElement xElement in a)
			{
				string zoneName = xElement.Descendants("zoneName").First().Value.ToLower();
				string spName = xElement.Descendants("spName").First().Value.ToLower();
				string mpName = xElement.Descendants("mpName").First().Value.ToLower();
				zoneBindings.Add(zoneName, new Tuple<string, string>(spName, mpName));
			}

			List<string> fix = new();
			string zonesFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "LiveCity\\Zones.txt");
			string[] zoneStrings = File.ReadAllLines(zonesFilePath);
			Alt.Log("Successfully loaded dump file LiveCity\\Zones.txt.");
			foreach (string zoneString in zoneStrings)
			{
				Zone zone = new();
				string[] splitted = zoneString.Split(',');
				zone.ZoneName = splitted[0].ToLower();

				zone.Min = new Vector3(float.Parse(splitted[1]), float.Parse(splitted[2]), float.Parse(splitted[3]));
				zone.Max = new Vector3(float.Parse(splitted[4]), float.Parse(splitted[5]), float.Parse(splitted[6]));
				zone.AreaName = splitted[7].ToLower();
				zone.SpName = zoneBindings[zone.ZoneName].Item1.ToLower();
				zone.MpName = zoneBindings[zone.ZoneName].Item2.ToLower();

				m_zones.Add(zone);
			}
		}

		private void LoadPopCycles()
		{
			string popCyclesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "LiveCity\\PopCycle");
			string[] popCycleString = File.ReadAllLines(popCyclesPath);

			Alt.Log("Successfully loaded dump file LiveCity\\PopCycle.");

			PopSchedule schedule = null;
			int currentTimeIndex = 0;
			string currentZone = "";

			foreach (string s in popCycleString)
			{
				if (s.StartsWith("//") || s.Length == 0)
					continue;


				if (s.StartsWith("POP_SCHEDULE:"))
				{
					schedule = new();
				}

				else if (s.StartsWith("      "))
				{
					string[] scheduleSplitted = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
					PopScheduleEntry entry = new();
					entry.MaxAmbientPeds = int.Parse(scheduleSplitted[0]);
					entry.MaxScenarioPeds = int.Parse(scheduleSplitted[1]);
					entry.MaxCars = int.Parse(scheduleSplitted[2]);
					entry.MaxParkedCars = int.Parse(scheduleSplitted[3]);
					entry.MaxLowParkedCars = int.Parse(scheduleSplitted[4]);
					entry.CopsCarPercentage = int.Parse(scheduleSplitted[5]) * 0.01f;
					entry.CopsPedPercentage = int.Parse(scheduleSplitted[6]) * 0.01f;
					//unused 7 8 9
					entry.PedGroupProbs = new();
					entry.VehGroupProbs = new();
					bool pedsMode = true;
					string currentGroup = "";
					for (int i = 10; i < scheduleSplitted.Length; i++)
					{
						if (scheduleSplitted[i] == "peds")
						{
							continue;
						}

						if (scheduleSplitted[i] == "cars")
						{
							pedsMode = false;
						}
						else
						{
							if (int.TryParse(scheduleSplitted[i], out int probability))
							{
								float prob = probability * 0.01f;
								if (pedsMode)
								{
									entry.PedGroupProbs[currentGroup] = prob;
								}
								else
								{
									entry.VehGroupProbs[currentGroup] = prob;
								}
							}
							else
							{
								currentGroup = scheduleSplitted[i].ToLower();
							}
						}
					}

					schedule.Entries[currentTimeIndex] = entry;

					// Sort data for probability in future
					entry.VehGroupProbsSorted = new List<(string, float)>();
					foreach (KeyValuePair<string, float> entryVehGroupProb in entry.VehGroupProbs)
					{
						entry.VehGroupProbsSorted.Add((entryVehGroupProb.Key, entryVehGroupProb.Value));
					}
					entry.VehGroupProbsSorted.Sort((lhs, rhs) => rhs.Item2.CompareTo(lhs.Item2));

					entry.PedGroupProbsSorted = new List<(string, float)>();
					foreach (KeyValuePair<string, float> entryPedGroupProb in entry.PedGroupProbs)
					{
						entry.PedGroupProbsSorted.Add((entryPedGroupProb.Key, entryPedGroupProb.Value));
					}
					entry.PedGroupProbsSorted.Sort((lhs, rhs) => rhs.Item2.CompareTo(lhs.Item2));

					currentTimeIndex++;
				}
				else if (s.StartsWith("END_POP_SCHEDULE"))
				{
					ZoneSchedules[currentZone] = schedule;
					currentTimeIndex = 0;
				}
				else if (s.Length > 0)
				{
					currentZone = s.ToLower();
				}
			}
		}

		private void LoadPopGroups()
		{
			string zoneBindPath =
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "LiveCity\\PopGroups.xml");
			XElement items = XElement.Load(zoneBindPath);

			Alt.Log("Successfully loaded dump file LiveCity\\PopGroups.xml.");

			List<XElement> pedGroupElements = items.Descendants("pedGroups").Elements().ToList();
			foreach (XElement xElement in pedGroupElements)
			{
				string groupName = xElement.Element("Name")!.Value.ToLower();
				List<XElement> modelElements =
					xElement.Element("models")?.Descendants("Item").Descendants("Name").ToList();
				List<string> models = new();
				foreach (XElement modelElem in modelElements!)
				{
					models.Add(modelElem.Value.ToLower());
				}

				PedGroups[groupName] = models;
			}

			List<XElement> vehGroupElements = items.Descendants("vehGroups").Elements().ToList();
			foreach (XElement xElement in vehGroupElements)
			{
				string groupName = xElement.Element("Name")!.Value.ToLower();
				List<XElement> modelElements =
					xElement.Element("models")?.Descendants("Item").Descendants("Name").ToList();
				List<string> models = new();
				foreach (XElement modelElem in modelElements!)
				{
					models.Add(modelElem.Value.ToLower());
				}

				VehGroups[groupName] = models;
			}
		}

		private void LoadScenarioPoints()
		{
			List<ScenarioPoint> allScenarioPoints =
				NavigationMeshProvider.LoadDataFromJsonFile<List<ScenarioPoint>>("LiveCity\\ScenarioPoints.json");
			m_allowedScenarios =
				NavigationMeshProvider.LoadDataFromJsonFile<List<string>>("LiveCity\\AllowedScenarios.json");
			Dictionary<string, List<string>> pedModelGroups =
				NavigationMeshProvider.LoadDataFromJsonFile<Dictionary<string, List<string>>>(
					"LiveCity\\PedModelGroup.json");
			foreach (KeyValuePair<string, List<string>> s in pedModelGroups)
			{
				PedModelGroups.Add(s.Key.ToLower(), s.Value);
			}

			foreach (ScenarioPoint scenarioPoint in allScenarioPoints)
			{
				if (m_allowedScenarios.Contains(scenarioPoint.IType))
				{
					ScenarioPoints.Add(scenarioPoint);

					int cellX = (int)(scenarioPoint.Position.X / 100),
						cellY = (int)(scenarioPoint.Position.Y / 100);

					CellCoord key = new() { X = cellX, Y = cellY };
					if (!m_scenarioMap.ContainsKey(key))
					{
						m_scenarioMap.Add(key, new List<ScenarioPoint>());
					}

					m_scenarioMap[key].Add(scenarioPoint);
				}
			}

			// From pedsync repo - search adjacent scenario points. Probably related to each other
			//foreach (ScenarioPoint scenarioPoint2 in ScenarioPoints)
			//{
			//	int cellX = (int)Math.Ceiling(scenarioPoint2.Position.X / 10),
			//		cellY = (int)Math.Ceiling(scenarioPoint2.Position.Y / 10);

			//	scenarioPoint2.NearScenarioPoints = new List<ScenarioPoint>();

			//	//if (
			//	//	!m_scenarioMap.ContainsKey((cellX, cellY)) &&
			//	//	!m_scenarioMap.ContainsKey((cellX, cellY - 1)) &&
			//	//	!m_scenarioMap.ContainsKey((cellX, cellY + 1)) &&
			//	//	!m_scenarioMap.ContainsKey((cellX - 1, cellY)) &&
			//	//	!m_scenarioMap.ContainsKey((cellX - 1, cellY - 1)) &&
			//	//	!m_scenarioMap.ContainsKey((cellX - 1, cellY + 1)) &&
			//	//	!m_scenarioMap.ContainsKey((cellX + 1, cellY)) &&
			//	//	!m_scenarioMap.ContainsKey((cellX + 1, cellY - 1)) &&
			//	//	!m_scenarioMap.ContainsKey((cellX + 1, cellY + 1))
			//	//) continue;

			//	//for (int i = -1; i < 2; i++)
			//	//{
			//	//	for (int j = -1; j < 2; j++)
			//	//	{
			//	//		//Check if zone exists
			//	//		if (m_scenarioMap.ContainsKey((cellX + i, cellY + j)))
			//	//		{
			//	//			foreach (ScenarioPoint scenarioPoint3 in m_scenarioMap[(cellX + i, cellY + j)])
			//	//			{
			//	//				double distance = Vector3.Distance(
			//	//					new Vector3(scenarioPoint2.Position.X, scenarioPoint2.Position.Y,
			//	//						scenarioPoint2.Position.Z),
			//	//					new Vector3(scenarioPoint3.Position.X, scenarioPoint3.Position.Y,
			//	//						scenarioPoint3.Position.Z));

			//	//				if (distance is < 3 and > 1 && scenarioPoint2.TimeStart == scenarioPoint3.TimeStart)
			//	//				{
			//	//					scenarioPoint2.NearScenarioPoints.Add(scenarioPoint3);
			//	//				}
			//	//			}
			//	//		}
			//	//	}
			//	//}
			//}
		}

		private void LoadCarData()
		{
			m_carModels = NavigationMeshProvider.LoadDataFromJsonFile<List<string>>("LiveCity\\CarModels.json");
			List<string> colorlessCars = NavigationMeshProvider.LoadDataFromJsonFile<List<string>>("LiveCity\\ColorlessCars.json");
			foreach (string car in colorlessCars)
			{
				m_colorlessCars.Add(Alt.Hash(car));
			}
			m_carColorsNum = NavigationMeshProvider.LoadDataFromJsonFile<List<int>>("LiveCity\\CarColorsNum.json");
		}

		public Zone GetZoneByPosition(Vector3 position)
		{
			foreach (Zone zone in m_zones)
			{
				if (zone.Min.X > zone.Max.X)
				{
					(zone.Min.X, zone.Max.X) = (zone.Max.X, zone.Min.X);
				}

				if (zone.Min.Y > zone.Max.Y)
				{
					(zone.Min.Y, zone.Max.Y) = (zone.Max.Y, zone.Min.Y);
				}

				if (zone.Min.Z > zone.Max.Z)
				{
					(zone.Min.Z, zone.Max.Z) = (zone.Max.Z, zone.Min.Z);
				}

				if (zone.Min.X <= position.X
					&& position.X <= zone.Max.X
					&& zone.Min.Y <= position.Y
					&& position.Y <= zone.Max.Y)
				//&& zone.Min.Z <= position.Z
				//&& position.Z <= zone.Max.Z)
				{
					return zone;
				}
			}

			return null;
		}

		internal record StreetNodeOption(StreetNode Node, StreetNodeConnected ConnectedNode);
		public StreetNodeOption GetRandomStreetNodeInRange(Vector3 position, float range, float minRange = 0.0f)
		{
			position.Z = 0.0f;
			const int cellSize = 100;

			float tlx = position.X - range;
			float tly = position.Y - range;

			float brx = position.X + range;
			float bry = position.Y + range;

			List<StreetNodeOption> options = new();

			for (int i = (int)(tlx / cellSize); i < (int)(brx / cellSize) + 1; ++i)
			{
				for (int j = (int)(tly / cellSize); j < (int)(bry / cellSize) + 1; ++j)
				{
					var coord = new CellCoord(i, j);
					if (m_vehicleNodesGrid.TryGetValue(coord, out List<StreetNode> value))
					{
						foreach (var point in value)
						{
							Vector3 pos2d = new(point.Position.X, point.Position.Y, 0);
							float distance = Vector3.Distance(pos2d, position);

							if (distance < range && distance > minRange)
							{
								foreach (StreetNodeConnected streetNodeConnected in point.ConnectedNodes)
								{
									if (streetNodeConnected.LaneCountForward != 0)
									{
										options.Add(new StreetNodeOption(point, streetNodeConnected));
									}
								}
							}
						}
					}
				}
			}

			if (options.Count == 0)
				return null;

			StreetNodeOption nodeOption = m_randomProvider.Choice(options);
			return nodeOption;
		}

		public static bool IsPointInsideSector(Vector3 origin, Vector3 point, Vector3 direction, float angleDegrees, float range)
		{
			float angle = MathF.Atan2(Vector3.Cross(point - origin, direction).Length(), Vector3.Dot(point - origin, direction));
			angle = MathF.Abs(angle);

			float radiusSquared = range * range;
			return angle <= MathUtils.ToRadians(angleDegrees) && Vector3.DistanceSquared(point, origin) < radiusSquared;
		}

		public StreetNodeOption GetRandomStreetNodeInSector(Vector3 position, Vector3 direction, float angleDegrees, float range, float minRange)
		{
			position.Z = 0.0f;
			const int cellSize = 100;

			float tlx = position.X - range;
			float tly = position.Y - range;

			float brx = position.X + range;
			float bry = position.Y + range;

			List<StreetNodeOption> options = new();

			for (int i = (int)(tlx / cellSize); i < (int)(brx / cellSize) + 1; ++i)
			{
				for (int j = (int)(tly / cellSize); j < (int)(bry / cellSize) + 1; ++j)
				{
					var coord = new CellCoord(i, j);
					if (m_vehicleNodesGrid.TryGetValue(coord, out List<StreetNode> value))
					{
						foreach (var point in value)
						{
							Vector3 pos2d = new(point.Position.X, point.Position.Y, 0);
							float distance = Vector3.Distance(pos2d, position);

							if (IsPointInsideSector(position with { Z = 0 }, pos2d, direction, angleDegrees, range))
							{
								if (distance > minRange)
								{
									foreach (StreetNodeConnected streetNodeConnected in point.ConnectedNodes)
									{
										if (streetNodeConnected.LaneCountForward != 0)
										{
											options.Add(new StreetNodeOption(point, streetNodeConnected));
										}
									}
								}
							}
						}
					}
				}
			}

			if (options.Count == 0)
				return null;

			StreetNodeOption nodeOption = m_randomProvider.Choice(options);
			return nodeOption;
		}

		public ScenarioPoint GetRandomScenarioPointInRange(Vector3 position, float range, float minRange = 0.0f)
		{
			position.Z = 0.0f;
			const int cellSize = 100;

			float tlx = position.X - range;
			float tly = position.Y - range;

			float brx = position.X + range;
			float bry = position.Y + range;

			List<ScenarioPoint> options = new();

			for (int i = (int)(tlx / cellSize); i < (int)(brx / cellSize) + 1; ++i)
			{
				for (int j = (int)(tly / cellSize); j < (int)(bry / cellSize) + 1; ++j)
				{
					var coord = new CellCoord(i, j);
					if (m_scenarioMap.TryGetValue(coord, out List<ScenarioPoint> value))
					{
						foreach (var point in value)
						{
							Vector3 pos2d = new(point.Position.X, point.Position.Y, 0);
							float distance = Vector3.Distance(pos2d, position);

							if (distance < range && distance > minRange)
								options.Add(point);
						}
					}
				}
			}

			if (options.Count == 0)
				return null;

			ScenarioPoint scenarioPoint = m_randomProvider.Choice(options);
			return scenarioPoint;
		}

		public ScenarioPoint GetRandomScenarioPointInSector(Vector3 position, Vector3 direction, float angleDegrees, float range, float minRange = 0.0f)
		{
			position.Z = 0.0f;
			const int cellSize = 100;

			float tlx = position.X - range;
			float tly = position.Y - range;

			float brx = position.X + range;
			float bry = position.Y + range;

			List<ScenarioPoint> options = new();

			for (int i = (int)(tlx / cellSize); i < (int)(brx / cellSize) + 1; ++i)
			{
				for (int j = (int)(tly / cellSize); j < (int)(bry / cellSize) + 1; ++j)
				{
					var coord = new CellCoord(i, j);
					if (m_scenarioMap.TryGetValue(coord, out List<ScenarioPoint> value))
					{
						foreach (var point in value)
						{
							Vector3 pos2d = new(point.Position.X, point.Position.Y, 0);
							float distance = Vector3.Distance(pos2d, position);

							if (IsPointInsideSector(position with { Z = 0 }, pos2d, direction, angleDegrees, range))
							{
								if (distance > minRange)
								{
									options.Add(point);
								}
							}
						}
					}
				}
			}

			if (options.Count == 0)
				return null;

			ScenarioPoint scenarioPoint = m_randomProvider.Choice(options);
			return scenarioPoint;
		}

		public uint GetRandomPedModelByPosition(Vector3 position)
		{
			Zone zone = GetZoneByPosition(position);
			if (zone == null)
			{
				// Fallback for positions which are not inside any zone. like 1368.424 -881.748 13.843
				return (uint)PedModel.Downtown01AMY;
			}

			PopSchedule schedule = ZoneSchedules[zone.MpName];
			// TODO: choose based on time manager. Taking 12-14 for now
			PopScheduleEntry chosenScheduleEntry = schedule.Entries[7];

			float prob = m_randomProvider.GetFloat();
			float accumulatedProb = 0.0f;

			string chosenGroup = chosenScheduleEntry.PedGroupProbsSorted[0].Item1;
			for (int i = 0; i < chosenScheduleEntry.PedGroupProbsSorted.Count; ++i)
			{
				if (prob < accumulatedProb)
				{
					chosenGroup = chosenScheduleEntry.PedGroupProbsSorted[i].Item1;
					break;
				}

				accumulatedProb += chosenScheduleEntry.PedGroupProbsSorted[i].Item2;
			}

			string chosenModel = m_randomProvider.Choice(PedGroups[chosenGroup]);
			return Alt.Hash(chosenModel);
		}

		public uint GetRandomVehicleModelByPosition(Vector3 position, bool excludeProhibitedAmbient, bool excludeProhibitedCarGen)
		{
			Zone zone = GetZoneByPosition(position);
			if (zone == null)
			{
				// Fallback for positions which are not inside any zone. like 1368.424 -881.748 13.843
				return (uint)VehicleModel.Asea;
			}

			PopSchedule schedule = ZoneSchedules[zone.MpName];
			// TODO: choose based on time manager. Taking 12-14 for now
			PopScheduleEntry chosenScheduleEntry = schedule.Entries[7];

			float prob = m_randomProvider.GetFloat();
			float accumulatedProb = 0.0f;

			string chosenGroup = chosenScheduleEntry.VehGroupProbsSorted[0].Item1;
			for (int i = 0; i < chosenScheduleEntry.VehGroupProbsSorted.Count; ++i)
			{
				if (prob < accumulatedProb)
				{
					chosenGroup = chosenScheduleEntry.VehGroupProbsSorted[i].Item1;
					break;
				}

				accumulatedProb += chosenScheduleEntry.VehGroupProbsSorted[i].Item2;
			}

			// TODO: generate precise list of what can be spawned instead of checking on the fly
			string chosenModel = m_randomProvider.Choice(VehGroups[chosenGroup]);
			if (excludeProhibitedCarGen)
			{
				if (m_carGenProhibitedModels.Contains(chosenModel))
				{
					chosenModel = "asea";
				}
			}

			if (excludeProhibitedAmbient)
			{
				if (m_ambientCarProhibitedModels.Contains(chosenModel))
				{
					chosenModel = "asea";
				}
			}


			// Quick fix for boats
			// TODO: Proper searching for spawn nodes for boats
			VehicleModelInfo info = Alt.GetVehicleModelInfo(chosenModel);
			if (info.Type == VehicleModelType.BOAT)
			{
				chosenModel = "asea";
			}

			return Alt.Hash(chosenModel);
		}

		public (byte, byte) GetRandomCarColor(uint vehicleModelHash)
		{
			if (!m_colorlessCars.Contains(vehicleModelHash))
			{
				if (m_carColorsNum.Count != 0)
				{
					byte color1 = (byte)m_randomProvider.Choice(m_carColorsNum);
					byte color2 = (byte)m_randomProvider.Choice(m_carColorsNum);
					return (color1, color2);
				}
			}

			return (0, 0);
		}

		public CarGenerator GetRandomCarGenInRange(Vector3 position, float range, float minRange = 0.0f)
		{
			position.Z = 0.0f;
			const int cellSize = 100;

			float tlx = position.X - range;
			float tly = position.Y - range;

			float brx = position.X + range;
			float bry = position.Y + range;

			List<CarGenerator> options = new();

			for (int i = (int)(tlx / cellSize); i < (int)(brx / cellSize) + 1; ++i)
			{
				for (int j = (int)(tly / cellSize); j < (int)(bry / cellSize) + 1; ++j)
				{
					var coord = new CellCoord(i, j);
					if (m_carGeneratorsGrid.TryGetValue(coord, out List<CarGenerator> value))
					{
						foreach (CarGenerator point in value)
						{
							Vector3 pos2d = point.Position with { Z = 0 };
							float distance = Vector3.Distance(pos2d, position);

							if (distance < range && distance > minRange)
								options.Add(point);
						}
					}
				}
			}

			if (options.Count == 0)
				return null;

			CarGenerator carGen = m_randomProvider.Choice(options);
			return carGen;
		}

		public CarGenerator GetRandomCarGenInSector(Vector3 position, Vector3 direction, float angleDegrees, float range, float minRange)
		{
			position.Z = 0.0f;
			const int cellSize = 100;

			float tlx = position.X - range;
			float tly = position.Y - range;

			float brx = position.X + range;
			float bry = position.Y + range;

			List<CarGenerator> options = new();

			for (int i = (int)(tlx / cellSize); i < (int)(brx / cellSize) + 1; ++i)
			{
				for (int j = (int)(tly / cellSize); j < (int)(bry / cellSize) + 1; ++j)
				{
					var coord = new CellCoord(i, j);
					if (m_carGeneratorsGrid.TryGetValue(coord, out List<CarGenerator> value))
					{
						foreach (CarGenerator point in value)
						{
							Vector3 pos2d = point.Position with { Z = 0 };
							float distance = Vector3.Distance(pos2d, position);

							if (IsPointInsideSector(position with { Z = 0 }, pos2d, direction, angleDegrees, range))
							{
								if (distance > minRange)
									options.Add(point);
							}
						}
					}
				}
			}

			if (options.Count == 0)
				return null;

			CarGenerator carGen = m_randomProvider.Choice(options);
			return carGen;
		}

		public Vector3? GetRandomFootpathPointInRange(Position position, float range, float minRange = 0.0f)
		{
			position.Z = 0.0f;
			const int cellSize = 100;

			float tlx = position.X - range;
			float tly = position.Y - range;

			float brx = position.X + range;
			float bry = position.Y + range;

			List<NavigationMeshPolyFootpath> options = new();

			for (int i = (int)(tlx / cellSize); i < (int)(brx / cellSize) + 1; ++i)
			{
				for (int j = (int)(tly / cellSize); j < (int)(bry / cellSize) + 1; ++j)
				{
					var coord = new CellCoord(i, j);
					if (m_navigationMeshProvider.FootpathPolygons.TryGetValue(coord, out List<NavigationMeshPolyFootpath> value))
					{
						foreach (NavigationMeshPolyFootpath polyFootpath in value)
						{
							options.Add(polyFootpath);
						}
					}
				}
			}

			if (options.Count == 0)
				return null;

			NavigationMeshPolyFootpath selectedPolygon = m_randomProvider.Choice(options);

			while (selectedPolygon.Vertices.Count <= 2)
			{
				selectedPolygon = m_randomProvider.Choice(options);
			}

			// TODO: select random triangle, not only 0-2 vertices
			Vector3 outPosition = NavigationMeshProvider.GetRandomPositionInsideTriangle(
				(Vector3)selectedPolygon.Vertices[0],
				(Vector3)selectedPolygon.Vertices[1],
				(Vector3)selectedPolygon.Vertices[2]);

			float distSq = Vector3.DistanceSquared(outPosition, position);
			if (distSq > range * range || distSq < minRange * minRange)
				return null;

			return outPosition;
		}

		public Vector3? GetRandomFootpathPointInSector(Vector3 position, Vector3 direction, float angleDegrees, float range, float minRange = 0.0f)
		{
			position.Z = 0.0f;
			const int cellSize = 100;

			float tlx = position.X - range;
			float tly = position.Y - range;

			float brx = position.X + range;
			float bry = position.Y + range;

			List<NavigationMeshPolyFootpath> options = new();

			for (int i = (int)(tlx / cellSize); i < (int)(brx / cellSize) + 1; ++i)
			{
				for (int j = (int)(tly / cellSize); j < (int)(bry / cellSize) + 1; ++j)
				{
					var coord = new CellCoord(i, j);
					if (m_navigationMeshProvider.FootpathPolygons.TryGetValue(coord, out List<NavigationMeshPolyFootpath> value))
					{
						foreach (NavigationMeshPolyFootpath polyFootpath in value)
						{
							// TODO: center of polygon instead?
							Vector3 pos2d = new(polyFootpath.Vertices[0].X, polyFootpath.Vertices[0].Y, 0);
							if (IsPointInsideSector(position with { Z = 0 }, pos2d, direction, angleDegrees, range))
							{
								options.Add(polyFootpath);
							}
						}
					}
				}
			}

			if (options.Count == 0)
				return null;

			NavigationMeshPolyFootpath selectedPolygon = m_randomProvider.Choice(options);

			while (selectedPolygon.Vertices.Count <= 2)
			{
				selectedPolygon = m_randomProvider.Choice(options);
			}

			// TODO: select random triangle, not only 0-2 vertices
			Vector3 outPosition = NavigationMeshProvider.GetRandomPositionInsideTriangle(
				(Vector3)selectedPolygon.Vertices[0],
				(Vector3)selectedPolygon.Vertices[1],
				(Vector3)selectedPolygon.Vertices[2]);

			float distSq = Vector3.DistanceSquared(outPosition, position);
			if (distSq > range * range || distSq < minRange * minRange)
				return null;

			return outPosition;
		}
	}
}
