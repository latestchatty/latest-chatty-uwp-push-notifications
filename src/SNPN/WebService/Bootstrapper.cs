using Autofac;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.TinyIoc;
using Serilog;
using System;

namespace SNPN.WebService
{
	public class Bootstrapper : DefaultNancyBootstrapper
	{
		ILogger _logger;

		protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
		{
			base.ApplicationStartup(container, pipelines);
			_logger = AppModuleBuilder.Container.Resolve<ILogger>();
		}
		protected override void RequestStartup(TinyIoCContainer container, IPipelines pipelines, NancyContext context)
		{
			pipelines.OnError.AddItemToEndOfPipeline((c, ex) =>
			{
				var guid = Guid.NewGuid();
				_logger.Error(ex, "{guid} Exception in url {url} ", guid.ToString(), c.Request.Url);
				return new { status = "error", exceptionid = guid.ToString() };
			});
			base.RequestStartup(container, pipelines, context);
		}
	}
}