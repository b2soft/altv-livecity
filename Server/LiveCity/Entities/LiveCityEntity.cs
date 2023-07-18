// Licensed to b2soft under the MIT license

using System.Numerics;
using LiveCity.Server.Generic;

namespace LiveCity.Server.LiveCity.Entities
{
	internal abstract class LiveCityEntity
	{
		public ELiveCityState State { get; set; } = ELiveCityState.JustCreated;
		public abstract bool Exists();
		public virtual void Destroy()
		{
			State = ELiveCityState.Destroying;
		}
		public abstract bool IsInRangeSquared(Vector3 position, float rangeSquared);
		public abstract Vector3 GetPosition();
	}
}
