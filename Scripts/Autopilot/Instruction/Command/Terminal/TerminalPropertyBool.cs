﻿using System.Collections.Generic;
using System.Text;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Utils;
using Sandbox.ModAPI;

namespace Rynchodon.Autopilot.Instruction.Command
{
	public class TerminalPropertyBool : TerminalProperty<bool>
	{
		public TerminalPropertyBool()
		{
			m_hasValue = true;
		}

		protected override string ShortType
		{
			get { return "bool"; }
		}

		public override ACommand Clone()
		{
			return new TerminalPropertyBool() { m_targetBlock = m_targetBlock.Clone(), m_termProp = m_termProp, m_value = m_value };
		}

		protected override void AddValueControl(List<Sandbox.ModAPI.Interfaces.Terminal.IMyTerminalControl> controls)
		{
            IMyTerminalControlCheckbox checkbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyShipController>("BoolValue");
            checkbox.Title = MyStringId.GetOrCompute("Value");
            checkbox.Tooltip = MyStringId.GetOrCompute("Value to set propety to");
			checkbox.Getter = block => m_value;
			checkbox.Setter = (block, value) => {
				m_value = value;
				m_hasValue = true;
			};
			controls.Add(checkbox);
		}
	}
}
