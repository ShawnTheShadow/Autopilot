using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Game.Entities.Cube;
//using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace Rynchodon.AntennaRelay
{
	/// <summary>
	/// For players sending a message through the terminal.
	/// </summary>
	class ManualMessage
	{

		public class StaticVariables
		{
			public Logger logger = new Logger("ManualMessage");

            /* var SendMessageButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm>("ManualMessageId");
             SendMessageButton.Title = MyStringId.GetOrCompute("Send Message");
             SendMessageButton.Tooltip = MyStringId.GetOrCompute("Send a message to an Autopilot or Programmable block");
             //public MyTerminalControlButton<MyFunctionalBlock>
                 //SendMessageButton = new MyTerminalControlButton<MyFunctionalBlock>("ManualMessageId", MyStringId.GetOrCompute("Send Message"),
                     //MyStringId.GetOrCompute("Send a message to an Autopilot or Programmable block"), SendMessage) { SupportsMultipleBlocks = false },

                 AbortMessageButton = new MyTerminalControlButton<MyFunctionalBlock>("Abort", MyStringId.GetOrCompute("Abort"),
                     MyStringId.GetOrCompute("Return to main terminal screen without sending a message"), Abort) { SupportsMultipleBlocks = false };

             public MyTerminalControlTextbox<MyFunctionalBlock>
                 TargetShipName = new MyTerminalControlTextbox<MyFunctionalBlock>("TargetShipName", MyStringId.GetOrCompute("Ship Name(s)"),
                     MyStringId.GetOrCompute("The name of the ship(s) that will receive the message")) { SupportsMultipleBlocks = false, Getter = GetTargetShipName, Setter = SetTargetShipName },

                 TargetBlockName = new MyTerminalControlTextbox<MyFunctionalBlock>("TargetBlockName", MyStringId.GetOrCompute("Block Name(s)"),
                     MyStringId.GetOrCompute("The name of the block(s) that will receive the message")) { SupportsMultipleBlocks = false, Getter = GetTargetBlockName, Setter = SetTargetBlockName },

                 Message = new MyTerminalControlTextbox<MyFunctionalBlock>("MessageToSend", MyStringId.GetOrCompute("Message"),
                     MyStringId.GetOrCompute("The message to send")) { SupportsMultipleBlocks = false, Getter = GetMessage, Setter = SetMessage };*/
            private List<IMyTerminalControl> m_customControls = new List<IMyTerminalControl>();
            public IMyTerminalControlButton SendMessageButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm>("ManualMessageId");
            public IMyTerminalControlButton AbortMessageButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm>("Abort");
            public IMyTerminalControlTextbox TargetShipName = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm>("TargetShipName");
            public IMyTerminalControlTextbox TargetBlockName = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm>("TargetBlockName");
            public IMyTerminalControlTextbox Message = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm>("MessageToSend");


            public void Initialize()
            {
                //SendMessageButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm>("ManualMessageId");
                SendMessageButton.Title = MyStringId.GetOrCompute("Send Message");
                SendMessageButton.Tooltip = MyStringId.GetOrCompute("Send a message to an Autopilot or Programmable block");
                SendMessageButton.SupportsMultipleBlocks = false;

                //AbortMessageButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm>("Abort");
                AbortMessageButton.Title = MyStringId.GetOrCompute("Abort");
                AbortMessageButton.Tooltip = MyStringId.GetOrCompute("Return to main terminal screen without sending a message");
                AbortMessageButton.SupportsMultipleBlocks = false;

                //TargetShipName = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm>("TargetShipName");
                TargetShipName.Title = MyStringId.GetOrCompute("Ship Name(s)");
                TargetShipName.Tooltip = MyStringId.GetOrCompute("The name of the ship(s) that will receive the message");
                TargetShipName.SupportsMultipleBlocks = false;
                TargetShipName.Getter = GetTargetShipName;
                TargetShipName.Setter = SetTargetShipName;

                //TargetBlockName = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm>("TargetBlockName");
                TargetBlockName.Title = MyStringId.GetOrCompute("Block Name(s)");
                TargetBlockName.Tooltip = MyStringId.GetOrCompute("The name of the block(s) that will receive the message");
                TargetBlockName.SupportsMultipleBlocks = false;
                TargetBlockName.Getter = GetTargetBlockName;
                TargetBlockName.Setter = SetTargetBlockName;

                //Message = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm>("MessageToSend");
                Message.Title = MyStringId.GetOrCompute("Message");
                Message.Tooltip = MyStringId.GetOrCompute("The message to send");
                Message.SupportsMultipleBlocks = false;
                Message.Getter = GetMessage;
                Message.Setter = SetMessage;
            }
		}
		private static StaticVariables Static = new StaticVariables();

		static ManualMessage()
		{
            MyAPIGateway.TerminalControls.CustomControlGetter += CustomHandler;
			MyAPIGateway.Entities.OnCloseAll += Entities_OnCloseAll;
            Static.Initialize();
		}

		private static void Entities_OnCloseAll()
		{
			MyAPIGateway.Entities.OnCloseAll -= Entities_OnCloseAll;
			MyAPIGateway.TerminalControls.CustomControlGetter -= CustomHandler;
			Static = null;
		}

		public static void CustomHandler(IMyTerminalBlock block, List<IMyTerminalControl> controlList)
		{
			ManualMessage instance;
			if (!Registrar.TryGetValue(block.EntityId, out instance))
				return;

			if (instance.m_sending)
			{
				controlList.Clear();
				controlList.Add(Static.AbortMessageButton);
				controlList.Add(Static.TargetShipName);
				controlList.Add(Static.TargetBlockName);
				controlList.Add(Static.Message);
				controlList.Add(Static.SendMessageButton);
			}
			else
			{
				controlList.Add(Static.SendMessageButton);
			}
		}

		private static void SendMessage(IMyFunctionalBlock block)
		{
			ManualMessage instance;
			if (!Registrar.TryGetValue(block.EntityId, out instance))
				throw new ArgumentException("block id not found in registrar");

			block.SwitchTerminalTo();

			if (instance.m_sending)
			{
				if (instance.m_targetShipName.Length < 3)
				{
					block.AppendCustomInfo("Ship Name(s) must be at least 3 characters");
					return;
				}
				if (instance.m_targetBlockName.Length < 3)
				{
					block.AppendCustomInfo("Block Name(s) must be at least 3 characters");
					return;
				}

				int count = Message.CreateAndSendMessage(block.EntityId, instance.m_targetShipName.ToString(), instance.m_targetBlockName.ToString(), instance.m_message.ToString());
				if (MyAPIGateway.Session.Player != null)
					(block as IMyTerminalBlock).AppendCustomInfo("Sent message to " + count + " block" + (count == 1 ? "" : "s"));

				instance.m_sending = false;
			}
			else
			{
				instance.m_sending = true;
			}
		}

		private static void Abort(IMyFunctionalBlock block)
		{
			ManualMessage instance;
			if (!Registrar.TryGetValue(block.EntityId, out instance))
				throw new ArgumentException("block id not found in registrar");

			instance.m_sending = false;
			block.SwitchTerminalTo();
		}

		#region Getter & Setter

		private static StringBuilder GetTargetShipName(IMyTerminalBlock block)
		{
			ManualMessage instance;
			if (!Registrar.TryGetValue(block.EntityId, out instance))
				throw new ArgumentException("block id not found in registrar");

			return instance.m_targetShipName;
		}

		private static void SetTargetShipName(IMyTerminalBlock block, StringBuilder value)
		{
			ManualMessage instance;
			if (!Registrar.TryGetValue(block.EntityId, out instance))
				throw new ArgumentException("block id not found in registrar");

			instance.m_targetShipName = value;
		}

		private static StringBuilder GetTargetBlockName(IMyTerminalBlock block)
		{
			ManualMessage instance;
			if (!Registrar.TryGetValue(block.EntityId, out instance))
				throw new ArgumentException("block id not found in registrar");

			return instance.m_targetBlockName;
		}

		private static void SetTargetBlockName(IMyTerminalBlock block, StringBuilder value)
		{
			ManualMessage instance;
			if (!Registrar.TryGetValue(block.EntityId, out instance))
				throw new ArgumentException("block id not found in registrar");

			instance.m_targetBlockName = value;
		}

		private static StringBuilder GetMessage(IMyTerminalBlock block)
		{
			ManualMessage instance;
			if (!Registrar.TryGetValue(block.EntityId, out instance))
				throw new ArgumentException("block id not found in registrar");

			return instance.m_message;
		}

		private static void SetMessage(IMyTerminalBlock block, StringBuilder value)
		{
			ManualMessage instance;
			if (!Registrar.TryGetValue(block.EntityId, out instance))
				throw new ArgumentException("block id not found in registrar");

			instance.m_message = value;
		}

		#endregion Getter & Setter

		private readonly Logger m_logger;
		private readonly IMyCubeBlock m_block;

		private bool m_sending;
		private StringBuilder m_targetShipName = new StringBuilder(), m_targetBlockName = new StringBuilder(), m_message = new StringBuilder();

		public ManualMessage(IMyCubeBlock block)
		{
			m_logger = new Logger(GetType().Name, block);
			m_block = block;

			Registrar.Add(block, this);
		}

	}
}
