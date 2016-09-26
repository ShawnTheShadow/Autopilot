﻿using System;
using System.Collections.Generic;
using System.Text;
using Rynchodon.Attached;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Collections;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using Ingame = Sandbox.ModAPI.Ingame;

namespace Rynchodon.Autopilot.Instruction.Command
{
	public class TerminalAction : ACommand
	{

		#region Static

		static TerminalAction()
		{
			Logger.SetFileName("TerminalAction");
		}

		private static void RunActionOnBlock(Movement.Mover mover, string blockName, string actionString, List<Ingame.TerminalActionParameter> termParams)
		{
			blockName = blockName.Trim();
			actionString = actionString.Trim(); // leave spaces in actionString

			AttachedGrid.RunOnAttachedBlock(mover.Block.CubeGrid, AttachedGrid.AttachmentKind.Permanent, block => {
				IMyCubeBlock fatblock = block.FatBlock;
				if (fatblock == null || !(fatblock is IMyTerminalBlock))
					return false;

				if (!mover.Block.Controller.canControlBlock(fatblock))
					return false;

				if (!fatblock.DisplayNameText.Contains(blockName, StringComparison.InvariantCultureIgnoreCase))
					return false;

				IMyTerminalBlock terminalBlock = fatblock as IMyTerminalBlock;
				Sandbox.ModAPI.Interfaces.ITerminalAction actionToRun = terminalBlock.GetActionWithName(actionString); // get actionToRun on every iteration so invalid blocks can be ignored
				if (actionToRun != null)
				{
					if (termParams != null)
						actionToRun.Apply(fatblock, termParams);
					else
						actionToRun.Apply(fatblock);
				}

				return false;
			}, true);
		}

		private static bool CheckParams(VRage.Game.ModAPI.IMyCubeBlock autopilot, string blockName, string actionName, string[] parameters, out string message, out List<Ingame.TerminalActionParameter> termParams)
		{
			int needParams = -1;
			message = actionName + " not found";

			foreach (IMyCubeBlock block in AttachedGrid.AttachedCubeBlocks((IMyCubeGrid)autopilot.CubeGrid, AttachedGrid.AttachmentKind.Permanent, true))
			{
				if (!block.DisplayNameText.Contains(blockName, StringComparison.InvariantCultureIgnoreCase))
					continue;
				IMyTerminalBlock term = block as IMyTerminalBlock;
				if (term == null)
					continue;
				ITerminalAction terminalAction = (ITerminalAction)term.GetActionWithName(actionName);
				if (terminalAction != null)
				{
					int paramCount = terminalAction.GetParameterDefinitions().Count;
					if (Math.Abs(parameters.Length - paramCount) > (Math.Abs(parameters.Length - needParams)))
						continue;

					needParams = paramCount;
					if (parameters.Length == needParams && CheckParams(terminalAction, out message, out termParams, parameters))
						return true;
				}
			}

			if (needParams < 0)
			{
				message = actionName + " has no parameters";
				termParams = null;
				return false;
			}
			if (parameters.Length != needParams)
			{
				message = actionName + " requires " + needParams + " parameters, got " + parameters.Length;
				termParams = null;
				return false;
			}

			termParams = null;
			return false;
		}

		private static bool CheckParams(ITerminalAction termAction, out string message, out List<Ingame.TerminalActionParameter> termParams, params string[] termParamStrings)
		{
			ListReader<Ingame.TerminalActionParameter> parameterDefinitions = termAction.GetParameterDefinitions();
			if (parameterDefinitions.Count != termParamStrings.Length)
			{
				message = "wrong number of parameters: " + termParamStrings.Length + ", expected: " + parameterDefinitions.Count;
				termParams = null;
				return false;
			}
			termParams = new List<Ingame.TerminalActionParameter>(parameterDefinitions.Count);
			Logger.DebugLog("counts: " + parameterDefinitions.Count + ", " + termParamStrings.Length + ", " + termParams.Count);
			for (int index = 0; index < parameterDefinitions.Count; index++)
			{
				object obj;
				try
				{
					obj = Convert.ChangeType(termParamStrings[index], parameterDefinitions[index].TypeCode);
				}
				catch (InvalidCastException)
				{
					message = termParamStrings[index] + " cannot be cast to " + parameterDefinitions[index].TypeCode;
					return false;
				}
				catch (FormatException)
				{
					message = termParamStrings[index] + " is not in a format recognized by " + parameterDefinitions[index].TypeCode;
					return false;
				}
				catch (OverflowException)
				{
					message = termParamStrings[index] + " is out of range of " + parameterDefinitions[index].TypeCode;
					return false;
				}
				catch (ArgumentException)
				{
					message = "type code is invalid: " + parameterDefinitions[index].TypeCode;
					return false;
				}

				termParams.Add(Ingame.TerminalActionParameter.Get(obj));
			}
			message = null;
			return true;
		}

		#endregion Static

		private StringBuilder m_targetBlock, m_actionParams;
		// don't save the actual action, as string is more consistent when commands are sent over network
		private string m_termAction;

