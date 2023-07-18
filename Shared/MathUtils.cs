// Licensed to b2soft under the MIT license

using System;
using System.Numerics;

namespace LiveCity.Shared
{
	public static class MathUtils
	{
		public static float ToRadians(float x)
		{
			return x * MathF.PI / 180.0f;
		}

		public static double ToRadians(double x)
		{
			return x * Math.PI / 180.0;
		}

		public static float ToDegrees(float x)
		{
			return x * 180.0f / MathF.PI;
		}

		public static double ToDegrees(double x)
		{
			return x * 180.0 / Math.PI;
		}
		public static Vector3 GetForwardVector(Vector3 rotation)
		{
			// Roll Pitch Yaw
			float z = rotation.Z;
			float x = rotation.X;
			double num = Math.Abs(Math.Cos(x));

			return new Vector3(
				(float)(-Math.Sin(z) * num),
				(float)(Math.Cos(z) * num),
				(float)Math.Sin(x)
			);
		}
	}
}
