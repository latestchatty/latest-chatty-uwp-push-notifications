using System;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Formatting.Compact;
using SNPN.Common;
using SNPN.Data;
using SNPN.Monitor;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using SNPN.Controllers;

//using Microsoft.AspNetCore.Hosting;

namespace SNPN
{
	public class Startup
	{

		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
			services.AddSingleton<INotificationService, NotificationService>();
			services.AddSingleton<AccessTokenManager>();
			services.AddSingleton<Monitor.Monitor>();
			services.AddSingleton(x =>
			{
				var configBuilder = new ConfigurationBuilder()
					.AddJsonFile("appsettings.json", false)
					.AddEnvironmentVariables()
					.SetBasePath(Directory.GetCurrentDirectory());

				return configBuilder.Build();
			});
			services.AddSingleton(x =>
			{
				var config = x.GetService<IConfigurationRoot>();
				var appConfig = new AppConfiguration();
				config.Bind(appConfig);
				return appConfig;
			});
			services.AddSingleton<ILogger>(x =>
			{
				return new LoggerConfiguration()
					.MinimumLevel.Verbose()
					.WriteTo.Console(new CompactJsonFormatter())
					.CreateLogger();
			});
			services.AddSingleton(x => new MemoryCache(new MemoryCacheOptions()));
			services.AddSingleton<DbHelper>();
			services.AddSingleton<IUserRepo, UserRepo>();
			services.AddScoped<NewEventHandler>();
			services.AddScoped<Func<NewEventHandler>>(c =>
			{
				return () => c.GetService<NewEventHandler>();
			});
			services.AddSingleton(x => new HttpClient());
			services.AddSingleton<INetworkService, NetworkService>();
			services.AddScoped<TileContentRepo>();
			// services.AddSingleton<IDateTime, SystemDateTime>();

			// services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}
			else
			{
				app.UseExceptionHandler("/error/500");
			}

			app.UseMvc();
		}
	}
}