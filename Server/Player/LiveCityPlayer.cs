// Licensed to b2soft under the MIT license

using System;
using AltV.Net;
using AltV.Net.Async.Elements.Entities;

namespace LiveCity.Server.Player
{
	internal class LiveCityPlayer : AsyncPlayer
	{
		public bool SpawnCompleted { get; set; }
		public LiveCityPlayer(ICore core, IntPtr nativePointer, uint id) : base(core, nativePointer, id) { }
	}
}
