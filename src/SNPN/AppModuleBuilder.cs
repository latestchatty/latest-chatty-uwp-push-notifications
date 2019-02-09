using System;
using System.IO;
using System.Net.Http;
using Autofac;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Formatting.Compact;
using SNPN.Common;
using SNPN.Data;
using SNPN.Monitor;
using SNPN.WebService;

namespace SNPN
{
	public static class AppModuleBuilder
	{
        // ReSharper disable once InconsistentNaming
        private static readonly Lazy<IContainer> container = new Lazy<IContainer>(BuildContainer);
		public static IContainer Container => container.Value;

		private static IContainer BuildContainer()
		{
			var builder = new ContainerBuilder();
			builder.RegisterType<NotificationService>().As<INotificationService>().SingleInstance();
			builder.RegisterType<AccessTokenManager>().SingleInstance();
			builder.RegisterType<Monitor.Monitor>().SingleInstance();
			builder.Register(x =>
			{
				var configBuilder = new ConfigurationBuilder()
					.AddJsonFile("appsettings.json", true)
					.AddEnvironmentVariables()
					.SetBasePath(Directory.GetCurrentDirectory());

				return configBuilder.Build();
			}).SingleInstance();
			builder.Register(x =>
			{
				var config = x.Resolve<IConfigurationRoot>();
				var appConfig = new AppConfiguration();
				config.Bind(appConfig);
				return appConfig;
			}).SingleInstance();
			builder.Register<ILogger>(x =>
			{
				return new LoggerConfiguration()
					.MinimumLevel.Verbose()
					.WriteTo.Console(new CompactJsonFormatter())
					.CreateLogger();
			});
			builder.Register(x => new MemoryCache(new MemoryCacheOptions())).SingleInstance();
			builder.RegisterType<UserRepo>().As<IUserRepo>().InstancePerDependency();
			builder.RegisterType<NewEventHandler>().InstancePerDependency();
			builder.Register<Func<NewEventHandler>>(c =>
			{
				var ctx = c.Resolve<IComponentContext>();
				return () => ctx.Resolve<NewEventHandler>();
			});
			builder.RegisterType<HttpClientHandler>().As<HttpMessageHandler>().SingleInstance();
			builder.RegisterType<NetworkService>().As<INetworkService>().SingleInstance();
			builder.RegisterType<TileContentRepo>().InstancePerDependency();
			return builder.Build();
		}
	}
}
