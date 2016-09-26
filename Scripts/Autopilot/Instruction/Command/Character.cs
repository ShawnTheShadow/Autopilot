﻿using System;
using System.Collections.Generic;
using System.Text;
using Rynchodon.Autopilot.Navigator;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using VRage.Utils;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;

namespace Rynchodon.Autopilot.Instruction.Command
{
	public class Character : ACommand
	{

		private StringBuilder target;

		public override ACommand Clone()
		{
			return new Character() { target = target.Clone() };
		}

		public override string Identifier
		{
			get { return "character"; }
		}

		public override string AddName
		{
			get { return "Character"; }
		}

		public override string AddDescription
		{
			get { return "Fly to a character"; }
		}

		public override string Description
		{
			get { return "Fly to the character: " + target; }
		}

		public override void AddControls(List<Sandbox.ModAPI.Interfaces.Terminal.IMyTerminalControl> controls)
		{
            //MyTerminalControlTextbox<MyShipController> name = new MyTerminalControlTextbox<MyShipController>("CharName", MyStringId.GetOrCompute("Character Name"), MyStringId.NullOrEmpty);
            IMyTerminalControlTextbox name = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyShipController>("CharName");
            name.Title = MyStringId.GetOrCompute("Character Name");
            name.Tooltip = MyStringId.NullOrEmpty;

            name.Getter = block => target;
			name.Setter = (block, value) => target = value;
			controls.Add(name);
		}

		protected override Action<Movement.Mover> Parse(VRage.Game.ModAPI.IMyCubeBlock autopilot, string command, out string message)
		{
			if (string.IsNullOrWhiteSpace(command))
			{
				message = "No character name";
				return null;
			}

			target = new StringBuilder(command);
			message = null;
			return mover => new FlyToCharacter(mover, command);
		}

		protected override string TermToString()
		{
			return Identifier + ' ' + target;
		}

		public override string[] Aliases
		{
			get { return new string[] { "char" }; }
		}
	}
}
