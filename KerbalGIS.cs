using System;
using System.Net;
using System.Threading;
using System.Reflection;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using KSP.IO;

namespace KerbalGIS
{
	[KSPAddon(KSPAddon.Startup.EveryScene, false)]
	public class KerbalGIS : MonoBehaviour
	{
		HttpListener listener = null;
		static Queue queue = new Queue();
		static Mutex queueMutex = new Mutex();

		public void Update ()
		{
			if (listener == null) {
				listener = new HttpListener ();
				listener.Prefixes.Add ("http://localhost:8080/");
				listener.Start ();
				listener.BeginGetContext (new AsyncCallback (httpCallback), listener);
			}
			if (queueMutex.WaitOne (0)) {
				if (queue.Count > 0) {
					HttpListenerContext context = (HttpListenerContext)queue.Dequeue();
					queueMutex.ReleaseMutex();
					httpHandle (context);
				} else {
					queueMutex.ReleaseMutex();
				}
			}
    	}

		public void OnDestroy() {
			listener.Stop();
			listener = null;
		}

		public static void httpCallback (IAsyncResult result)
		{
			HttpListener listener = (HttpListener) result.AsyncState;
			HttpListenerContext context = listener.EndGetContext(result);
			queueMutex.WaitOne();
			queue.Enqueue(context);
			queueMutex.ReleaseMutex();
			listener.BeginGetContext (new AsyncCallback (httpCallback), listener);
		}

		public static void httpHandle (HttpListenerContext context)
		{
			string request = context.Request.RawUrl;
			Match match = Regex.Match (request, @"^/tile/([A-Za-z]+)/([a-z]+)/(\d+)/(\d+)/(\d+)\.png$", RegexOptions.None);
			if (match.Success) {
				httpTileHandle (context, match.Groups);
				return;
			}

			match = Regex.Match (request, @"^/info/([A-Za-z]+)\.json$", RegexOptions.None);
			if (match.Success) {
				httpBodyInfoHandle (context, match.Groups);
				return;
			}

			match = Regex.Match (request, @"^/info\.json$", RegexOptions.None);
			if (match.Success) {
				httpInfoHandle (context, match.Groups);
				return;
			}

			HttpListenerResponse response = context.Response;
			if (request == "/") request = "/index.html";
			try {
				byte[] ret = File.ReadAllBytes<KerbalGIS> (request);
				response.StatusCode = 200;
				response.Close (ret, false);
			} catch (Exception) {
				response.StatusCode = 404;
				response.Close();
			}
		}

		public static void httpBodyInfoHandle (HttpListenerContext context, GroupCollection url)
		{
			try {
				HttpListenerResponse response = context.Response;
				CelestialBody body = findBody (url [1].Value);
				if (body == null || body.BiomeMap.Attributes.Length <= 1) {
					httpError (context);
					return;
				}

				string ret = getInfo(body);

				response.StatusCode = 200;
				response.ContentType = "application/json";
				response.Close (Encoding.UTF8.GetBytes(ret), false);
			} catch (Exception e) {
				Debug.LogException(e);
			}
		}

		public static void httpInfoHandle (HttpListenerContext context, GroupCollection url)
		{
			try {
				HttpListenerResponse response = context.Response;
				string ret = "{ ";
				foreach (CelestialBody body in FlightGlobals.Bodies) {
					ret += "\"" + body.name + "\": " + getInfo(body) + ",";
				}
				ret += "}";

				response.StatusCode = 200;
				response.ContentType = "application/json";
				response.Close (Encoding.UTF8.GetBytes(ret), false);
			} catch (Exception e) {
				Debug.LogException(e);
			}
		}

