// Licensed to b2soft under the MIT license

using System;
using AltV.Net;
using AltV.Net.Async.Elements.Entities;
using AltV.Net.Elements.Entities;

namespace LiveCity.Server.Generic
{
	public class LiveCityPedFactory : IEntityFactory<LiveCityPed>
	{
		public LiveCityPed Create(ICore core, IntPtr entityPointer, uint id)
		{
			return new LiveCityPed(core, entityPointer, id);
		}
	}

	public enum ELiveCityState
	{
		JustCreated = 0,
		Active,
		Destroying
	}

	public class LiveCityPed : AsyncPed
	{
		public IBlip Blip { get; set; }

		public LiveCityPed(ICore core, IntPtr nativePointer, uint id) : base(core, nativePointer, id)
		{
		}
	}
}
