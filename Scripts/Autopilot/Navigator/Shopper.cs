using System;
using System.Collections.Generic; // from mscorlib.dll, System.dll, System.Core.dll, and VRage.Library.dll
using System.Text; // from mscorlib.dll
using Rynchodon.Autopilot.Data;
using Sandbox.Definitions; // from Sandbox.Game.dll
using Sandbox.Game.Entities; // from Sandbox.Game.dll
using Sandbox.ModAPI;
using VRage; // from VRage.dll and VRage.Library.dll
using VRage.Game; // from VRage.Game.dll
using VRage.Game.Entity; // from VRage.Game.dll
using VRage.Game.ModAPI; // from VRage.Game.dll
using VRage.ObjectBuilders;

namespace Rynchodon.Autopilot.Navigator
{
	/// <summary>
	/// Retrieves items from connected grids.
	/// </summary>
	public class Shopper : INavigatorMover
	{
		private const float maxTransfer = 1;

		private readonly Logger m_logger;
		private readonly AllNavigationSettings m_navSet;
		private readonly IMyCubeGrid m_grid;
		private readonly List<IMyInventory> m_destInventory = new List<IMyInventory>();
		private readonly List<IMyInventory> m_sourceInventory = new List<IMyInventory>();

		private Action m_currentTask;
		private ulong m_nextUpdate;
		private Dictionary<string, int> m_shoppingList;

		public Shopper(AllNavigationSettings navSet, IMyCubeGrid grid, Dictionary<string, int> shopList)
		{
			this.m_logger = new Logger(grid);
			this.m_navSet = navSet;
			this.m_shoppingList = shopList;
			this.m_grid = grid;

			this.m_currentTask = Return;

			foreach (var pair in m_shoppingList)
				m_logger.debugLog("Item: " + pair.Key + ", amount: " + pair.Value);
		}

		public void Move()
		{
			if (Globals.UpdateCount < m_nextUpdate)
				return;
			m_nextUpdate = Globals.UpdateCount + 100ul;

			if (m_sourceInventory.Count == 0)
			{
				FindSourceInventory();
				if (m_sourceInventory.Count == 0)
				{
					m_logger.debugLog("No source inventories found", Logger.severity.WARNING);
					m_navSet.OnTaskComplete_NavWay();
					return;
				}
			}

			if (m_shoppingList.Count != 0)
				MyAPIGateway.Utilities.TryInvokeOnGameThread(m_currentTask);
			else
			{
				m_logger.debugLog("Shopping finished, proceed to checkout", Logger.severity.INFO);
				m_navSet.OnTaskComplete_NavWay();
			}
		}

		public void AppendCustomInfo(StringBuilder customInfo)
		{
			if (m_shoppingList.Count == 0)
			{
				customInfo.AppendLine("Finished getting components");
				return;
			}

			var component = m_shoppingList.FirstPair();

			customInfo.Append("Searching for ");
			customInfo.Append(component.Value);
			customInfo.Append(" ");
			customInfo.AppendLine(component.Key);
		}

		/// <summary>
		/// Starts retreiving items, connection needs to be established within 200 updates of invoking this function.
		/// </summary>
		public void Start()
		{
			m_logger.debugLog("shopper started");
			m_navSet.Shopper = null;

			this.m_grid.GetBlocks_Safe(null, slim => {
				MyEntity entity = slim.FatBlock as MyEntity;
				if (entity != null && entity is Sandbox.ModAPI.Ingame.IMyCargoContainer)
				{
					m_logger.debugLog("entity: " + entity.GetBaseEntity().getBestName() + ", inventories: " + entity.InventoryCount);
					int count = entity.InventoryCount;
					for (int i = 0; i < count; i++)
						m_destInventory.Add(entity.GetInventory(i));
				}
				return false;
			});

			if (m_destInventory.Count == 0)
			{
				m_logger.debugLog("no dest inventory");
				return;
			}

			m_navSet.Settings_Task_NavWay.NavigatorMover = this;
			m_navSet.Settings_Task_NavWay.PathfinderCanChangeCourse = false;

			m_nextUpdate = Globals.UpdateCount + 200ul; // give attached a chance to be registered before FindSourceInventory
		}

		/// <summary>
		/// Find inventories that items can be transferred from.
		/// </summary>
		private void FindSourceInventory()
		{
			m_logger.debugLog("looking for attached blocks");

			Attached.AttachedGrid.RunOnAttachedBlock(this.m_grid, Attached.AttachedGrid.AttachmentKind.Terminal, slim => {
				MyEntity entity = slim.FatBlock as MyEntity;
				if (entity == null)
					return false;

				if (entity.HasInventory)
				{
					if ((entity.GetInventory(0) as IMyInventory).IsConnectedTo(m_destInventory[0]))
					{
						m_logger.debugLog("entity: " + entity.GetBaseEntity().getBestName() + ", inventories: " + entity.InventoryCount);
						int count = entity.InventoryCount;
						for (int i = 0; i < count; i++)
							m_sourceInventory.Add(entity.GetInventory(i));
					}
				}
				return false;
			});
		}

