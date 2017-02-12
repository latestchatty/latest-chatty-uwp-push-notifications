using Microsoft.AspNetCore.Builder;
//using Microsoft.AspNetCore.Hosting;
using Nancy.Owin;

namespace SNPN
{
	public class Startup
	{
		//public IConfiguration Configuration { get; set; }

		//public Startup(IHostingEnvironment env)
		//{ }

		public void Configure(IApplicationBuilder app)
		{
			app.UseOwin(x => x.UseNancy());
		}
	}
}