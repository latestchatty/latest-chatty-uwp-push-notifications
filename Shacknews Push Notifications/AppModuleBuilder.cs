using Autofac;
using Shacknews_Push_Notifications.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shacknews_Push_Notifications
{
	public class AppModuleBuilder
	{
		public IContainer BuilderContainer()
		{
			var builder = new ContainerBuilder();
			builder.RegisterType<NotificationService>().InstancePerDependency();
			builder.RegisterType<AccessTokenManager>().SingleInstance();
			builder.RegisterType<DatabaseService>().InstancePerDependency();
			builder.RegisterType<Monitor>().SingleInstance();
			return builder.Build();
		}
	}
}
