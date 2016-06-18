using Autofac;
using Nancy.Bootstrappers.Autofac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shacknews_Push_Notifications.WebService
{
	public class Bootstrapper : AutofacNancyBootstrapper
	{
		private readonly ILifetimeScope lifetimeScope;

		public Bootstrapper(ILifetimeScope scope)
		{
			this.lifetimeScope = scope;
		}

		protected override ILifetimeScope GetApplicationContainer()
		{
			return this.lifetimeScope;
		}
	}
}
