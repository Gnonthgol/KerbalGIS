using System;
using UnityEngine;

namespace KerbalGIS
{
	public class Info
	{
		public static string getJSONInfo (CelestialBody body)
		{
			string ret = "{ \"biomes\": [ ";
			foreach (CBAttributeMap.MapAttribute biome in body.BiomeMap.Attributes) {
				ret += "{ \"name\": \"" + biome.name + "\", ";
				ret += "\"color\": \"" + toHTMLColor (biome.mapColor) + "\" }, ";
			}
			ret += "] }";
			return ret;
		}

		public static string getJSONInfo ()
		{
			string ret = "{ ";
			foreach (CelestialBody body in FlightGlobals.Bodies) {
				ret += "\"" + body.name + "\": " + Info.getJSONInfo (body) + ",";
			}
			ret += "}";
			return ret;
		}

		public static string toHTMLColor (Color c)
		{
			return string.Format ("#{0,2:X}{1,2:X}{2,2:X}", (byte)(c.r * 0xff), (byte)(c.g * 0xff), (byte)(c.b * 0xff)).Replace (' ', '0');
		}
	}
}

