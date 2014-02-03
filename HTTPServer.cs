using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using KSP.IO;
using UnityEngine;

namespace KerbalGIS
{
	public class HTTPServer
	{
		HttpListener listener = null;
		Queue queue = new Queue ();
		Mutex queueMutex = new Mutex ();

		public HTTPServer ()
		{
			size = KerbalGIS.config.GetValue("defaultTileSize", 256);

			listener = new HttpListener ();
			foreach (string prefix in KerbalGIS.config.GetValue("prefix", "http://localhost:8080/").Split(new char[]{','})) {
				listener.Prefixes.Add (prefix);
			}
			listener.Start ();
			listener.BeginGetContext (new AsyncCallback (httpCallback), this);
		}

		public void Update ()
		{
			if (queueMutex.WaitOne (0)) {
				if (queue.Count > 0) {
					HttpListenerContext context = (HttpListenerContext)queue.Dequeue ();
					queueMutex.ReleaseMutex ();
					httpHandle (context);
				} else {
					queueMutex.ReleaseMutex ();
				}
			}
		}

		public void Stop ()
		{
			listener.Stop ();
			listener = null;
		}

		public static void httpCallback (IAsyncResult result)
		{
			HTTPServer server = (HTTPServer)result.AsyncState;
			HttpListenerContext context = server.listener.EndGetContext (result);
			server.queueMutex.WaitOne ();
			server.queue.Enqueue (context);
			server.queueMutex.ReleaseMutex ();
			server.listener.BeginGetContext (new AsyncCallback (httpCallback), server);
		}

		public void httpHandle (HttpListenerContext context)
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
			if (request == "/")
				request = "/index.html";
			try {
				byte[] ret = readBytes(request);
				response.StatusCode = ret==null?404:200;
				response.Close (ret, false);
			} catch (Exception) {
				response.StatusCode = 404;
				response.Close ();
			}
		}

		public void httpBodyInfoHandle (HttpListenerContext context, GroupCollection url)
		{
			try {
				HttpListenerResponse response = context.Response;
				CelestialBody body = KerbalGIS.findBody (url [1].Value);
				if (body == null || body.BiomeMap.Attributes.Length <= 1) {
					httpError (context);
					return;
				}

				string ret = Info.getJSONInfo (body);

				response.StatusCode = 200;
				response.ContentType = "application/json";
				response.Close (Encoding.UTF8.GetBytes (ret), false);
			} catch (Exception e) {
				Debug.LogException (e);
			}
		}

		public void httpInfoHandle (HttpListenerContext context, GroupCollection url)
		{
			try {
				HttpListenerResponse response = context.Response;
				string ret = Info.getJSONInfo ();

				response.StatusCode = 200;
				response.ContentType = "application/json";
				response.Close (Encoding.UTF8.GetBytes (ret), false);
			} catch (Exception e) {
				Debug.LogException (e);
			}
		}

		float deltaTime = 0.0f;
		int size = 256;

		public void httpTileHandle (HttpListenerContext context, GroupCollection url)
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
				CelestialBody body = KerbalGIS.findBody (url [1].Value);
				if (body == null) {
					httpError (context);
					return;
				}

				Texture2D tex = null;

				switch (url [2].Value) {
				case "sat":
					tex = Tiles.getSatTile (body, x, y, z, size);
					break;
				case "biome":
					tex = Tiles.getBiomeTile (body, x, y, z, size);
					break;
				case "hillshading":
					tex = Tiles.getHillshadingTile (body, x, y, z, size);
					break;
				default:
					httpError (context);
					return;
				}

				if (tex == null) {
					httpError (context);
					return;
				}

				byte[] ret = tex.EncodeToPNG ();

				response.StatusCode = 200;
				response.ContentType = "image/png";
				response.Close (ret, false);

				if (deltaTime == 0.0f) {
					deltaTime = Time.realtimeSinceStartup - start;
				} else {
					float a = 1.0f/KerbalGIS.config.GetValue("tileSmoothing", 10);
					deltaTime = (Time.realtimeSinceStartup - start) * a + deltaTime * (1-a);
				}

				float dT = 1.0f/KerbalGIS.config.GetValue("tilesPerSecond", 4);
				int minSize = KerbalGIS.config.GetValue("minTileSize", 16);
				if (deltaTime > dT && size > minSize) {
					size >>= 1;
					deltaTime /= 4;
				} else if (deltaTime < dT/4 && size < 256) {
					size <<= 1;
					deltaTime *= 4;
				}
			} catch (Exception e) {
				Debug.LogException (e);
			}
		}

		public void httpError (HttpListenerContext context)
		{
			HttpListenerResponse response = context.Response;
			response.StatusCode = 404;
			response.Close ();
		}

		public byte[] readBytes (string file)
		{
			string dir = Path.GetDirectoryName(IOUtils.GetFilePathFor(typeof(HTTPServer), file));
			string path = Path.Combine(dir, "www" + file);

			if (System.IO.File.Exists (path)) {
				return System.IO.File.ReadAllBytes (path);
			} else {
				return null;
			}
		}
	}
}
