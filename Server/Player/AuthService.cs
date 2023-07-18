// Licensed to b2soft under the MIT license

using System.Threading.Tasks;
using AltV.Net.Async;
using AltV.Net.Data;
using AltV.Net.Elements.Entities;
using AltV.Net.Enums;
using LiveCity.Server.LiveCity;
using LiveCity.Shared;

namespace LiveCity.Server.Player
{
	internal class AuthService : ILiveCityService
	{
		private readonly LiveCityService m_liveCityService;

		public AuthService(LiveCityService liveCityService)
		{
			m_liveCityService = liveCityService;
			AltAsync.OnPlayerConnect += OnPlayerConnect;
		}

		private Task OnPlayerConnect(IPlayer player, string reason)
		{
			player.Model = (uint)PedModel.FreemodeMale01;
			player.Spawn(new Position(0, 0, 72));

			m_liveCityService.AddPlayer(player as LiveCityPlayer);

			return Task.CompletedTask;
		}
	}
}
