﻿using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Rynchodon.Autopilot.Instruction.Command
{
    /// <summary>
    /// Create a GOLIS from the GPS list.
    /// </summary>
    public class GolisGps : GolisCoordinate
	{

		public override ACommand Clone()
		{
			return new GolisGps() { destination = destination };
		}

		public override string Identifier
		{
			get { return "cg"; }
		}

		public override string AddName
		{
			get { return "GPS"; }
		}

		public override string AddDescription
		{
			get { return "Fly to coordinates chosen from the GPS list."; }
		}

		public override void AddControls(List<IMyTerminalControl> controls)
		{
            //IMyTerminalControlListbox gpsList = new MyTerminalControlListbox<MyShipController>("GolisGpsList", MyStringId.GetOrCompute("GPS list"), MyStringId.NullOrEmpty, false, 18);
            IMyTerminalControlListbox gpsList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyShipController>("GolisGpsList");
            gpsList.Title = MyStringId.GetOrCompute("GPS list");
            gpsList.Tooltip = MyStringId.NullOrEmpty;
            gpsList.Multiselect = false;
            gpsList.VisibleRowsCount = 18;           

            gpsList.ListContent = FillWaypointList;
			gpsList.ItemSelected = OnItemSelected;
			controls.Add(gpsList);
		}

		private void FillWaypointList(IMyTerminalBlock dontCare, List<MyTerminalControlListBoxItem> allItems, List<MyTerminalControlListBoxItem> selected)
		{
			List<IMyGps> gpsList = MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.Player.IdentityId);
			bool select = destination.IsValid();
			foreach (IMyGps gps in gpsList)
			{
				// this will leak memory, as MyTerminalControlListBoxItem uses MyStringId for some stupid reason
				MyTerminalControlListBoxItem item = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(gps.Name), MyStringId.GetOrCompute(gps.Description), gps);
				allItems.Add(item);

				if (select && selected.Count == 0 && gps.Coords == destination)
					selected.Add(item);
			}
		}

		private void OnItemSelected(IMyTerminalBlock dontCare, List<MyTerminalControlListBoxItem> selected)
		{
			Logger.DebugLog("CommandGolisGps", "selected.Count: " + selected.Count, Logger.severity.ERROR, condition: selected.Count > 1);

			if (selected.Count == 0)
				destination = new Vector3D(double.NaN, double.NaN, double.NaN);
			else
				destination = ((IMyGps)selected[0].UserData).Coords;
		}

	}
}
