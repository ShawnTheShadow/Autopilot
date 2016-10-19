using System.Text;
using Rynchodon.Attached;
using Sandbox.Common.ObjectBuilders;
using VRage.Game.ModAPI;

namespace Rynchodon.Autopilot.Navigator
{
	public class Self_Destruct : IEnemyResponse
	{

		private readonly Logger m_logger;
		private readonly IMyCubeBlock m_block;
		private bool m_countingDown;

		public Self_Destruct(IMyCubeBlock block)
		{
			this.m_logger = new Logger(block);
			this.m_block = block;
		}

		public bool CanRespond()
		{
			return !m_countingDown;
		}

		public bool CanTarget(IMyCubeGrid grid)
		{
			return true;
		}

		public void UpdateTarget(AntennaRelay.LastSeen enemy)
		{
			if (enemy == null)
				return;

			AttachedGrid.RunOnAttached(m_block.CubeGrid, AttachedGrid.AttachmentKind.Terminal, grid => {
				var warheads = CubeGridCache.GetFor(grid).GetBlocksOfType(typeof(MyObjectBuilder_Warhead));
				if (warheads != null)
					foreach (var war in warheads)
						if (m_block.canControlBlock(war))
						{
							m_logger.debugLog("Starting countdown for " + war.getBestName(), Logger.severity.DEBUG);
							war.ApplyAction("StartCountdown");
						}
				return false;
			}, true);

			m_countingDown = true;
		}

		public void Move() { }

		public void AppendCustomInfo(StringBuilder customInfo)
		{
			customInfo.AppendLine("Self destruct sequence");
		}

		public void Rotate() { }

	}
}
