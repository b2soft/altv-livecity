// Licensed to b2soft under the MIT license

namespace LiveCity.Shared
{
	public static class GlobalConfig
	{
		public const bool EnableLiveCity = true;
		public const bool EnableNavigationDataLoad = true;

		public const float StreamingRange = 400.0f;
		public const float StreamingRangeSquared = StreamingRange * StreamingRange;

		public static class LiveCity
		{
			public const int EntityBudget = 100;

			public const int ParkedVehiclesBudget = 20;
			public const int WanderVehiclesBudget = 26;
			public const int WanderPedsBudget = 12;
			public const int ScenarioPedsBudget = 16;

			public const float CloseRange = 200.0f;
			public const float CloseRangeSquared = CloseRange * CloseRange;
			public const float MinimumRange = 100.0f;
			public const float FarSectorHalfAngle = 45.0f;
		}
	}
}
