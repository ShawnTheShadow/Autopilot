﻿#define LOG_ENABLED //remove on build

using System;
using System.Collections.Generic;
//using System.Linq;
//using System.Text;

using Sandbox.Common;
using Sandbox.Common.Components;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Ingame = Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage;

namespace Rynchodon.AntennaRelay
{
	public class LaserAntenna : Receiver
	{
		private static List<LaserAntenna> value_registry = new List<LaserAntenna>();
		public static ReadOnlyList<LaserAntenna> registry { get { return new ReadOnlyList<LaserAntenna>(value_registry); } }

		private Ingame.IMyLaserAntenna myLaserAntenna;
		private Logger myLogger;

		public LaserAntenna(IMyCubeBlock block)
			: base(block)
		{
			myLaserAntenna = CubeBlock as Ingame.IMyLaserAntenna;
			myLogger = new Logger("LaserAntenna", () => CubeBlock.CubeGrid.DisplayName);
			value_registry.Add(this);

			//log("init as antenna: " + CubeBlock.BlockDefinition.SubtypeName, "Init()", Logger.severity.TRACE);
			//EnforcedUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
		}

		protected override void Close(IMyEntity entity)
		{
			try
			{
				if (CubeBlock != null)
					value_registry.Remove(this);
			}
			catch (Exception e)
			{ myLogger.log("exception on removing from registry: " + e, "Close()", Logger.severity.WARNING); }
			CubeBlock = null;
			myLaserAntenna = null;
			myLastSeen = null;
			myMessages = null;
		}

		public void UpdateAfterSimulation100()
		{
			try
			{
				if (!myLaserAntenna.IsWorking)
					return;

				//Showoff.doShowoff(CubeBlock, myLastSeen.Values.GetEnumerator(), myLastSeen.Count);

				// stage 5 is the final stage. It is possible for one to be in stage 5, while the other is not
				MyObjectBuilder_LaserAntenna builder = CubeBlock.getSlim().GetObjectBuilder() as MyObjectBuilder_LaserAntenna;
				if (builder.targetEntityId != null)
					foreach (LaserAntenna lAnt in value_registry)
						if (lAnt.CubeBlock.EntityId == builder.targetEntityId)
							if (builder.State == 5 && (lAnt.CubeBlock.getSlim().GetObjectBuilder() as MyObjectBuilder_LaserAntenna).State == 5)
							{
								//log("Laser " + CubeBlock.gridBlockName() + " connected to " + lAnt.CubeBlock.gridBlockName(), "UpdateAfterSimulation100()", Logger.severity.DEBUG);
								foreach (LastSeen seen in myLastSeen.Values)
									lAnt.receive(seen);
								foreach (Message mes in myMessages)
									lAnt.receive(mes);
								break;
							}

				// send to attached receivers
				Receiver.sendToAttached(CubeBlock, myLastSeen);
				Receiver.sendToAttached(CubeBlock, myMessages);

				UpdateEnemyNear();
			}
			catch (Exception e)
			{ myLogger.log("Exception: " + e, "UpdateAfterSimulation100()", Logger.severity.ERROR); }
		}
	}
}
