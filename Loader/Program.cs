using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using NDesk.Options;
using Newtonsoft.Json;

namespace Loader
{
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    class Program
	{
		static void Main(string[] args)
		{
			string scDataRoot = null;
			string outputRoot = null;
			string itemFile = null;
			string missingShops = null;
			bool shipsOnly = false;

			DateTime startdt = DateTime.Now;

			var watchMaster = System.Diagnostics.Stopwatch.StartNew();

			var p = new OptionSet
			{
				{ "scdata=", v => scDataRoot = v },
				{ "input=",  v => scDataRoot = v },
				{ "output=",  v => outputRoot = v },
				{ "itemfile=", v => itemFile = v },
				{ "shipsonly", v => shipsOnly = true }
			};

			var extra = p.Parse(args);

			var badArgs = false;
			if (extra.Count > 0) badArgs = true;
			else if (!String.IsNullOrEmpty(itemFile) && (!String.IsNullOrEmpty(scDataRoot) || !String.IsNullOrEmpty(outputRoot))) badArgs = true;
			else if (String.IsNullOrEmpty(itemFile) && (String.IsNullOrEmpty(scDataRoot) || String.IsNullOrEmpty(outputRoot))) badArgs = true;

			if (badArgs)
			{
				Console.WriteLine("Usage:");
				Console.WriteLine("    Loader.exe -input=<path to extracted Star Citizen data> -output=<path to JSON output folder>");
				Console.WriteLine(" or Loader.exe -itemfile=<path to an SCItem XML file>");
				Console.WriteLine();
				return;
			}

			var log_index   = startdt.ToString("yyyyMMddHHmmss");
			missingShops    = Path.Join (outputRoot, $"missing_shops-{log_index}.log" );
			var mainlogfile = Path.Join (outputRoot, $"loader-{log_index}.log" );

			var watchPrep = System.Diagnostics.Stopwatch.StartNew();

			JsonConvert.DefaultSettings = () => new JsonSerializerSettings
			{
				Formatting = Formatting.Indented,
				NullValueHandling = NullValueHandling.Ignore
			};

			if (itemFile != null)
			{
				var entityParser = new ClassParser<scdb.Xml.Entities.EntityClassDefinition>();
				var entity = entityParser.Parse(itemFile);
				var json = JsonConvert.SerializeObject(entity);
				Console.WriteLine(json);
				return;
			}

			// Prep the output folder
			if (Directory.Exists(outputRoot) && !shipsOnly)
			{
				var info = new DirectoryInfo(outputRoot);
				foreach (var file in info.GetFiles()) file.Delete();
				foreach (var dir in info.GetDirectories()) dir.Delete(true);
			}
			else Directory.CreateDirectory(outputRoot);

			File.AppendAllText(missingShops, $"Loader Starting: {startdt.ToString("dd MMM yyyy HH:mm:ss")}{Environment.NewLine}");

			Console.WriteLine("Initialising");

			// A loadout loader to help with any XML loadouts we encounter while parsing entities
			var loadoutLoader = new LoadoutLoader
			{
				OutputFolder = outputRoot,
				DataRoot = scDataRoot
			};

			watchPrep.Stop();

			var watchLabel = System.Diagnostics.Stopwatch.StartNew();

			// Localisation
			Console.WriteLine("Load Localisation");

			var labelLoader = new LabelsLoader
			{
				OutputFolder = outputRoot,
				DataRoot = scDataRoot
			};
			var labels = labelLoader.Load("english");
			var localisationSvc = new LocalisationService(labels);

			watchLabel.Stop();

			var watchManu = System.Diagnostics.Stopwatch.StartNew();

			// Manufacturers
			Console.WriteLine("Load Manufacturers");

			var manufacturerLoader = new ManufacturerLoader(localisationSvc)
			{
				OutputFolder = outputRoot,
				DataRoot = scDataRoot
			};
			var manufacturerIndex = manufacturerLoader.Load();

			watchManu.Stop();

			var watchAmmo = System.Diagnostics.Stopwatch.StartNew();

			// Ammunition
			Console.WriteLine("Load Ammunition");

			var ammoLoader = new AmmoLoader
			{
				OutputFolder = outputRoot,
				DataRoot = scDataRoot
			};
			var ammoIndex = ammoLoader.Load();

			watchAmmo.Stop();

			var watchItem = System.Diagnostics.Stopwatch.StartNew();

			// Items
			if (!shipsOnly)
			{
				Console.WriteLine("Load Items");

				var itemLoader = new ItemLoader
				{
					OutputFolder = outputRoot,
					DataRoot = scDataRoot,
					OnXmlLoadout = path => loadoutLoader.Load(path),
					Manufacturers = manufacturerIndex,
					Ammo = ammoIndex
				};
				itemLoader.Load();

				watchItem.Stop();
			}

			if (watchItem.IsRunning)
			    watchItem.Stop();

			var watchShips = System.Diagnostics.Stopwatch.StartNew();

			// Ships and vehicles
			Console.WriteLine("Load Ships and Vehicles");

			var shipLoader = new ShipLoader
			{
				OutputFolder = outputRoot,
				DataRoot = scDataRoot,
				OnXmlLoadout = path => loadoutLoader.Load(path),
				Manufacturers = manufacturerIndex
			};
			shipLoader.Load();

			watchShips.Stop();
			var watchShops = System.Diagnostics.Stopwatch.StartNew();

			// Prices
			if (!shipsOnly)
			{
				Console.WriteLine("Load Shops");

				var shopLoader = new ShopLoader(localisationSvc)
				{
					OutputFolder = outputRoot,
					DataRoot = scDataRoot,
					UnknownShop = missingShops
				};

				shopLoader.Load();
				watchShops.Stop();
			}

			if (watchShops.IsRunning)
			    watchShops.Stop();

			var watchStars = System.Diagnostics.Stopwatch.StartNew();

			// Starmap
			if (!shipsOnly)
			{
				Console.WriteLine("Load Starmap");

				var starmapLoader = new StarmapLoader(localisationSvc)
				{
					OutputFolder = outputRoot,
					DataRoot = scDataRoot
				};

				starmapLoader.Load();
				watchStars.Stop();
			}

			if (watchStars.IsRunning)
			    watchStars.Stop();

			watchMaster.Stop();

			Console.WriteLine("Finished!");
			Console.WriteLine($"  Initialise        : {watchPrep.Elapsed.Hours} hrs {watchPrep.Elapsed.Minutes} mins {watchPrep.Elapsed.Seconds}.{watchPrep.Elapsed.Milliseconds} secs");
			Console.WriteLine($"  Load Labels       : {watchLabel.Elapsed.Hours} hrs {watchLabel.Elapsed.Minutes} mins {watchLabel.Elapsed.Seconds}.{watchLabel.Elapsed.Milliseconds} secs" );
			Console.WriteLine($"  Load Manufacturers: {watchManu.Elapsed.Hours} hrs {watchManu.Elapsed.Minutes} mins {watchManu.Elapsed.Seconds}.{watchManu.Elapsed.Milliseconds} secs");
			Console.WriteLine($"  Load Ammo         : {watchAmmo.Elapsed.Hours} hrs {watchAmmo.Elapsed.Minutes} mins {watchAmmo.Elapsed.Seconds}.{watchAmmo.Elapsed.Milliseconds} secs");
			Console.WriteLine($"  Load Items        : {watchItem.Elapsed.Hours} hrs {watchItem.Elapsed.Minutes} mins {watchItem.Elapsed.Seconds}.{watchItem.Elapsed.Milliseconds} secs");
			Console.WriteLine($"  Load Ships        : {watchShips.Elapsed.Hours} hrs {watchShips.Elapsed.Minutes} mins {watchShips.Elapsed.Seconds}.{watchShips.Elapsed.Milliseconds} secs");
			Console.WriteLine($"  Load Shops        : {watchShops.Elapsed.Hours} hrs {watchShops.Elapsed.Minutes} mins {watchShops.Elapsed.Seconds}.{watchShops.Elapsed.Milliseconds} secs");
			Console.WriteLine($"  Load Starmp       : {watchStars.Elapsed.Hours} hrs {watchStars.Elapsed.Minutes} mins {watchStars.Elapsed.Seconds}.{watchStars.Elapsed.Milliseconds} secs");
			Console.WriteLine($"  Total Elapsed     : {watchMaster.Elapsed.Hours} hrs {watchMaster.Elapsed.Minutes} mins {watchMaster.Elapsed.Seconds}.{watchMaster.Elapsed.Milliseconds} secs");
		}

        private string GetDebuggerDisplay()
        {
            return ToString();
        }
    }
}
