// Licensed to b2soft under the MIT license

using System;
using AltV.Net;
using AltV.Net.Elements.Entities;

namespace LiveCity.Server.Player
{
	internal class LiveCityPlayerFactory : IEntityFactory<IPlayer>
	{
		public IPlayer Create(ICore core, IntPtr playerPointer, uint id)
		{
			return new LiveCityPlayer(core, playerPointer, id);
		}
	}
}
