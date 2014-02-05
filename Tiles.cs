using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace KerbalGIS
{
	public class Tiles
	{
		public static Texture2D getSatTile (CelestialBody body, int x, int y, int z, int size = 256)
		{
			if (body.pqsController == null)
				return null;
			initMaps (body);
			size = getSize (body, size, z);
			TileData data = getTileData(body, x, y, z, size);
			size = data.size;
			Texture2D ret = new Texture2D (size, size, TextureFormat.ARGB32, false);

			ret.SetPixels (data.color);
			ret.Apply ();
			return ret;
		}

		public static Texture2D getBiomeTile (CelestialBody body, int tileX, int tileY, int z, int size = 256)
		{
			if (body.BiomeMap.Attributes.Length <= 1)
				return null;
			Texture2D ret = new Texture2D (size, size, TextureFormat.ARGB32, false);
			for (int x = 0; x < size; x++) {
				for (int y = 0; y < size; y++) {
					Color c = getBiomeColor (body, tile2lat (tileY + 1 - y / (size - 1.0), z), tile2lon (tileX + x / (size - 1.0), z));
					ret.SetPixel (x, y, c);
				}
			}
			ret.Apply ();
			return ret;
		}

		public static Texture2D getHillshadingTile (CelestialBody body, int tileX, int tileY, int z, int size = 256)
		{
			if (body.pqsController == null)
				return null;
			size = getSize (body, size, z);
			TileData data = getTileData(body, tileX, tileY, z, size);
			size = data.size;
			Texture2D ret = new Texture2D (size, size, TextureFormat.ARGB32, false);
			for (int x = 0; x < size; x++) {
				for (int y = 0; y < size; y++) {
					double s = Math.PI * 2 * body.Radius / ((size - 1) << z);
					double dx = ((data.getEle(x-1, y-1) + 2*data.getEle(x, y-1) + data.getEle(x+1, y-1)) -
                    	(data.getEle(x-1, y+1) + 2*data.getEle(x, y+1) + data.getEle(x+1, y+1))) /
                    	(4.0 * s);
					double dy = ((data.getEle(x-1, y-1) + 2*data.getEle(x-1, y) + data.getEle(x-1, y+1)) -
                    	(data.getEle(x+1, y-1) + 2*data.getEle(x+1, y) + data.getEle(x+1, y+1))) /
                    	(4.0 * s);

					double slope = Math.PI / 2 - Math.Atan (Math.Sqrt (dx * dx + dy * dy));
					double aspect = Math.Atan2 (dx, dy);

					double cang = Math.Sin (Math.PI / 4) * Math.Sin (slope) +
						Math.Cos (Math.PI / 4) * Math.Cos (slope) *
						Math.Cos ((315) / 180 * Math.PI - Math.PI / 2 - aspect);
					//Color c = (cang > 0.5)?(new Color(1f, 1f, 1f, (float)cang*2-1)):(new Color(0f, 0f, 0f, (float)cang*2));
					Color c = new Color ((float)cang, (float)cang, (float)cang, 1.0f);
					ret.SetPixel (x, y, c);
				}
			}
			ret.Apply ();
			return ret;
		}

		private static Hashtable tileDataCache = new Hashtable();
		public static TileData getTileData (CelestialBody body, int tileX, int tileY, int z, int size = 256)
		{
			string args = String.Format("{0},{1},{2},{3}", body.name, tileX, tileY, z);
			TileData ret = (TileData)tileDataCache [args];

			if (ret == null || ret.size < size) {
				ret = new TileData (size);

				for (int x = 0; x < size; x++) {
					for (int y = 0; y < size; y++) {
						double lat = tile2lat (tileY + 1 - y / (size - 1.0), z);
						double lon = tile2lon (tileX + x / (size - 1.0), z);
						var buildData = getBuildData (body, lat, lon);
						ret.elevation [y * size + x] = buildData.vertHeight;
						ret.color [y * size + x] = buildData.vertColor;
					}
				}

				tileDataCache[args] = ret;
			}

			return ret;
		}

		public static double tile2lon (double x, int z)
		{
			return (x / Math.Pow (2.0, z) * 360.0) - 180.0;
		}

		public static double tile2lat (double y, int z)
		{
			return -(y / Math.Pow (2.0, z) * 360.0) + 180.0;
		}

		private static Color getBiomeColor (CelestialBody body, double lat, double lon)
		{
			if (lat > 90 || lat < -90 || lon > 180 || lon < -180)
				return Color.clear;
			Color r = body.BiomeMap.GetAtt (lat * Mathf.PI / 180, lon * Mathf.PI / 180).mapColor;
			r.a = 1.0f;
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

		public static PQS.VertexBuildData getBuildData (CelestialBody body, double lat, double lon)
		{
			if (lat > 90 || lat < -90 || lon > 180 || lon < -180)
				return new PQS.VertexBuildData ();
			PQS pqs = body.pqsController;
			Vector3d radialVector = body.GetRelSurfaceNVector (lat, lon);
			var buildData = new PQS.VertexBuildData ();
			buildData.directionFromCenter = radialVector.normalized;
			buildData.vertHeight = pqs.radius;
			typeof(PQS).InvokeMember ("Mod_OnVertexBuildHeight", BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.NonPublic, null, pqs, new object[]{buildData});
			typeof(PQS).InvokeMember ("Mod_OnVertexBuild", BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.NonPublic, null, pqs, new object[]{buildData});
			buildData.vertColor.a = 1.0f;
			return buildData;
		}

		public static int getSize (CelestialBody body, int max, int z)
		{
			double pixels = Math.Sqrt (PQS.cacheVertCount) * 4 * Math.Pow (2, body.pqsController.maxLevel);
			return Math.Min ((int)(pixels / Math.Pow (2, z)), max);
		}

		public class TileData {
			public int size = 256;
			public Color[] color;
			public double[] elevation;

			public TileData(int size = 256) {
				if (size < 2) size = 2;
				this.size = size;
				color = new Color[size*size];
				elevation = new double[size*size];
			}

			public double getEle (int x, int y)
			{
				if (x < 0) {
					return 2*getEle(x+1, y) - getEle(x+2, y);
				} else if (x >= size) {
					return 2*getEle(x-1, y) - getEle(x-2, y);
				}
				if (y < 0) {
					return 2*getEle(x, y+1) - getEle(x, y+2);
				} else if (y >= size) {
					return 2*getEle(x, y-1) - getEle(x, y-2);
				}
				return elevation[y * size + x];
			}
		}
	}
}

