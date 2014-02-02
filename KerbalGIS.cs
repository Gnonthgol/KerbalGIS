using System;
using UnityEngine;

namespace KerbalGIS
{
	[KSPAddon(KSPAddon.Startup.EveryScene, false)]
	public class KerbalGIS : MonoBehaviour
	{
		HTTPServer server = null;

		public void Update ()
		{
			if (server == null) {
				server = new HTTPServer ();
			}
			server.Update ();
		}

		public void OnDestroy ()
		{
			server.Stop ();
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

