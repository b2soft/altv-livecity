// Licensed to b2soft under the MIT license

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace LiveCity.Shared
{
	public static class LiveCityServiceProvider
	{
		private static IServiceProvider s_serviceProvider;

		public static void RegisterServices()
		{
			var serviceCollection = new ServiceCollection();

			var baseType = typeof(ILiveCityService);
			var serviceTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).Where(t => t != baseType && baseType.IsAssignableFrom(t) && !t.IsGenericType);

			foreach (var t in serviceTypes)
			{
				serviceCollection.AddSingleton(t);
			}

			s_serviceProvider = serviceCollection.BuildServiceProvider();

			var singletonTypes = serviceCollection.Where(s => s.Lifetime == ServiceLifetime.Singleton).Select(s => s.ServiceType);
			foreach (var type in singletonTypes)
				s_serviceProvider.GetRequiredService(type);
		}

		public static IServiceProvider GetServiceProvider()
		{
			return s_serviceProvider;
		}
	}


}