		/// <summary>
		/// Puts components back
		/// </summary>
		private void Return()
		{
			float allowedVolume = maxTransfer; // volume is in m� not l

			foreach (IMyInventory sourceInv in m_destInventory)
				if (sourceInv.CurrentVolume > MyFixedPoint.Zero)
				{
					var items = sourceInv.GetItems();
					for (int i = 0; i < items.Count; i++)
					{
						MyObjectBuilderType baseType = items[i].Content.GetType();
						if (baseType != typeof(MyObjectBuilder_Component))
							continue;

						SerializableDefinitionId defId = new SerializableDefinitionId(baseType, items[i].Content.SubtypeName);
						float oneVol = MyDefinitionManager.Static.GetPhysicalItemDefinition(defId).Volume;

						foreach (IMyInventory destInv in m_sourceInventory)
							if (destInv.CurrentVolume < destInv.MaxVolume - 1 && sourceInv.IsConnectedTo(destInv))
							{
								int allowedAmount = (int)(allowedVolume / oneVol);
								if (allowedAmount <= 0)
								{
									m_logger.debugLog("allowedAmount < 0", Logger.severity.FATAL, condition: allowedAmount < 0);
									m_logger.debugLog("reached max transfer for this update", Logger.severity.DEBUG);
									return;
								}

								int invSpace = (int)((float)(destInv.MaxVolume - destInv.CurrentVolume) / oneVol);
								MyFixedPoint transferAmount = Math.Min(allowedAmount, invSpace);
								if (transferAmount > items[i].Amount)
									transferAmount = items[i].Amount;

								if (transferAmount > invSpace)
									transferAmount = invSpace;

								MyFixedPoint volumeBefore = destInv.CurrentVolume;
								if (destInv.TransferItemFrom(sourceInv, 0, stackIfPossible: true, amount: transferAmount))
								{
									//m_logger.debugLog("transfered item from " + (sourceInv.Owner as IMyEntity).getBestName() + " to " + (destInv.Owner as IMyEntity).getBestName() +
									//	", amount: " + transferAmount + ", volume: " + (destInv.CurrentVolume - volumeBefore), "Return()");
									allowedVolume += (float)(volumeBefore - destInv.CurrentVolume);
								}
							}
					}
				}

			m_logger.debugLog("finished emptying inventories", Logger.severity.INFO);
			m_currentTask = Shop;
		}

		/// <summary>
		/// Retreive items.
		/// </summary>
		private void Shop()
		{
			float allowedVolume = maxTransfer; // volume is in m� not l

			var component = m_shoppingList.FirstPair();
			//m_logger.debugLog("shopping for " + component.Key, "Move()");
			SerializableDefinitionId defId = new SerializableDefinitionId(typeof(MyObjectBuilder_Component), component.Key);
			float oneVol = MyDefinitionManager.Static.GetPhysicalItemDefinition(defId).Volume;
			foreach (IMyInventory source in m_sourceInventory)
			{
				//m_logger.debugLog("source: " + (source.Owner as IMyEntity).getBestName(), "Move()");

				int amountToMove, sourceIndex;
				GetComponentInInventory(source, defId, out amountToMove, out sourceIndex);
				if (amountToMove < 1)
					continue;
				//m_logger.debugLog("found: " + component.Key + ", count: " + amountToMove, "Move()");
				if (amountToMove > component.Value)
				{
					//m_logger.debugLog("reducing amountToMove to " + component.Value, "Move()");
					amountToMove = component.Value;
				}

				foreach (IMyInventory destination in m_destInventory)
				{
					if (amountToMove < 1)
						break;

					//m_logger.debugLog("destination: " + (destination.Owner as IMyEntity).getBestName(), "Move()");

					int allowedAmount = (int)(allowedVolume / oneVol);
					if (allowedAmount < 1)
					{
						m_logger.debugLog("allowed amount less than 1");
						return;
					}
					if (allowedAmount < amountToMove)
					{
						//m_logger.debugLog("allowedAmount(" + allowedAmount + ") < amountToMove(" + amountToMove + ")", "Move()");
						amountToMove = allowedAmount;
					}

					MyFixedPoint amountInDest = destination.GetItemAmount(defId);
					if (destination.TransferItemFrom(source, sourceIndex, amount: amountToMove))
					{
						MyFixedPoint amountNow = destination.GetItemAmount(defId);
						if (amountNow == amountInDest)
						{
							m_logger.debugLog("no transfer took place");
						}
						else
						{
							int transferred = (int)(amountNow - amountInDest);
							m_shoppingList[component.Key] -= transferred;
							allowedVolume -= transferred * oneVol;
							amountToMove -= transferred;
							//m_logger.debugLog("transfered: " + transferred + ", remaining volume: " + allowedVolume + " remaining amount: " + amountToMove, "Move()");
							component = m_shoppingList.FirstPair();
							if (amountToMove < 1)
							{
								if (component.Value < 1)
								{
									m_logger.debugLog("final transfer for " + component.Key);
									m_shoppingList.Remove(component.Key);
								}
								return;
							}
						}
					}
					else
						m_logger.debugLog("transfer failed", Logger.severity.WARNING);
				}
			}

			if (allowedVolume == maxTransfer)
			{
				m_logger.debugLog("no items were transferred, dropping item from list: " + component.Key, Logger.severity.DEBUG);
				m_shoppingList.Remove(component.Key);
			}
			else
				m_logger.debugLog("value: " + m_shoppingList[component.Key], Logger.severity.DEBUG);
		}

		/// <summary>
		/// Get the component amount and index from an inventory.
		/// </summary>
		/// <param name="inventory">Inventory to search.</param>
		/// <param name="defId">Component definition</param>
		/// <param name="amount">Amount of components in inventory</param>
		/// <param name="index">Component position in inventory</param>
		private void GetComponentInInventory(IMyInventory inventory, MyDefinitionId defId, out int amount, out int index)
		{
			amount = (int)inventory.GetItemAmount(defId);
			if (amount == 0)
			{
				index = -1;
				return;
			}

			var allItems = inventory.GetItems();
			for (index = 0; index < allItems.Count; index++)
				if (allItems[index].Content.GetId() == defId)
					return;
		}

	}
}
