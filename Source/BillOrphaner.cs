using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;

namespace Everybody_Gets_One
{
	[HarmonyPatch(typeof(Game), nameof(Game.DeinitAndRemoveMap))]
	public static class BillOrphaner
  {
	  public static void Prefix(Map map)
	  {
		  var component = Find.World.GetComponent<PersonCountWorldComp>();
			component.OrphanBills(map);
	  }
  }
}
