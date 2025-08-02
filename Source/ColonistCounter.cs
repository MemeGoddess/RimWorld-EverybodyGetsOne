using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using HarmonyLib;
using RimWorld.Planet;
using UnityEngine;
using TD_Find_Lib;

namespace Everybody_Gets_One
{
	public class PersonCountWorldComp : WorldComponent
	{
		//cache these counts each update
		public Dictionary<Bill_Production, QuerySearch> billPersonCounters = new();

		public QuerySearch defaultQuerySearch;

		public PersonCountWorldComp(World world) : base(world) { }

		private List<Bill_Production> scribeBills;
		private List<QuerySearch> scribeSearches;
		public override void ExposeData()
		{
			Scribe_Collections.Look(ref billPersonCounters, "billPersonCounters", LookMode.Reference, LookMode.Deep, ref scribeBills, ref scribeSearches);
			if (billPersonCounters == null)
				billPersonCounters = new();
		}

		public bool HasPersonCounter(Bill_Production bill)
		{
			return billPersonCounters.ContainsKey(bill);
		}

		public void SetPersonCounter(Bill_Production bill, QuerySearch search)
		{
			billPersonCounters[bill] = search;

		}

		public QuerySearch GetPersonCounter(Bill_Production bill)
		{
			QuerySearch personCounter;
			if (!billPersonCounters.TryGetValue(bill, out personCounter))
			{
				personCounter = MakePersonCounter(bill);
				billPersonCounters[bill] = personCounter;
			}

			if(personCounter.MapType == SearchMapType.ChosenMaps && !personCounter.ChosenMaps.Any())
				personCounter.ChosenMaps.Add(bill.Map);

			return personCounter;
		}

		public void RemovePersonCounter(Bill_Production bill)
		{
			billPersonCounters.Remove(bill);
		}


		public int CountFor(Bill_Production bill)
		{
 			QuerySearch personCounter = GetPersonCounter(bill);

			personCounter.RemakeList();

			return personCounter.result.allThingsCount;
		}

		public QuerySearch MakePersonCounter(Bill_Production bill)
		{
			defaultQuerySearch ??= CreateQuerySearch(bill.Map);
			var search = defaultQuerySearch.CloneForUse([bill.Map], "TD.PeopleForBill".Translate() + bill.LabelCap);

			return search;
		}

		public void OpenPersonCounter(Bill_Production bill)
		{
			Find.WindowStack.Add(new PersonCounterEditor(GetPersonCounter(bill)));
		}

		public void IngestMapComponent(PersonCountMapComp comp)
		{
			var bills = comp.billPersonCounters;
			var billCount = 0;
			foreach (var querySearch in bills)
			{
				if (billPersonCounters.ContainsKey(querySearch.Key))
					continue;

				billPersonCounters[querySearch.Key] = querySearch.Value;
				billCount++;
			}
			comp.billPersonCounters = new Dictionary<Bill_Production, QuerySearch>();
			if (billCount > 0)
				Verse.Log.Message($"[EverybodyGetsOne] Migrated {billCount} bill(s) from {comp.map.Parent.Label}. You should only see this once ^.^");

			Find.Maps.ForEach(map => map.components.Remove(comp));
		}

		public void OrphanBills(Map map)
		{
			foreach (var billPersonCounter in billPersonCounters)
			{
				if(billPersonCounter.Value.ChosenMaps.Contains(map))
					billPersonCounter.Value.ChosenMaps.Remove(map);
			}
		}

		private QuerySearch CreateQuerySearch(Map map)
		{
			QuerySearch search = new(map);
			search.SetListType(SearchListType.Everyone, false);

			ThingQueryBasicProperty queryColonist = ThingQueryMaker.MakeQuery<ThingQueryBasicProperty>();
			queryColonist.sel = QueryPawnProperty.IsColonist;
			search.Children.Add(queryColonist, remake: false);

			ThingQueryBasicProperty querySlave = ThingQueryMaker.MakeQuery<ThingQueryBasicProperty>();
			querySlave.sel = QueryPawnProperty.IsSlaveOfColony;
			querySlave.include = false;
			search.Children.Add(querySlave, remake: false);

			ThingQueryQuest queryQuestLodger = ThingQueryMaker.MakeQuery<ThingQueryQuest>();
			//Default is Quest Lodger
			queryQuestLodger.include = false;
			search.Children.Add(queryQuestLodger, remake: false);

			search.name = "TD.PeopleForBill".Translate();

			return search;
		}
	}