		public override ACommand Clone()
		{
			return new TerminalAction() { m_targetBlock = m_targetBlock.Clone(), m_termAction = m_termAction, m_actionParams = m_actionParams.Clone() };
		}

		public override string Identifier
		{
			get { return "a"; }
		}

		public override string AddName
		{
			get { return "Terminal Action"; }
		}

		public override string AddDescription
		{
			get { return "Run a terminal action on one or more blocks"; }
		}

		public override string Description
		{
			get
			{
				string first = "For all blocks with " + m_targetBlock + " in the name, run " + m_termAction;
				if (m_actionParams.Length == 0)
					return first;
				return first + " with parameters: " + m_actionParams;
			}
		}

		public override void AddControls(List<Sandbox.ModAPI.Interfaces.Terminal.IMyTerminalControl> controls)
		{
            IMyTerminalControlTextbox textBox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyShipController>("BlockName");
            textBox.Title = MyStringId.GetOrCompute("Block name");
            textBox.Tooltip = MyStringId.GetOrCompute("Blocks with names containing this string will run the action.");
			textBox.Getter = block => m_targetBlock;
			textBox.Setter = (block, value) => m_targetBlock = value;
			controls.Add(textBox);

            IMyTerminalControlListbox actionList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyShipController>("ActionList");
            actionList.Title = MyStringId.GetOrCompute("Action");
            actionList.Tooltip = MyStringId.NullOrEmpty;
			actionList.ListContent = ListContent;
			actionList.ItemSelected = ItemSelected;

            IMyTerminalControlButton searchButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyShipController>("SearchButton");
            searchButton.Title = MyStringId.GetOrCompute("Search");
            searchButton.Tooltip = MyStringId.GetOrCompute("Search for actions");
            searchButton.Action = block => actionList.UpdateVisual();

			controls.Add(searchButton);
			controls.Add(actionList);

            IMyTerminalControlTextbox actionParams = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyShipController>("ActionParams");
            actionParams.Title = MyStringId.GetOrCompute("Action Params");
            actionParams.Tooltip = MyStringId.GetOrCompute("Comma separated list of parameters to pass to action");
			actionParams.Getter = block => m_actionParams;
			actionParams.Setter = (block, value) => m_actionParams = value;
			controls.Add(actionParams);
		}

		protected override Action<Movement.Mover> Parse(VRage.Game.ModAPI.IMyCubeBlock autopilot, string command, out string message)
		{
			string[] split = command.Split(new char[] { ',' }, 3);

			if (split.Length < 2)
			{
				message = "Too few arguments: " + split.Length;
				return null;
			}

			string blockName = split[0].Trim(), actionName = split[1].Trim();

			if (string.IsNullOrWhiteSpace(blockName))
			{
				message = "missing block name";
				return null;
			}
			if (string.IsNullOrWhiteSpace(actionName))
			{
				message = "missing action";
				return null;
			}

			List<Ingame.TerminalActionParameter> termParams = null;
			if (split.Length > 2)
			{
				if (!CheckParams(autopilot, blockName, actionName, split[2].Split(','), out message, out termParams))
					return null;
				m_actionParams = new StringBuilder(split[2]);
			}
			else
				m_actionParams = new StringBuilder();

			m_targetBlock = new StringBuilder(blockName);
			m_termAction = actionName;
			message = null;
			return mover => RunActionOnBlock(mover, blockName, actionName, termParams);
		}

		protected override string TermToString(out string message)
		{
			if (string.IsNullOrWhiteSpace(m_targetBlock.ToString()))
			{
				message = "Need a block name";
				return null;
			}
			if (string.IsNullOrWhiteSpace(m_termAction))
			{
				message = "Need an action";
				return null;
			}

			message = null;
			string first = Identifier + ' ' + m_targetBlock + ',' + m_termAction;
			if (m_actionParams.Length == 0)
				return first;
			return first + ',' + m_actionParams;
		}

		private void ListContent(IMyTerminalBlock autopilot, List<MyTerminalControlListBoxItem> items, List<MyTerminalControlListBoxItem> selected)
		{
			string blockName = m_targetBlock.ToString().Trim();
			if (string.IsNullOrWhiteSpace(blockName))
				return;

			HashSet<ITerminalAction> termActs = new HashSet<ITerminalAction>();
			foreach (IMyCubeBlock block in AttachedGrid.AttachedCubeBlocks((IMyCubeGrid)autopilot.CubeGrid, AttachedGrid.AttachmentKind.Permanent, true))
			{
				if (!block.DisplayNameText.Contains(blockName, StringComparison.InvariantCultureIgnoreCase))
					continue;
				IMyTerminalBlock term = block as IMyTerminalBlock;
				if (term == null)
					continue;
				term.GetActions(null, action => {
					if (termActs.Add((ITerminalAction)action))
						items.Add(new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(action.Id), MyStringId.NullOrEmpty, action));
					return false;
				});
			}
		}

		private void ItemSelected(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selected)
		{
			if (selected.Count == 0)
				m_termAction = null;
			else
				m_termAction = ((ITerminalAction)selected[0].UserData).Id;
		}

	}
}
