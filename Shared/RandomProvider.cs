// Licensed to b2soft under the MIT license

using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveCity.Shared
{
	public class RandomProvider : ILiveCityService
	{
		private readonly Random m_random = new();

		public float GetFloat()
		{
			return m_random.NextSingle();
		}

		public double GetDouble()
		{
			return m_random.NextDouble();
		}

		public int GetInt()
		{
			return m_random.Next();
		}

		public int GetInt(int maxValue)
		{
			return m_random.Next(maxValue);
		}

		public int GetInt(int minValue, int maxValue)
		{
			return m_random.Next(minValue, maxValue);
		}

		public T Choice<T>(ICollection<T> source)
		{
			return source.ElementAt(m_random.Next(source.Count));
		}
	}
}