		static float deltaTime = 0.0f;
		static int scale = 256;
		public static void httpTileHandle (HttpListenerContext context, GroupCollection url)
		{
			HttpListenerResponse response = context.Response;

			try {
				float start = Time.realtimeSinceStartup;

				int z;
				int x;
				int y;
				if (!int.TryParse (url [3].Value, out z) || !int.TryParse (url [4].Value, out x) || !int.TryParse (url [5].Value, out y)) {
					httpError (context);
					return;
				}
				CelestialBody body = findBody (url [1].Value);
				if (body == null) {
					httpError (context);
					return;
				}

				Texture2D tex = null;

				switch (url [2].Value) {
				case "sat":
					tex = getSatTile (body, x, y, z, scale);
					break;
				case "biome":
					tex = getBiomeTile (body, x, y, z, scale);
					break;
				case "hillshading":
					tex = getHillshadingTile (body, x, y, z, scale);
					break;
				default:
					httpError (context);
					return;
				}

				if (tex == null) {
					httpError(context);
					return;
				}

				byte[] ret = tex.EncodeToPNG ();

				response.StatusCode = 200;
				response.ContentType = "image/png";
				response.Close (ret, false);

				if (deltaTime == 0.0f) deltaTime = Time.realtimeSinceStartup-start;
				deltaTime = (Time.realtimeSinceStartup - start)*0.1f + deltaTime*0.9f;
				if (deltaTime > 0.5 && scale > 16) {
					scale >>= 1;
					deltaTime /= 4;
				} else if (deltaTime < 0.1 && scale < 256) {
					scale <<= 1;
					deltaTime *= 4;
				}
			} catch (Exception e) {
				Debug.LogException(e);
			}
		}

		public static void httpError (HttpListenerContext context)
		{
			HttpListenerResponse response = context.Response;
			response.StatusCode = 404;
			response.Close();
		}

		public static CelestialBody findBody (string name)
		{
			foreach (CelestialBody body in FlightGlobals.Bodies) {
				if (body.GetName () == name)
					return body;
			}
			return null;
		}

		public static string getInfo (CelestialBody body)
		{
			string ret = "{ \"biomes\": [ ";
			foreach (CBAttributeMap.MapAttribute biome in body.BiomeMap.Attributes) {
				ret += "{ \"name\": \"" + biome.name + "\", ";
				ret += "\"color\": \"" + toHTMLColor(biome.mapColor) + "\" }, ";
			}
			ret += "] }";
			return ret;
		}

		public static int getSize (CelestialBody body, int max, int z)
		{
			double pixels = Math.Sqrt(PQS.cacheVertCount)*4 * Math.Pow (2, body.pqsController.maxLevel);
			return Math.Min((int)(pixels/Math.Pow(2, z)), max);
		}

		public static Texture2D getSatTile (CelestialBody body, int tileX, int tileY, int z, int size = 256)
		{
			if (body.pqsController == null)
				return null;
			initMaps(body);
			size = getSize(body, size, z);
			Texture2D ret = new Texture2D (size, size, TextureFormat.ARGB32, false);
			for (int x = 0; x < size; x++) {
				for (int y = 0; y < size; y++) {
					Color c = getColor(body, tile2lat(tileY+1-y/(size-1.0), z), tile2lon(tileX+x/(size-1.0), z));
					ret.SetPixel(x, y, c);
				}
			}
			ret.Apply();
			return ret;
		}

		public static Texture2D getBiomeTile (CelestialBody body, int tileX, int tileY, int z, int size = 256)
		{
			if (body.BiomeMap.Attributes.Length <= 1)
				return null;
			Texture2D ret = new Texture2D (size, size, TextureFormat.ARGB32, false);
			for (int x = 0; x < size; x++) {
				for (int y = 0; y < size; y++) {
					Color c = getBiomeColor(body, tile2lat(tileY+1-y/(size-1.0), z), tile2lon(tileX+x/(size-1.0), z));
					ret.SetPixel(x, y, c);
				}
			}
			ret.Apply();
			return ret;
		}

