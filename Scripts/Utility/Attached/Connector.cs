using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace Rynchodon.Attached
{
	public class Connector : AttachableBlockUpdate
	{
		private readonly Logger myLogger;

		public Connector(IMyCubeBlock block)
			: base(block, AttachedGrid.AttachmentKind.Connector)
		{
			myLogger = new Logger("Connector", block);
		}

		protected override AttachableBlockBase GetPartner()
		{
			IMyShipConnector myConn = myBlock as IMyShipConnector;
			if (!myConn.IsConnected)
				return null;

			IMyShipConnector other = myConn.OtherConnector;
			if (other == null)
				return null;
			return GetPartner(other.EntityId);
		}
	}
}