	public class PersonCountMapComp : MapComponent
	{
		//cache these counts each update
		public Dictionary<Bill_Production, QuerySearch> billPersonCounters = new();

		public override void FinalizeInit()
		{
			base.FinalizeInit();
			var worldComp = Find.World.GetComponent<PersonCountWorldComp>();
			worldComp.IngestMapComponent(this);
		}

		public PersonCountMapComp(Map map) : base(map) { }

		private List<Bill_Production> scribeBills;
		private List<QuerySearch> scribeSearches;
		public override void ExposeData()
		{
			Scribe_Collections.Look(ref billPersonCounters, "billPersonCounters", LookMode.Reference, LookMode.Deep, ref scribeBills, ref scribeSearches);
			if (billPersonCounters == null)
				billPersonCounters = new();
		}
	}

	public class PersonCounterEditor : SearchEditorRevertableWindow, ISearchReceiver
	{
		public PersonCounterEditor(QuerySearch search) : base(search, TransferTag)
		{
			//Same as the Dialog_BillConfig
			forcePause = true;
			doCloseX = true;
			absorbInputAroundWindow = true;
			closeOnClickedOutside = true;

			//Overrides from SearchEditorWindow
			onlyOneOfTypeAllowed = true;
			//preventCameraMotion = false;
			draggable = false;
			resizeable = false;
			//above //doCloseX = true;
		}

		public override Vector2 InitialSize => new Vector2(750, 320);

		public override void SetInitialSizeAndPosition()
		{
			base.SetInitialSizeAndPosition();
			windowRect.x = (UI.screenWidth - windowRect.width) / 2;
			windowRect.y = 0;
		}


		// ISearchReceiver stuff
		public static string TransferTag = "TD.EGO";
		public string Source => TransferTag;
		public string ReceiveName => "TD.UseAsPersonCounter".Translate();
		public QuerySearch.CloneArgs CloneArgs => QuerySearch.CloneArgs.use;
		
		public bool CanReceive() => true;
		public void Receive(QuerySearch search)
		{
			Import(search);
		}

		public override void PostOpen()
		{
			base.PostOpen();

			SearchTransfer.Register(this);
		}

		public override void PreClose()
		{
			base.PreClose();

			SearchTransfer.Deregister(this);
		}
	}

	public static class MapCompExtensions
	{
		public static int CurrentPersonCount(this Bill_Production bill) =>
			Find.World.GetComponent<PersonCountWorldComp>().CountFor(bill);

		public static void OpenPersonCounter(this Bill_Production bill) =>
			Find.World.GetComponent<PersonCountWorldComp>().OpenPersonCounter(bill);

		public static bool HasPersonCounter(this Bill_Production bill) =>
			Find.World.GetComponent<PersonCountWorldComp>().HasPersonCounter(bill);

		public static QuerySearch GetPersonCounter(this Bill_Production bill) =>
			Find.World.GetComponent<PersonCountWorldComp>().GetPersonCounter(bill);

		public static void RemovePersonCounter(this Bill_Production bill) =>
			Find.World.GetComponent<PersonCountWorldComp>().RemovePersonCounter(bill);

		public static void SetPersonCounter(this Bill_Production bill, QuerySearch search) =>
			Find.World.GetComponent<PersonCountWorldComp>().SetPersonCounter(bill, search);
	}


	[HarmonyPatch(typeof(Building_WorkTable), nameof(Building_WorkTable.Notify_BillDeleted))]
	public static class DeleteBillCounter
	{
		public static void Postfix(Building_WorkTable __instance, Bill bill)
		{
			if(bill is Bill_Production billP)
			{
				billP.RemovePersonCounter();
			}
		}
	}

	[HarmonyPatch(typeof(Building), nameof(Building.Destroy))]
	public class Building_Destroy_Detour
	{
		public static void Prefix(Building __instance)
		{
			//Because Building_WorkTable doesn't override Destroy
			if (__instance is Building_WorkTable workTable)
				foreach (var bill in workTable.BillStack.Bills)
					if (bill is Bill_Production billP)
						billP.RemovePersonCounter();
		}
	}
}
