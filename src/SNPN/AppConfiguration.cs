namespace SNPN
{
	public class AppConfiguration
	{
		public string DbLocation { get; set;} = "";
		public string NotificationSid { get; set; } = "";
		public string ClientSecret { get; set; } = "";
		public string WinchattyApiBase { get; set; } = "https://winchatty.com/v2/";
		public string HostUrl { get; set; } = "http://0.0.0.0:4000";
		public int MaxNotificationBodyLength { get; set; } = 350;
	}
}