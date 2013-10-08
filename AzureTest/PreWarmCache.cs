using System.Web.Hosting;

namespace AzureTest
{
	public class PreWarmCache : IProcessHostPreloadClient
	{
		public void Preload(string[] parameters)
		{
			EventLogger.LogEvent("Preload initiated");
		}
	}
}