using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Nancy.Owin;

namespace Shacknews_Push_Notifications
{
	public class Startup
	{
		public IConfiguration Configuration { get; set; }

		public Startup(IHostingEnvironment env)
		{ }

		public void Configure(IApplicationBuilder app)
		{
			app.UseOwin(x => x.UseNancy());
		}
	}
}