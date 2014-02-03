using System;
using UnityEngine;
using KSP.IO;

namespace KerbalGIS
{
	[KSPAddon(KSPAddon.Startup.EveryScene, false)]
	public class KerbalGIS : MonoBehaviour
	{
		HTTPServer server = null;
		public static PluginConfiguration config = null;

		public void Update ()
		{
			if (config == null) {
				config = PluginConfiguration.CreateForType<KerbalGIS> ();
				config.load ();
			}
			if (server == null) {
				server = new HTTPServer ();
			}

			server.Update ();
		}

		public void OnDestroy ()
		{
			server.Stop ();
			config.save();
		}

		public static CelestialBody findBody (string name)
		{
			foreach (CelestialBody body in FlightGlobals.Bodies) {
				if (body.GetName () == name)
					return body;
			}
			return null;
		}
	}
}

