﻿using System;
using System.Collections.Generic;
using System.Text;
using Rynchodon.Autopilot.Navigator;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Utils;
using VRageMath;
using Sandbox.ModAPI;

namespace Rynchodon.Autopilot.Instruction.Command
{
	/// <summary>
	/// Create a GOLIS from specified coordinates.
	/// </summary>
	public class GolisCoordinate : ACommand
	{

		static GolisCoordinate()
		{
			Logger.SetFileName("GolisCoordinate");
		}

		protected Vector3D destination;

		public override ACommand Clone()
		{
			return new GolisCoordinate() { destination = destination };
		}

		public override string Identifier
		{
			get { return "c"; }
		}

		public override string AddName
		{
			get { return "Coordinates"; }
		}

		public override string AddDescription
		{
			get { return "Fly to manually entered coordinates."; }
		}

		public override string Description
		{
			get { return "Fly to the coordinates: " + destination.X + ',' + destination.Y + ',' + destination.Z; }
		}

		public override void AddControls(List<IMyTerminalControl> controls)
		{
			IMyTerminalControlTextbox control;
			//control = new MyTerminalControlTextbox<MyShipController>("GolisCoordX", MyStringId.GetOrCompute("X Coordinate"), MyStringId.NullOrEmpty);
            control = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyShipController>("GolisCoordX");
            control.Title = MyStringId.GetOrCompute("X Coordinate");
            control.Tooltip = MyStringId.NullOrEmpty;
            AddGetSet(control, 0);
			controls.Add(control);

			//control = new MyTerminalControlTextbox<MyShipController>("GolisCoordY", MyStringId.GetOrCompute("Y Coordinate"), MyStringId.NullOrEmpty);
            control = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyShipController>("GolisCoordY");
            control.Title = MyStringId.GetOrCompute("Y Coordinate");
            control.Tooltip = MyStringId.NullOrEmpty;
            AddGetSet(control, 1);
			controls.Add(control);

			//control = new MyTerminalControlTextbox<MyShipController>("GolisCoordZ", MyStringId.GetOrCompute("Z Coordinate"), MyStringId.NullOrEmpty);
            control = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyShipController>("GolisCoordZ");
            control.Title = MyStringId.GetOrCompute("Z Coordinate");
            control.Tooltip = MyStringId.NullOrEmpty;
            AddGetSet(control, 2);
			controls.Add(control);
		}

		private void AddGetSet(IMyTerminalControlTextbox control, int index)
		{
			control.Getter = block => new StringBuilder(destination.GetDim(index).ToString());
			control.Setter = (block, strBuild) => {
				double value;
				if (!PrettySI.TryParse(strBuild.ToString(), out value))
					value = double.NaN;
				destination.SetDim(index, value);
			};
		}

		protected override Action<Movement.Mover> Parse(VRage.Game.ModAPI.IMyCubeBlock autopilot, string command, out string message)
		{
			if (!GetVector(command, out destination))
			{
				message = "Failed to parse: " + command;
				return null;
			}

			message = null;
			return mover => new GOLIS(mover, destination);
		}

		protected override string TermToString(out string message)
		{
			if (!destination.X.IsValid())
			{
				message = "Invalid X coordinate";
				return null;
			}
			if (!destination.Y.IsValid())
			{
				message = "Invalid Y coordinate";
				return null;
			}
			if (!destination.Z.IsValid())
			{
				message = "Invalid Z coordinate";
				return null;
			}

			message = null;
			return Identifier + ' ' + destination.X + ',' + destination.Y + ',' + destination.Z;
		}

	}

}
