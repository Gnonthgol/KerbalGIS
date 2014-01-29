KerbalGIS version 1.0-alpha1 for KSP version 0.23.0
=======

Starts a web server that shows maps of the bodies in Kerbal Space Program.

Default location for the webserver is localhost port 8080. The map tiles are located under /tile/{body}/{style}/ and follows the standard TMS scheme using latlon projection. The available styles are currently 'sat', 'biome' and 'hillshading'. A list of the biome colors can be found at /info/{body}.json or all bodies at /info.json. Files in the PluginData directory will be served if there are no other endpoints.

Installation
-------

Copy the KerbalGIS directory to your KSP/GameData/ directory.

Usage
-------

Start KSP. After the game is loaded point your web browser at http://localhost:8080/ and enjoy the maps. KSP will still be usable but performance will suffer when you pan or zoom the map in your browser.

License
-------
Copyright 2014 Gnonthgol
See LICENSE.txt

Enjoy!