		public static Texture2D getHillshadingTile (CelestialBody body, int tileX, int tileY, int z, int size = 256)
		{
			if (body.pqsController == null)
				return null;
			size = getSize(body, size, z);
			Texture2D ret = new Texture2D (size, size, TextureFormat.ARGB32, false);
			double[] line = new double[size+1];
			for (int x = 0; x < size+1; x++) {
				for (int y = 0; y < size+1; y++) {
					double ele = getElevation(body, tile2lat(tileY+1-y/(size-1.0), z), tile2lon(tileX+x/(size-1.0), z));
					if (x > 0 && y > 0) {
						double s = Math.PI * 2 * body.Radius / ((size-1) << z);
						double dx = (line [y] - ele) / s;
						double dy = (line [y-1] - ele) / s;

						double slope = Math.PI / 2 - Math.Atan (Math.Sqrt (dx * dx + dy * dy));
						double aspect = Math.Atan2 (dx, dy);

						double cang = Math.Sin (Math.PI / 4) * Math.Sin (slope) +
									  Math.Cos (Math.PI / 4) * Math.Cos (slope) *
									  Math.Cos ((315) / 180 * Math.PI - Math.PI/2 - aspect);
						//Color c = (cang > 0.5)?(new Color(1f, 1f, 1f, (float)cang*2-1)):(new Color(0f, 0f, 0f, (float)cang*2));
						Color c = new Color((float)cang, (float)cang, (float)cang, 1.0f);
						ret.SetPixel (x - 1, y - 1, c);
					}
					line[y] = ele;
				}
			}
			ret.Apply();
			return ret;
		}

		public static double tile2lon (double x, int z)
		{
			return (x / Math.Pow(2.0, z) * 360.0) - 180.0;
		}

		public static double tile2lat (double y, int z)
		{
			return -(y / Math.Pow(2.0, z) * 360.0) + 180.0;
		}

		private static Color getBiomeColor (CelestialBody body, double lat, double lon)
		{
			if (lat > 90 || lat < -90 || lon > 180 || lon < -180)
				return Color.clear;
			Color r = body.BiomeMap.GetAtt(lat*Mathf.PI/180, lon*Mathf.PI/180).mapColor;
			r.a = 1.0f;
			return r;
		}

		private static double getElevation (CelestialBody body, double lat, double lon)
		{
			double r = body.pqsController.GetSurfaceHeight (body.GetRelSurfaceNVector (lat, lon)) - body.Radius;
			if (r < 0)
				r = 0.0;
			return r;
		}

		public static void initMaps (CelestialBody body)
		{
			PQS pqs = body.pqsController;
			PQS.Global_AllowScatter = false;
			pqs.isBuildingMaps = true;
			pqs.isFakeBuild = true;
			typeof(PQS).InvokeMember ("SetupMods", BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.NonPublic, null, pqs, null);
			typeof(PQS).InvokeMember ("SetupBuildDelegates", BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.NonPublic, null, pqs, new object[]{true});
		}

		public static Color getColor (CelestialBody body, double lat, double lon)
		{
			if (lat > 90 || lat < -90 || lon > 180 || lon < -180)
				return Color.clear;
			PQS pqs = body.pqsController;
			Vector3d radialVector = body.GetRelSurfaceNVector (lat, lon);
			var buildData = new PQS.VertexBuildData ();
			buildData.directionFromCenter = radialVector.normalized;
			buildData.vertHeight = pqs.radius;
			typeof(PQS).InvokeMember ("Mod_OnVertexBuildHeight", BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.NonPublic, null, pqs, new object[]{buildData});
			typeof(PQS).InvokeMember ("Mod_OnVertexBuild", BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.NonPublic, null, pqs, new object[]{buildData});
			buildData.vertColor.a = 1.0f;
			return buildData.vertColor;
		}

		public static string toHTMLColor (Color c)
		{
			return string.Format("#{0,2:X}{1,2:X}{2,2:X}", (byte)(c.r*0xff), (byte)(c.g*0xff), (byte)(c.b*0xff)).Replace(' ', '0');
		}
	}
}

