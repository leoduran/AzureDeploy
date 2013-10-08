using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Web.Administration;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace AzureTest
{
	public class WebRole : RoleEntryPoint {
		public override bool OnStart() {
			// For information on handling configuration changes
			// see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

			EventLogger.LogEvent("WebRole on start initiated");

			using (var serverManager = new ServerManager()) {
				var config = serverManager.GetApplicationHostConfiguration();

				var serviceAutoStartProvidersSection = config.GetSection("system.applicationHost/serviceAutoStartProviders");

				var serviceAutoStartProvidersCollection = serviceAutoStartProvidersSection.GetCollection();

				var addAutoStartProviderElementExists = FindElement(serviceAutoStartProvidersCollection, "add", "name", "PreWarmMyCache") != null;

				if (!addAutoStartProviderElementExists) {
					var addAutoStartProvidersElement = serviceAutoStartProvidersCollection.CreateElement("add");
					addAutoStartProvidersElement["name"] = @"PreWarmMyCache";
					addAutoStartProvidersElement["type"] = @"AzureTest.PreWarmCache, AzureTest";
					serviceAutoStartProvidersCollection.Add(addAutoStartProvidersElement);
				}

				var applicationPoolsSection = config.GetSection("system.applicationHost/applicationPools");

				var applicationPoolDefaultsElement = applicationPoolsSection.GetChildElement("applicationPoolDefaults");
				applicationPoolDefaultsElement["startMode"] = @"AlwaysRunning";

				var recyclingElement = applicationPoolDefaultsElement.GetChildElement("recycling");

				var periodicRestartElement = recyclingElement.GetChildElement("periodicRestart");
				periodicRestartElement["time"] = TimeSpan.Parse("00:00:00");

				var scheduleCollection = periodicRestartElement.GetCollection("schedule");

				var scheduleAddElementExists = FindElement(scheduleCollection, "add") != null;

				if (!scheduleAddElementExists) {
					var addElement = scheduleCollection.CreateElement("add");
					addElement["value"] = TimeSpan.Parse("11:00:00");

					scheduleCollection.Add(addElement);
				}

				var sitesSection = config.GetSection("system.applicationHost/sites");

				var sitesCollection = sitesSection.GetCollection();

				foreach (var site in serverManager.Sites) {
					var siteElement = FindElement(sitesCollection, "site", "name", site.Name);
					if (siteElement == null) {
						throw new InvalidOperationException("Element not found!");
					}

					var applicationDefaultsElement = siteElement.GetChildElement("applicationDefaults");
					applicationDefaultsElement["serviceAutoStartEnabled"] = true;
					applicationDefaultsElement["serviceAutoStartProvider"] = @"PreWarmMyCache";

					// start idle timeout
					try {
						var siteApplication = site.Applications.First();
						var appPoolName = siteApplication.ApplicationPoolName;
						var appPool = serverManager.ApplicationPools[appPoolName];

						appPool.ProcessModel.IdleTimeout = TimeSpan.FromHours(0);
					}
					catch (Exception) {
						EventLogger.LogEvent("Unable to set idle timeout");
					}

				}

				serverManager.CommitChanges();
			}

			return base.OnStart();
		}

		private ConfigurationElement FindElement(IEnumerable<ConfigurationElement> collection, string elementTagName, params string[] keyValues) {
			foreach (var element in collection) {
				if (String.Equals(element.ElementTagName, elementTagName, StringComparison.OrdinalIgnoreCase)) {
					bool matches = true;

					for (int i = 0; i < keyValues.Length; i += 2) {
						object o = element.GetAttributeValue(keyValues[i]);
						string value = null;
						if (o != null) {
							value = o.ToString();
						}

						if (!String.Equals(value, keyValues[i + 1], StringComparison.OrdinalIgnoreCase)) {
							matches = false;
							break;
						}
					}
					if (matches) {
						return element;
					}
				}
			}

			return null;
		}
	}



	public class EventLogger
	{
		public static void LogEvent(string message)
		{
			const string source = "SurveyWebsite";
			const string log = "Application";

			try
			{
				if (!EventLog.SourceExists(source))
				{
					EventLog.CreateEventSource(source, log);
				}

				EventLog.WriteEntry(source, message);
			}
			catch(Exception){}
		}
	}
}