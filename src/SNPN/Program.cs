using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Serilog.Events;
using Serilog.Formatting.Compact;
using SNPN.Health;

Log.Logger = new LoggerConfiguration()
			  .Enrich.FromLogContext()
			  .MinimumLevel.Verbose()
			  .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
			  .WriteTo.Console(new CompactJsonFormatter())
			  //.WriteTo.Debug()
			  .CreateLogger();

try
{
	var builder = WebApplication.CreateSlimBuilder(args);

	builder.WebHost.ConfigureKestrel(opts => opts.ListenAnyIP(4000));
	builder.Services.AddSingleton<INotificationService, NotificationService>();
	builder.Services.AddSingleton<AccessTokenManager>();
	builder.Services.AddSingleton<SNPN.Monitor.Monitor>();
	builder.Services.AddSingleton(x =>
	{
		var configBuilder = new ConfigurationBuilder()
			.AddJsonFile("appsettings.json", true)
			.AddEnvironmentVariables()
			.SetBasePath(Directory.GetCurrentDirectory());

		return configBuilder.Build();
	});
	builder.Services.AddSingleton(x =>
	{
		var config = x.GetService<IConfigurationRoot>();
		var appConfig = new AppConfiguration();
		config.Bind(appConfig);
		return appConfig;
	});

	builder.Services.AddSingleton<FirebaseApp>(x =>
	{
		var fcmJSON = Environment.GetEnvironmentVariable("FCM_KEY_JSON");
		if (fcmJSON != null && FirebaseApp.DefaultInstance == null)
		{
			return FirebaseApp.Create(new AppOptions()
			{
				Credential = GoogleCredential.FromJson(fcmJSON)
			});
		}
		return null;
	});

	builder.Services.AddMemoryCache();
	builder.Services.AddSingleton<DbHelper>();
	builder.Services.AddSingleton<IUserRepo, UserRepo>();
	builder.Services.AddScoped<NewEventHandler>();
	builder.Services.AddScoped<Func<NewEventHandler>>(c =>
	{
		return () => c.GetService<NewEventHandler>();
	});
	builder.Services.AddSingleton(x => new HttpClient());
	builder.Services.AddSingleton<INetworkService, NetworkService>();
	builder.Services.AddScoped<TileContentRepo>();
	builder.Services.AddSingleton<VersionHelper>();

	builder.Services.AddSingleton<IDBHealth, DBHealth>();
	builder.Services.AddHealthChecks()
		.AddCheck<Health>("DB Health")
		.AddCheck<WinchattyHealth>("Winchatty health");
	
	builder.Host.UseSerilog();

	builder.Services.AddControllers();

	builder.Logging.ClearProviders();

	var app = builder.Build();

	app.UseSerilogRequestLogging();
	app.MapControllers();
	app.MapHealthChecks("/health");

	if (!app.Environment.IsDevelopment())
	{
		app.UseExceptionHandler("/error/500");
	}

	var monitor = app.Services.GetService(typeof(SNPN.Monitor.Monitor)) as SNPN.Monitor.Monitor;
	var dbHelper = app.Services.GetService(typeof(DbHelper)) as DbHelper;

	dbHelper.GetConnection().Dispose();

	monitor.Start();
	app.Run();

	monitor.Stop();
}
catch (Exception ex)
{
	Log.Logger?.Error(ex, "Unhandled exception in app.");
}
finally
{
	Log.CloseAndFlush();
}