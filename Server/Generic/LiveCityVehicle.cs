// Licensed to b2soft under the MIT license

using System;
using AltV.Net;
using AltV.Net.Async.Elements.Entities;
using AltV.Net.Elements.Entities;

namespace LiveCity.Server.Generic
{
	public class LiveCityVehicleFactory : IEntityFactory<LiveCityVehicle>
	{
		public LiveCityVehicle Create(ICore core, IntPtr entityPointer, uint id)
		{
			return new LiveCityVehicle(core, entityPointer, id);
		}
	}

	public class LiveCityVehicle : AsyncVehicle
	{
		public IBlip Blip { get; set; }

		public LiveCityVehicle(ICore core, IntPtr nativePointer, uint id) : base(core, nativePointer, id)
		{
		}
	}
}
