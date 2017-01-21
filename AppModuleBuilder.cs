using Autofac;
using Shacknews_Push_Notifications.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.IO;
using Microsoft.Extensions.Caching.Memory;

namespace Shacknews_Push_Notifications
{
	public class AppModuleBuilder
	{
		private static Lazy<IContainer> container = new Lazy<IContainer>(() => BuildContainer());
		public static IContainer Container { get { return container.Value; } }
		private static IContainer BuildContainer()
		{
			var builder = new ContainerBuilder();
			builder.RegisterType<NotificationService>().SingleInstance();
			builder.RegisterType<AccessTokenManager>().SingleInstance();
			builder.RegisterType<DatabaseService>().InstancePerDependency();
			builder.RegisterType<Monitor>().SingleInstance();
			builder.Register<AppConfiguration>(x =>
			{
				var configBuilder = new ConfigurationBuilder()
												.AddJsonFile("appsettings.json")
												.SetBasePath(Directory.GetCurrentDirectory());

				var config = configBuilder.Build();
				var appConfig = new AppConfiguration();
				ConfigurationBinder.Bind(config, appConfig);
				return appConfig;
			}).SingleInstance();
			builder.Register<MemoryCache>(x => new MemoryCache(new MemoryCacheOptions())).SingleInstance();
			return builder.Build();
		}
	}
}
