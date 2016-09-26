﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Rynchodon.Autopilot.Instruction.Command;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace Rynchodon.Autopilot.Instruction
{
    /// <summary>
    /// GUI programming and command interpretation.
    /// </summary>
    public class AutopilotCommands
	{

		private class StaticVariables
		{
			public readonly Regex GPS_tag = new Regex(@"[^c]\s*GPS:.*?:(-?\d+\.?\d*):(-?\d+\.?\d*):(-?\d+\.?\d*):");
			public readonly string GPS_replaceWith = @"cg $1, $2, $3";
			public Dictionary<char, List<ACommand>> dummyCommands = new Dictionary<char, List<ACommand>>();
			public AddCommandInternalNode addCommandRoot;
		}

		private static StaticVariables Static = new StaticVariables();

		static AutopilotCommands()
		{
			Logger.SetFileName("AutopilotCommands");

			MyAPIGateway.Entities.OnCloseAll += Entities_OnCloseAll;

			List<AddCommandInternalNode> rootCommands = new List<AddCommandInternalNode>();

			// fly somewhere

			List<AddCommandLeafNode> commands = new List<AddCommandLeafNode>();

			AddDummy(new GolisCoordinate(), commands);
			AddDummy(new GolisGps(), commands);
			AddDummy(new FlyRelative(), commands);
			AddDummy(new Character(), commands);

			rootCommands.Add(new AddCommandInternalNode("Fly Somewhere", commands.ToArray()));

			// friendly grid

			commands.Clear();

			AddDummy(new TargetBlockSearch(), commands);
			AddDummy(new LandingBlock(), commands);
			AddDummy(new Offset(), commands);
			AddDummy(new Form(), commands);
			AddDummy(new GridDestination(), commands);
			AddDummy(new Unland(), commands);
			AddDummy(new UnlandBlock(), commands);

			rootCommands.Add(new AddCommandInternalNode("Fly to a Ship", commands.ToArray()));

			// complex task

			commands.Clear();

			AddDummy(new Enemy(), commands);
			AddDummy(new HarvestVoxel(), commands);
			AddDummy(new Grind(), commands);
			AddDummy(new Weld(), commands);
			AddDummy(new LandVoxel(), commands);
			AddDummy(new Orbit(), commands);
			AddDummy(new NavigationBlock(), commands);

			rootCommands.Add(new AddCommandInternalNode("Tasks", commands.ToArray()));

			// variables

			commands.Clear();

			AddDummy(new Proximity(), commands);
			AddDummy(new SpeedLimit(), commands);
			AddDummy(new StraightLine(), commands);
			AddDummy(new Asteroid(), commands);

			rootCommands.Add(new AddCommandInternalNode("Variables", commands.ToArray()));

			// flow control

			commands.Clear();

			AddDummy(new TextPanel(), commands);
			AddDummy(new Wait(), commands);
			AddDummy(new Exit(), commands);
			AddDummy(new Stop(), commands);
			AddDummy(new Disable(), commands);

			rootCommands.Add(new AddCommandInternalNode("Flow Control", commands.ToArray()));

			// terminal action/property

			commands.Clear();

			AddDummy(new TerminalAction(), commands);
			AddDummy(new TerminalPropertyBool(), commands);
			AddDummy(new TerminalPropertyFloat(), commands);
			AddDummy(new TerminalPropertyColour(), commands);

			rootCommands.Add(new AddCommandInternalNode("Terminal", commands.ToArray()));

			Static.addCommandRoot = new AddCommandInternalNode("root", rootCommands.ToArray());
		}

		private static void Entities_OnCloseAll()
		{
			MyAPIGateway.Entities.OnCloseAll -= Entities_OnCloseAll;
			Static = null;
		}

		private static void AddDummy(ACommand command, string idOrAlias)
		{
			List<ACommand> list;
			if (!Static.dummyCommands.TryGetValue(idOrAlias[0], out list))
			{
				list = new List<ACommand>();
				Static.dummyCommands.Add(idOrAlias[0], list);
			}
			if (!list.Contains(command))
				list.Add(command);
		}

		private static void AddDummy(ACommand command, List<AddCommandLeafNode> children = null)
		{
			foreach (string idOrAlias in command.IdAndAliases())
				AddDummy(command, idOrAlias);
			
			if (children != null)
				children.Add(new AddCommandLeafNode(command));
		}

		public static AutopilotCommands GetOrCreate(IMyTerminalBlock block)
		{
			if (Globals.WorldClosed || block.Closed)
				return null;
			AutopilotCommands result;
			if (!Registrar.TryGetValue(block, out result))
				result = new AutopilotCommands(block);
			return result;
		}

		/// <summary>
		/// Get the best command associated with a string.
		/// </summary>
		/// <param name="parse">The complete command string, including Identifier.</param>
		/// <returns>The best command associated with parse.</returns>
		private static ACommand GetCommand(string parse)
		{
			parse = parse.TrimStart().ToLower();

			List<ACommand> list;
			if (!Static.dummyCommands.TryGetValue(parse[0], out list))
				return null;

			ACommand bestMatch = null;
			int bestMatchLength = 0;
			foreach (ACommand cmd in list)
				foreach (string idOrAlias in cmd.IdAndAliases())
					if (idOrAlias.Length > bestMatchLength && parse.StartsWith(idOrAlias))
					{
						bestMatchLength = idOrAlias.Length;
						bestMatch = cmd;
					}

			if (bestMatch == null)
				return null;
			return bestMatch.Clone();
		}

		private readonly IMyTerminalBlock m_block;
		/// <summary>Command list for GUI programming, not to be used by others</summary>
		private readonly List<ACommand> m_commandList = new List<ACommand>();
		private readonly Logger m_logger;

		/// <summary>Shared from any command source</summary>
		private readonly StringBuilder m_syntaxErrors = new StringBuilder();
		/// <summary>Action list for GUI programming and commands text box, not to be used for messaged commands.</summary>
		private readonly AutopilotActionList m_actionList = new AutopilotActionList();

		private IMyTerminalControlListbox m_termCommandList;
		private bool m_listCommands = true, m_replace;
		private int m_insertIndex;
		private ACommand m_currentCommand;
		private Stack<AddCommandInternalNode> m_currentAddNode = new Stack<AddCommandInternalNode>();
		private string m_infoMessage, m_commands;

		/// <summary>
		/// The most recent commands from either terminal or a message.
		/// </summary>
		public string Commands
		{
			get { return m_commands; }
			private set
			{
				m_commands = value;
				m_logger.alwaysLog("Commands: " + m_commands); // for bug reports
			}
		}

		public bool HasSyntaxErrors { get { return m_syntaxErrors.Length != 0; } }

		private AutopilotCommands(IMyTerminalBlock block)
		{
			this.m_block = block;
			this.m_logger = new Logger(GetType().Name, block);

			m_block.AppendingCustomInfo += m_block_AppendSyntaxErrors;

			Registrar.Add(block, this);
		}

		/// <summary>
		/// Invoke when commands textbox changed.
		/// </summary>
		public void OnCommandsChanged()
		{
			m_actionList.Clear();
		}

		public void StartGooeyProgramming()
		{
			m_logger.debugLog("entered");

			using (MainLock.AcquireSharedUsing())
			{
				m_currentCommand = null;
				m_listCommands = true;
				m_commandList.Clear();
				m_syntaxErrors.Clear();

				//MyTerminalControls.Static.CustomControlGetter += CustomControlGetter;
                MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
				m_block.AppendingCustomInfo += m_block_AppendingCustomInfo;

				Commands = AutopilotTerminal.GetAutopilotCommands(m_block).ToString();
				ParseCommands(Commands);
				if (m_syntaxErrors.Length != 0)
					m_block.RefreshCustomInfo();
				m_block.SwitchTerminalTo();
			}
		}

		public AutopilotActionList GetActions()
		{
			using (MainLock.AcquireSharedUsing())
			{
				if (!m_actionList.IsEmpty)
				{
					m_actionList.Reset();
					return m_actionList;
				}
				m_syntaxErrors.Clear();

				Commands = AutopilotTerminal.GetAutopilotCommands(m_block).ToString();
				List<ACommand> commands = new List<ACommand>();
				GetActions(Commands, m_actionList);
				if (m_syntaxErrors.Length != 0)
					m_block.RefreshCustomInfo();
				return m_actionList;
			}
		}

		public AutopilotActionList GetActions(string allCommands)
		{
			using (MainLock.AcquireSharedUsing())
			{
				Commands = allCommands;
				m_syntaxErrors.Clear();

				AutopilotActionList actList = new AutopilotActionList();
				GetActions(Commands, actList);
				if (m_syntaxErrors.Length != 0)
					m_block.RefreshCustomInfo();
				return actList;
			}
		}

		public void GetActions(string allCommands, AutopilotActionList actionList)
		{
			GetActions(ParseCommands(allCommands), actionList);
		}

		private IEnumerable<ACommand> ParseCommands(string allCommands)
		{
			if (string.IsNullOrWhiteSpace(allCommands))
			{
				Logger.DebugLog("no commands");
				yield break;
			}

			allCommands = Static.GPS_tag.Replace(allCommands, Static.GPS_replaceWith);

			string[] commands = allCommands.Split(new char[] { ';', ':' });
			foreach (string cmd in commands)
			{
				if (string.IsNullOrWhiteSpace(cmd))
				{
					Logger.DebugLog("empty command");
					continue;
				}

				ACommand apCmd = GetCommand(cmd);
				if (apCmd == null)
				{
					m_syntaxErrors.AppendLine("No command: \"" + cmd + '"');
					Logger.DebugLog("No command: \"" + cmd + '"');
					continue;
				}

				string msg;
				if (!apCmd.SetDisplayString((IMyCubeBlock)m_block, cmd, out msg))
				{
					m_syntaxErrors.Append("Error with command: \"");
					m_syntaxErrors.Append(cmd);
					m_syntaxErrors.Append("\":\n  ");
					m_syntaxErrors.AppendLine(msg);
					Logger.DebugLog("Error with command: \"" + cmd + "\":\n  " + msg, Logger.severity.INFO);
					continue;
				}

				yield return apCmd;
			}
		}

		private void GetActions(IEnumerable<ACommand> commandList, AutopilotActionList actionList)
		{
			int count = 0;
			const int limit = 1000;

			foreach (ACommand cmd in commandList)
			{
				TextPanel tp = cmd as TextPanel;
				if (tp == null)
				{
					if (cmd.Action == null)
					{
						Logger.AlwaysLog("Command is missing action: " + cmd.DisplayString, Logger.severity.ERROR);
						continue;
					}

					if (++count > limit)
					{
						Logger.DebugLog("Reached command limit");
						m_syntaxErrors.AppendLine("Reached command limit");
						return;
					}
					Logger.DebugLog("yield: " + cmd.DisplayString);
					actionList.Add(cmd.Action);
					continue;
				}

				TextPanelMonitor textPanelMonitor = tp.GetTextPanelMonitor(m_block, this);
				if (textPanelMonitor == null)
				{
					Logger.DebugLog("Text panel not found: " + tp.SearchPanelName);
					m_syntaxErrors.Append("Text panel not found: ");
					m_syntaxErrors.AppendLine(tp.SearchPanelName);
					continue;
				}
				if (textPanelMonitor.AutopilotActions.IsEmpty)
				{
					Logger.DebugLog(textPanelMonitor.TextPanel.DisplayNameText + " has no commands");
					m_syntaxErrors.Append(textPanelMonitor.TextPanel.DisplayNameText);
					m_syntaxErrors.AppendLine(" has no commands");
					continue;
				}

				actionList.Add(textPanelMonitor);
			}
		}

		private void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
		{
            //m_logger.debugLog("entered");
            IMyTerminalControlButton ctrl_btn;
            if (block != m_block)
				return;

			controls.Clear();

			if (m_listCommands)
			{
				m_logger.debugLog("showing command list");

				if (m_termCommandList == null)
				{
                   //m_termCommandList = new MyTerminalControlListbox<MyShipController>("CommandList", MyStringId.GetOrCompute("Commands"), MyStringId.NullOrEmpty, false, 10);
                    m_termCommandList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyShipController>("CommandList");
                    m_termCommandList.Title = MyStringId.GetOrCompute("Commands");
                    m_termCommandList.Tooltip = MyStringId.NullOrEmpty;
                    m_termCommandList.Multiselect = false;
                    m_termCommandList.VisibleRowsCount = 10;                  
                    m_termCommandList.ListContent = ListCommands;
					m_termCommandList.ItemSelected = CommandSelected;
				}
				controls.Add(m_termCommandList);               

                //controls.Add(new MyTerminalControlButton<MyShipController>("AddCommand", MyStringId.GetOrCompute("Add Command"), MyStringId.NullOrEmpty, AddCommand));
                ctrl_btn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyShipController>("AddCommand");
                ctrl_btn.Title = MyStringId.GetOrCompute("Add Command");
                ctrl_btn.Tooltip = MyStringId.NullOrEmpty;
                ctrl_btn.Action = AddCommand;
                controls.Add(ctrl_btn);

				//controls.Add(new MyTerminalControlButton<MyShipController>("InsertCommand", MyStringId.GetOrCompute("Insert Command"), MyStringId.NullOrEmpty, InsertCommand));
                ctrl_btn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyShipController>("InsertCommand");
                ctrl_btn.Title = MyStringId.GetOrCompute("Insert Command");
                ctrl_btn.Tooltip = MyStringId.NullOrEmpty;
                ctrl_btn.Action = InsertCommand;
                controls.Add(ctrl_btn);

                //controls.Add(new MyTerminalControlButton<MyShipController>("RemoveCommand", MyStringId.GetOrCompute("Remove Command"), MyStringId.NullOrEmpty, RemoveCommand));
                ctrl_btn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyShipController>("RemoveCommand");
                ctrl_btn.Title = MyStringId.GetOrCompute("Remove Command");
                ctrl_btn.Tooltip = MyStringId.NullOrEmpty;
                ctrl_btn.Action = RemoveCommand;
                controls.Add(ctrl_btn);

                //controls.Add(new MyTerminalControlButton<MyShipController>("EditCommand", MyStringId.GetOrCompute("Edit Command"), MyStringId.NullOrEmpty, EditCommand));
                ctrl_btn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyShipController>("EditCommand");
                ctrl_btn.Title = MyStringId.GetOrCompute("Edit Command");
                ctrl_btn.Tooltip = MyStringId.NullOrEmpty;
                ctrl_btn.Action = EditCommand;
                controls.Add(ctrl_btn);

                //controls.Add(new MyTerminalControlButton<MyShipController>("MoveCommandUp", MyStringId.GetOrCompute("Move Command Up"), MyStringId.NullOrEmpty, MoveCommandUp));
                ctrl_btn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyShipController>("MoveCommandUp");
                ctrl_btn.Title = MyStringId.GetOrCompute("Move Command Up");
                ctrl_btn.Tooltip = MyStringId.NullOrEmpty;
                ctrl_btn.Action = MoveCommandUp;
                controls.Add(ctrl_btn);

                //controls.Add(new MyTerminalControlButton<MyShipController>("MoveCommandDown", MyStringId.GetOrCompute("Move Command Down"), MyStringId.NullOrEmpty, MoveCommandDown));
                ctrl_btn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyShipController>("MoveCommandDown");
                ctrl_btn.Title = MyStringId.GetOrCompute("Move Command Down");
                ctrl_btn.Tooltip = MyStringId.NullOrEmpty;
                ctrl_btn.Action = MoveCommandDown;
                controls.Add(ctrl_btn);

                //controls.Add(new MyTerminalControlSeparator<MyShipController>());      
                controls.Add(MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyShipController>("Seperator"));

                //controls.Add(new MyTerminalControlButton<MyShipController>("Finished", MyStringId.GetOrCompute("Save & Exit"), MyStringId.GetOrCompute("Save all commands and exit"), b => Finished(true)));
                ctrl_btn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyShipController>("Finished");
                ctrl_btn.Title = MyStringId.GetOrCompute("Save & Exit");
                ctrl_btn.Tooltip = MyStringId.GetOrCompute("Save all commands and exit");
                ctrl_btn.Action = b => Finished(true);
                controls.Add(ctrl_btn);

                //controls.Add(new MyTerminalControlButton<MyShipController>("DiscardAll", MyStringId.GetOrCompute("Discard & Exit"), MyStringId.GetOrCompute("Discard all commands and exit"), b => Finished(false)));
                ctrl_btn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyShipController>("DiscardAll");
                ctrl_btn.Title = MyStringId.GetOrCompute("Discard & Exit");
                ctrl_btn.Tooltip = MyStringId.GetOrCompute("Discard all commands and exit");
                ctrl_btn.Action = b => Finished(false);
                controls.Add(ctrl_btn);


                return;
			}

			if (m_currentCommand == null)
			{
				// add/insert new command
				if (m_currentAddNode.Count == 0)
					m_currentAddNode.Push(Static.addCommandRoot);
                
                foreach (AddCommandTreeNode child in m_currentAddNode.Peek().Children)
                {
                     ctrl_btn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyShipController>(child.Name.RemoveWhitespace());
                    ctrl_btn.Title = MyStringId.GetOrCompute(child.Name);
                    ctrl_btn.Tooltip = MyStringId.GetOrCompute(child.Tooltip);
                    ctrl_btn.Action = shipController =>
                    {
                        AddCommandLeafNode leaf = child as AddCommandLeafNode;
                        if (leaf != null)
                        {
                            m_currentCommand = leaf.Command.Clone();
                            if (!m_currentCommand.HasControls)
                                CheckAndSave(block);
                            m_currentAddNode.Clear();
                        }
                        else
                            m_currentAddNode.Push((AddCommandInternalNode)child);
                        ClearMessage();
                    };
                }
                ctrl_btn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyShipController>("UpOneLevel");
                ctrl_btn.Title = MyStringId.GetOrCompute("Up one level");
                ctrl_btn.Tooltip = MyStringId.GetOrCompute("Return to previous list");
                ctrl_btn.Action = shipController => {
					m_currentAddNode.Pop();
					if (m_currentAddNode.Count == 0)
						m_listCommands = true;
					shipController.SwitchTerminalTo();
				};

				return;
			}

			m_logger.debugLog("showing single command: " + m_currentCommand.Identifier);

			m_currentCommand.AddControls(controls);
			//controls.Add(new MyTerminalControlSeparator<MyShipController>());
            controls.Add(MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyShipController>("Seperator"));
            //controls.Add(new MyTerminalControlButton<MyShipController>("SaveGooeyCommand", MyStringId.GetOrCompute("Check & Save"), MyStringId.GetOrCompute("Check the current command for syntax errors and save it"), CheckAndSave));
            ctrl_btn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyShipController>("SaveGooeyCommand");
            ctrl_btn.Title = MyStringId.GetOrCompute("Check & Save");
            ctrl_btn.Tooltip = MyStringId.GetOrCompute("Check the current command for syntax errors and save it");
            ctrl_btn.Action = CheckAndSave;
            //controls.Add(new MyTerminalControlButton<MyShipController>("DiscardGooeyCommand", MyStringId.GetOrCompute("Discard"), MyStringId.GetOrCompute("Discard the current command"), Discard));
            ctrl_btn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyShipController>("DiscardGooeyCommand");
            ctrl_btn.Title = MyStringId.GetOrCompute("Discard");
            ctrl_btn.Tooltip = MyStringId.GetOrCompute("Discard the current command");
            ctrl_btn.Action = Discard;
        }

		private void ListCommands(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> allItems, List<MyTerminalControlListBoxItem> selected)
		{
			m_logger.debugLog(block != m_block, "block != m_block", Logger.severity.FATAL);

			foreach (ACommand command in m_commandList)
			{
				// this will leak memory, as MyTerminalControlListBoxItem uses MyStringId for some stupid reason
				MyTerminalControlListBoxItem item = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(command.DisplayString), MyStringId.GetOrCompute(command.Description), command);
				allItems.Add(item);
				if (command == m_currentCommand && selected.Count == 0)
					selected.Add(item);
			}
		}

		private void CommandSelected(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selected)
		{
			m_logger.debugLog(block != m_block, "block != m_block", Logger.severity.FATAL);
			m_logger.debugLog(selected.Count > 1, "selected.Count: " + selected.Count, Logger.severity.ERROR);

			if (selected.Count == 0)
			{
				m_currentCommand = null;
				m_logger.debugLog("selection cleared");
			}
			else
			{
				m_currentCommand = (ACommand)selected[0].UserData;
				m_logger.debugLog("selected: " + m_currentCommand.DisplayString);
			}
		}

		#region Button Action

		private void Discard(IMyTerminalBlock block)
		{
			m_logger.debugLog("entered");

			m_logger.debugLog(block != m_block, "block != m_block", Logger.severity.FATAL);
			m_logger.debugLog(m_currentCommand == null, "m_currentCommand == null", Logger.severity.FATAL);

			m_currentCommand = null;
			m_listCommands = true;

			ClearMessage();
		}

		private void CheckAndSave(IMyTerminalBlock block)
		{
			m_logger.debugLog(block != m_block, "block != m_block", Logger.severity.FATAL);
			m_logger.debugLog(m_currentCommand == null, "m_currentCommand == null", Logger.severity.FATAL);

			string msg;
			if (m_currentCommand.ValidateControls((IMyCubeBlock)m_block, out msg))
			{
				if (m_commandList.Contains(m_currentCommand))
				{
					m_logger.debugLog("edited command: " + m_currentCommand.DisplayString);
				}
				else
				{
					if (m_insertIndex == -1)
					{
						m_logger.debugLog("new command: " + m_currentCommand.DisplayString);
						m_commandList.Add(m_currentCommand);
					}
					else
					{
						if (m_replace)
						{
							m_commandList.RemoveAt(m_insertIndex);
							m_logger.debugLog("replace at " + m_insertIndex + ": " + m_currentCommand.DisplayString);
						}
						else
							m_logger.debugLog("new command at " + m_insertIndex + ": " + m_currentCommand.DisplayString);
						m_commandList.Insert(m_insertIndex, m_currentCommand);
					}
				}
				m_currentCommand = null;
				m_listCommands = true;
				ClearMessage();
			}
			else
			{
				m_logger.debugLog("failed to save command: " + m_currentCommand.DisplayString + ", reason: " + msg);
				LogAndInfo(msg);
			}
		}

		private void AddCommand(IMyTerminalBlock block)
		{
			m_logger.debugLog(block != m_block, "block != m_block", Logger.severity.FATAL);

			m_insertIndex = -1;
			m_replace = false;
			m_currentCommand = null;
			m_listCommands = false;
			ClearMessage();
			m_logger.debugLog("adding new command at end");
		}

		private void InsertCommand(IMyTerminalBlock block)
		{
			m_logger.debugLog(block != m_block, "block != m_block", Logger.severity.FATAL);

			if (m_currentCommand == null)
			{
				LogAndInfo("nothing selected");
				return;
			}

			m_insertIndex = m_commandList.IndexOf(m_currentCommand);
			m_replace = false;
			m_currentCommand = null;
			m_listCommands = false;
			ClearMessage();
			m_logger.debugLog("inserting new command at " + m_insertIndex);
		}

		private void RemoveCommand(IMyTerminalBlock block)
		{
			m_logger.debugLog("entered");

			m_logger.debugLog(block != m_block, "block != m_block", Logger.severity.FATAL);

			if (m_currentCommand == null)
			{
				LogAndInfo("nothing selected");
				return;
			}

			m_commandList.Remove(m_currentCommand);
			m_currentCommand = null;
			ClearMessage();
		}

		private void EditCommand(IMyTerminalBlock block)
		{
			m_logger.debugLog(block != m_block, "block != m_block", Logger.severity.FATAL);

			if (m_currentCommand == null)
			{
				LogAndInfo("nothing selected");
				return;
			}

			if (!m_currentCommand.HasControls)
			{
				LogAndInfo("This command cannot be edited");
				return;
			}

			m_logger.debugLog("editing: " + m_currentCommand.DisplayString);

			m_insertIndex = m_commandList.IndexOf(m_currentCommand);
			m_replace = true;
			m_currentCommand = m_currentCommand.Clone();
			m_listCommands = false;
			ClearMessage();
		}

		private void MoveCommandUp(IMyTerminalBlock block)
		{
			m_logger.debugLog(block != m_block, "block != m_block", Logger.severity.FATAL);

			if (m_currentCommand == null)
			{
				LogAndInfo("nothing selected");
				return;
			}

			int index = m_commandList.IndexOf(m_currentCommand);
			if (index == 0)
			{
				LogAndInfo("already first element: " + m_currentCommand.DisplayString);
				return;
			}
			m_logger.debugLog("moved up: " + m_currentCommand.DisplayString);
			m_commandList.Swap(index, index - 1);
			ClearMessage();
		}

		private void MoveCommandDown(IMyTerminalBlock block)
		{
			m_logger.debugLog(block != m_block, "block != m_block", Logger.severity.FATAL);

			if (m_currentCommand == null)
			{
				LogAndInfo("nothing selected");
				return;
			}

			int index = m_commandList.IndexOf(m_currentCommand);
			if (index == m_commandList.Count - 1)
			{
				LogAndInfo("already last element: " + m_currentCommand.DisplayString);
				return;
			}
			m_logger.debugLog("moved down: " + m_currentCommand.DisplayString);
			m_commandList.Swap(index, index + 1);
			ClearMessage();
		}

		private void Finished(bool save)
		{
			m_logger.debugLog("entered");

			if (save)
			{
				Commands = string.Join(" ; ", m_commandList.Select(cmd => cmd.DisplayString));
				AutopilotTerminal.SetAutopilotCommands(m_block, new StringBuilder(Commands));
				m_actionList.Clear();
				GetActions(Commands, m_actionList);
			}

			//MyTerminalControls.Static.CustomControlGetter -= CustomControlGetter;
            MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
			m_block.AppendingCustomInfo -= m_block_AppendingCustomInfo;

			m_block.RefreshCustomInfo();
			m_block.SwitchTerminalTo();

			Cleanup();
		}

		#endregion Button Action

		private void LogAndInfo(string message,string member = null, int lineNumber = 0)
		{
			m_logger.debugLog(message, member: member, lineNumber: lineNumber);
			m_infoMessage = message;
			m_block.RefreshCustomInfo();
			m_block.SwitchTerminalTo();
		}

		private void ClearMessage()
		{
			m_infoMessage = null;
			m_block.RefreshCustomInfo();
			m_block.SwitchTerminalTo();
		}

		private void m_block_AppendingCustomInfo(IMyTerminalBlock arg1, StringBuilder arg2)
		{
			m_logger.debugLog("entered");

			if (!string.IsNullOrWhiteSpace(m_infoMessage))
			{
				m_logger.debugLog("appending info message: " + m_infoMessage);
				arg2.AppendLine();
				arg2.AppendLine(m_infoMessage);
			}

			if (!m_listCommands && m_currentCommand != null)
			{
				m_logger.debugLog("appending command info");
				arg2.AppendLine();
				m_currentCommand.AppendCustomInfo(arg2);
				arg2.AppendLine();
			}
		}

		private void m_block_AppendSyntaxErrors(IMyTerminalBlock arg1, StringBuilder arg2)
		{
			if (m_syntaxErrors.Length != 0)
			{
				m_logger.debugLog("appending syntax errors");
				arg2.AppendLine();
				arg2.Append("Syntax Errors:\n");
				arg2.Append(m_syntaxErrors);
			}
		}

		private void Cleanup()
		{
			m_commandList.Clear();

			m_infoMessage = null;
			m_termCommandList = null;
			m_currentCommand = null;
		}

	}
}
