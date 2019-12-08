﻿using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Newtonsoft.Json;
using NDesk.Options;

using scdb.Xml.Entities;

namespace Loader
{
	class Program
	{
		static void Main(string[] args)
		{
			string scDataRoot = null;
			string outputRoot = null; ;

			var p = new OptionSet
			{
				{ "scdata=", v => { scDataRoot = v; } },
				{ "output=",  v => { outputRoot = v; } }
			};

			var extra = p.Parse(args);

			if (extra.Count > 0 || String.IsNullOrWhiteSpace(scDataRoot) || String.IsNullOrWhiteSpace(outputRoot))
			{
				Console.WriteLine("Usage: Loader.exe -scdata=<path to extracted Star Citizen data> -output=<path to JSON output folder>");
				return;
			}

			JsonConvert.DefaultSettings = () => new JsonSerializerSettings
			{
				Formatting = Formatting.Indented,
				NullValueHandling = NullValueHandling.Ignore
			};

			// Ships and ground vehicles
			var shipLoader = new ShipLoader
			{
				OutputFolder = Path.Combine(outputRoot, "ships"),
				DataRoot = scDataRoot
			};
			var shipIndex = shipLoader.Load();

			File.WriteAllText(Path.Combine(outputRoot, "ships.json"), JsonConvert.SerializeObject(shipIndex));

			// Items that go on ships
			var itemLoader = new ItemLoader
			{
				OutputFolder = Path.Combine(outputRoot, "items"),
				DataRoot = scDataRoot
			};
			var itemIndex = itemLoader.Load();
			File.WriteAllText(Path.Combine(outputRoot, "items.json"), JsonConvert.SerializeObject(itemIndex));

			var itemDict = new Dictionary<string, EntityCacheEntry>();
			itemIndex.ForEach(i => itemDict.Add(i.ItemName, new EntityCacheEntry { item = i.ItemName, entityFilename = i.EntityFilename, entity = i.Entity }));
		}
	}

	public class EntityCacheEntry
	{
		public string item;
		public string entityFilename;
		public EntityClassDefinition entity;
	}
}