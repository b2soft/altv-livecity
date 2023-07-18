// Licensed to b2soft under the MIT license

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using AltV.Net;
using LiveCity.Shared;
using MessagePack;

namespace LiveCity.Server.Navigation
{
	[MessagePackObject]
	public class NavigationMesh
	{
		[Key(0)] public int AreaId { get; set; }

		[Key(1)] public int CellX { get; set; }

		[Key(2)] public int CellY { get; set; }

		[Key(3)] public List<NavigationMeshPoly> Polygons { get; set; }
	}

	[MessagePackObject]
	public class NavigationMeshPoly
	{
		[Key(0)] public int Index { get; set; }

		[Key(1)] public int PartId { get; set; }

		[Key(2)] public bool IsFootpath { get; set; }

		[Key(3)] public bool IsUnderground { get; set; }

		[Key(4)] public bool IsSteepSlope { get; set; }

		[Key(5)] public bool IsWater { get; set; }

		[Key(6)] public bool HasPathNode { get; set; }

		[Key(7)] public bool IsInterior { get; set; }

		[Key(8)] public bool IsFlatGround { get; set; }

		[Key(9)] public bool IsRoad { get; set; }

		[Key(10)] public bool IsCellEdge { get; set; }

		[Key(11)] public bool IsTrainTrack { get; set; }

		[Key(12)] public bool IsShallowWater { get; set; }

		[Key(13)] public bool IsFootpathUnk1 { get; set; }

		[Key(14)] public bool IsFootpathUnk2 { get; set; }

		[Key(15)] public bool IsFootpathMall { get; set; }

		[Key(16)] public bool IsSlopeSouth { get; set; }

		[Key(17)] public bool IsSlopeSouthEast { get; set; }

		[Key(18)] public bool IsSlopeEast { get; set; }

		[Key(19)] public bool IsSlopeNorthEast { get; set; }

		[Key(20)] public bool IsSlopeNorth { get; set; }

		[Key(21)] public bool IsSlopeNorthWest { get; set; }

		[Key(22)] public bool IsSlopeWest { get; set; }

		[Key(23)] public bool IsSlopeSouthWest { get; set; }

		[Key(24)] public int UnkX { get; set; }

		[Key(25)] public int UnkY { get; set; }

		[Key(26)] public WorldVector3 Position { get; set; }

		[Key(27)] public List<WorldVector3> Vertices { get; set; }

		[Key(28)] public List<NavigationMeshPolyEdge> Edges { get; set; }
	}

	[MessagePackObject]
	public class NavigationMeshPolyEdge
	{
		[Key(0)] public uint AreaId { get; set; }

		[Key(1)] public uint PolyIndex { get; set; }
	}

	[MessagePackObject]
	public class WorldVector3
	{
		[Key(0)] public float X { get; set; }

		[Key(1)] public float Y { get; set; }

		[Key(2)] public float Z { get; set; }

		public static explicit operator Vector3(WorldVector3 v)
		{
			return new Vector3(v.X, v.Y, v.Z);
		}
	}

	[MessagePackObject]
	public class NavigationMeshPolyFootpath
	{
		[Key(0)] public int Index { get; set; }

		[Key(1)] public int AreaId { get; set; }

		[Key(2)] public int CellX { get; set; }

		[Key(3)] public int CellY { get; set; }

		[Key(4)] public List<WorldVector3> Vertices { get; set; }
	}

	struct CellCoord
	{
		public CellCoord(int x, int y)
		{
			X = x;
			Y = y;
		}

		public int X;
		public int Y;
	}

	internal class NavigationMeshProvider : ILiveCityService
	{
		public readonly Dictionary<CellCoord, List<NavigationMeshPolyFootpath>> FootpathPolygons = new();

		public static Vector3 GetRandomPositionInsideTriangle(Vector3 a, Vector3 b, Vector3 c)
		{
			Random random = new();
			float r1 = random.NextSingle();
			float r2 = random.NextSingle();

			Vector3 p = Vector3.Multiply(a, 1.0f - MathF.Sqrt(r1)) + Vector3.Multiply(b, MathF.Sqrt(r1) * (1.0f - r2)) +
						Vector3.Multiply(c, MathF.Sqrt(r1) * r2);
			return p;
		}

		public NavigationMeshProvider(RandomProvider randomProvider)
		{
			//ExtractFootpathFromNavMesh();
			//	return;

			List<NavigationMeshPolyFootpath> allFootpaths = LoadDataFromDumpFile<List<NavigationMeshPolyFootpath>>("footpath.msgpack");

			foreach (NavigationMeshPolyFootpath navigationMeshPolyFootpath in allFootpaths)
			{
				CellCoord key = new() { X = navigationMeshPolyFootpath.CellX, Y = navigationMeshPolyFootpath.CellY };
				;
				if (!FootpathPolygons.ContainsKey(key))
				{
					FootpathPolygons.Add(key, new List<NavigationMeshPolyFootpath>());
				}

				FootpathPolygons[key].Add(navigationMeshPolyFootpath);
			}
		}

		private static void ExtractFootpathFromNavMesh()
		{
			List<NavigationMeshPolyFootpath> footpathPolygons = new();

			List<NavigationMesh> navigationMeshes =
				LoadDataFromDumpFile<List<NavigationMesh>>("navigationMeshes.msgpack");

			foreach (NavigationMesh navigationMesh in navigationMeshes)
			{
				foreach (NavigationMeshPoly navigationMeshPolygon in navigationMesh.Polygons)
				{
					if (navigationMeshPolygon.IsFootpath)
					{
						NavigationMeshPolyFootpath navigationMeshPolyFootpath = new()
						{
							AreaId = navigationMesh.AreaId,
							Index = navigationMeshPolygon.Index,
							CellX = (int)(navigationMeshPolygon.Position.X / 100),
							CellY = (int)(navigationMeshPolygon.Position.Y / 100),
							Vertices = navigationMeshPolygon.Vertices
						};

						footpathPolygons.Add(navigationMeshPolyFootpath);
					}
				}
			}

			string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "footpath.msgpack");
			File.WriteAllBytes(filePath, MessagePackSerializer.Serialize(footpathPolygons));
		}

		public static TDumpType LoadDataFromDumpFile<TDumpType>(string dumpFileName)
			where TDumpType : new()
		{
			TDumpType dumpResult = default;
			string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", dumpFileName);
			if (!File.Exists(filePath))
			{
				Alt.Log($"Could not find dump file at {filePath}");
				return default;
			}

			try
			{
				MessagePackSerializerOptions lz4Options =
					MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
				dumpResult = MessagePackSerializer.Deserialize<TDumpType>(File.ReadAllBytes(filePath), lz4Options);
				Alt.Log($"Successfully loaded dump file {dumpFileName}.");
			}
			catch (Exception e)
			{
				Alt.Log($"Failed loading dump: {e}");
			}

			return dumpResult;
		}

		public static TDumpType LoadDataFromJsonFile<TDumpType>(string dumpFileName)
			where TDumpType : new()
		{
			TDumpType dumpResult = default;
			string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", dumpFileName);
			if (!File.Exists(filePath))
			{
				Alt.Log($"Could not find dump file at {filePath}");
				return default;
			}

			try
			{
				dumpResult = JsonSerializer.Deserialize<TDumpType>(File.ReadAllText(filePath));
				Alt.Log($"Successfully loaded dump file {dumpFileName}.");
			}
			catch (Exception e)
			{
				Alt.Log($"Failed loading dump: {e}");
			}

			return dumpResult;
		}
	}
}
