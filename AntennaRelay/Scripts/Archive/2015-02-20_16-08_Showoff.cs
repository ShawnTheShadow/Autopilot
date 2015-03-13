﻿using System;
using System.Collections.Generic;
//using System.Linq;
using System.Text;

using Sandbox.ModAPI;
using Ingame = Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRageMath;

namespace Rynchodon.AntennaRelay
{
	public static class Showoff
	{
		private static string getTextPanelName(IMyCubeBlock showoff)
		{
			string displayName = showoff.DisplayNameText;
			int start = displayName.IndexOf('[') + 1;
			int end = displayName.IndexOf(']');
			if (start > 0 && end > start) // has appropriate brackets
			{
				int length = end - start;
				return displayName.Substring(start, length);
			}
			log("bad brackets", "getTextPanelName()", Logger.severity.TRACE);
			return null;
		}

		private static LinkedList<Ingame.IMyTextPanel> findTextPanel(IMyCubeBlock showoff)
		{
			string searchForName = getTextPanelName(showoff);
			if (searchForName == null)
				return null;

			List<IMySlimBlock> allBlocks = new List<IMySlimBlock>();
			showoff.CubeGrid.GetBlocks(allBlocks);

			LinkedList<Ingame.IMyTextPanel> textPanels = new LinkedList<Ingame.IMyTextPanel>();
			foreach (IMySlimBlock block in allBlocks)
			{
				IMyCubeBlock fatblock = block.FatBlock;
				if (fatblock == null)
					continue;

				Ingame.IMyTextPanel panel = fatblock as Ingame.IMyTextPanel;
				if (panel == null)
				{
					log("not a panel: " + fatblock.DisplayNameText, "findTextPanel()", Logger.severity.TRACE);
					continue;
				}

				if (!showoff.canConsiderFriendly(fatblock))
				{
					log("not friendly: " + fatblock.DisplayNameText, "findTextPanel()", Logger.severity.TRACE);
					continue;
				}

				if (fatblock.DisplayNameText.looseContains(searchForName))
				{
					log("adding panel: " + fatblock.DisplayNameText, "findTextPanel()", Logger.severity.TRACE);
					textPanels.AddLast(panel);
				}
			}
			return textPanels;
		}

		//private struct DistLastSeen
		//{
		//	public double distance;
		//	public LastSeen seen;

		//	public DistLastSeen(Vector3D me, Vector3D target, LastSeen seen)
		//	{
		//		this.distance = (me - target).Length();
		//		this.seen = seen;
		//	}
		//}

		private const string publicTitle = "Grid found by Autopilot";
		private const string radarId = "Radar";

		private class sortableLastSeen : IComparable<sortableLastSeen>
		{
			public readonly IMyCubeBlockExtensions.Relations relations;
			public readonly int distance;
			public readonly int seconds;
			public readonly LastSeen seen;

			private const string tab = "    ";
			private readonly string GPStag = '\n' + tab + tab + "GPSAutopilot_";

			private sortableLastSeen() { }

			public sortableLastSeen(Vector3D myPos, LastSeen seen, IMyCubeBlockExtensions.Relations relations)
			{
				this.seen = seen;
				this.relations = relations;
				TimeSpan sinceLastSeen;
				distance = (int)(seen.predictPosition(out sinceLastSeen) - myPos).Length();
				seconds = (int)sinceLastSeen.TotalSeconds;
			}

			public StringBuilder ToStringBuilder(int count)
			{
				string time = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");

				StringBuilder builder = new StringBuilder();
				builder.Append(relations);
				builder.Append(tab);
				builder.Append(distance);
				builder.Append('m');
				builder.Append(tab);
				builder.Append(time);
				builder.Append(GPStag);
				builder.Append(relations);
				builder.Append('#');
				builder.Append(count);
				builder.Append(':');
				builder.Append(Math.Round(seen.LastKnownPosition.X, 2));
				builder.Append(':');
				builder.Append(Math.Round(seen.LastKnownPosition.Y, 2));
				builder.Append(':');
				builder.Append(Math.Round(seen.LastKnownPosition.Z, 2));
				builder.Append(":\n");

				return builder;
			}

			/// <summary>
			/// sort by relations, then by distance
			/// </summary>
			/// <param name="other"></param>
			/// <returns></returns>
			public int CompareTo(sortableLastSeen other)
			{
				if (this.relations != other.relations)
					return this.relations.CompareTo(other.relations);
				return this.distance.CompareTo(other.distance);
			}
		}

		public static void doShowoff(IMyCubeBlock showoff, IEnumerator<LastSeen> toDisplay, int toDisplayCount)
		{
			LinkedList<Ingame.IMyTextPanel> textPanels = findTextPanel(showoff);
			if (textPanels == null)
				return;

			log("building toDisplay", "findTextPanel()", Logger.severity.TRACE);
			Vector3D myPos = showoff.GetPosition();
			List<sortableLastSeen> sortableSeen = new List<sortableLastSeen>();
			while (toDisplay.MoveNext())
			{
				IMyCubeGrid grid = toDisplay.Current.Entity as IMyCubeGrid;
				if (grid == null || AttachedGrids.isGridAttached(grid, showoff.CubeGrid))
					continue;

				IMyCubeBlockExtensions.Relations relations = showoff.getRelationsTo(grid, IMyCubeBlockExtensions.Relations.Enemies).mostHostile();
				sortableSeen.Add(new sortableLastSeen(myPos, toDisplay.Current, relations));
			}
			sortableSeen.Sort();

			int count = 0;
			StringBuilder displayText = new StringBuilder();
			foreach (sortableLastSeen sortable in sortableSeen)
				displayText.Append(sortable.ToStringBuilder(count++));

			string displayString = displayText.ToString();

			foreach (Ingame.IMyTextPanel panel in textPanels)
			{
				log("writing to panel: " + panel.DisplayNameText, "findTextPanel()", Logger.severity.TRACE);
				panel.WritePublicText(displayString);
				if (panel.GetPublicTitle() != publicTitle)
				{
					panel.WritePublicTitle(publicTitle);
					panel.AddImageToSelection(radarId);
					panel.ShowTextureOnScreen();
				}
			}
		}


		private static Logger myLogger;
		[System.Diagnostics.Conditional("LOG_ENABLED")]
		private static void log(string toLog, string method = null, Logger.severity level = Logger.severity.DEBUG)
		{ alwaysLog(toLog, method, level); }
		private static void alwaysLog(string toLog, string method = null, Logger.severity level = Logger.severity.DEBUG)
		{
			if (myLogger == null)
				myLogger = new Logger(string.Empty, "Showoff");
			myLogger.log(level, method, toLog);
		}
	}
}
