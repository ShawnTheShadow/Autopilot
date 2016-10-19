using System.Collections.Generic;
using System.Text; // from mscorlib.dll
using Rynchodon.Autopilot.Data;
using Rynchodon.Autopilot.Movement;
using Sandbox.Common.ObjectBuilders; // from MedievalEngineers.ObjectBuilders.dll and SpaceEngineers.ObjectBuilders.dll
using Sandbox.Game.Entities;
using Sandbox.ModAPI; // from Sandbox.Common.dll
using VRage.Game.Entity;
using VRage.Game.ModAPI; // from VRage.Math.dll
using VRageMath;

namespace Rynchodon.Autopilot.Navigator
{

	public class WeldBlock : NavigatorMover, INavigatorRotator
	{

		private const float OffsetAdd = 5f;
		private const ulong TimeoutStart = 1200ul;

		private enum Stage : byte { Lineup, Approach, Weld, Retreat }

		private readonly Logger m_logger;
		private readonly PseudoBlock m_welder;
		private readonly List<Vector3I> m_neighbours = new List<Vector3I>();
		private readonly List<Vector3I> m_emptyNeighbours = new List<Vector3I>();
		private readonly float m_offset, m_slimTarget_initDmg;

		private IMySlimBlock m_targetSlim;
		private IMyCubeGrid m_otherGrid;
		private Vector3I m_targetCell;
		private bool m_weldProjection;

		private IMyCubeGrid m_realGrid
		{ get { return m_weldProjection ? m_otherGrid : m_targetSlim.CubeGrid; } }

		private bool m_weldersEnabled;
		private float m_damage;
		private ulong m_timeout_start, m_lastWeld;
		private Vector3D m_targetWorld;
		private Vector3I? m_closestEmptyNeighbour;
		private Stage value_stage;
		private readonly LineSegmentD m_lineUp = new LineSegmentD();

		private Stage m_stage
		{
			get { return value_stage; }
			set
			{
				if (value_stage == value)
					return;

				m_logger.debugLog("stage changed from " + value_stage + " to " + value, Logger.severity.DEBUG);
				value_stage = value;
				m_navSet.OnTaskComplete_NavWay();

				switch (value_stage)
				{
					case Stage.Approach:
						m_lastWeld = Globals.UpdateCount;
						break;
					case Stage.Weld:
						m_lastWeld = Globals.UpdateCount;
						m_navSet.Settings_Task_NavWay.DestinationEntity = m_realGrid;
						m_navSet.Settings_Task_NavWay.SpeedMaxRelative = 1f;
						break;
				}
			}
		}

		public WeldBlock(Mover mover, AllNavigationSettings navSet, PseudoBlock welder, IMySlimBlock block)
			: base(mover)
		{
			this.m_logger = new Logger(() => mover.Block.CubeGrid.DisplayName, () => block.getBestName(), () => m_stage.ToString());
			this.m_offset = welder.Block.LocalAABB.GetLongestDim() * 0.5f; // this works for default welders, may not work if mod has an exotic design
			this.m_welder = welder;
			this.m_targetSlim = block;
			this.m_timeout_start = Globals.UpdateCount + TimeoutStart;

			IMyCubeBlock Projector = ((MyCubeGrid)block.CubeGrid).Projector;
			if (Projector != null)
			{
				this.m_weldProjection = true;
				this.m_otherGrid = Projector.CubeGrid;
				this.m_slimTarget_initDmg = 1f;
				this.m_targetCell = Projector.CubeGrid.WorldToGridInteger(block.CubeGrid.GridIntegerToWorld(block.Position));
			}
			else
			{
				this.m_weldProjection = false;
				this.m_slimTarget_initDmg = block.Damage();
				this.m_targetCell = block.Position;
			}

			m_navSet.Settings_Task_NavEngage.NavigatorMover = this;
			m_navSet.Settings_Task_NavEngage.NavigatorRotator = this;
			m_navSet.Settings_Task_NavEngage.DestinationEntity = m_realGrid;

			IEnumerator<Vector3I> neighbours = this.m_targetSlim.ForEachNeighbourCell();
			while (neighbours.MoveNext())
			{
				Vector3I cell = m_weldProjection ? Projector.CubeGrid.WorldToGridInteger(block.CubeGrid.GridIntegerToWorld(neighbours.Current)) : neighbours.Current;
				m_neighbours.Add(cell);
				if (this.m_realGrid.GetCubeBlock(cell) == null)
					m_emptyNeighbours.Add(cell);
			}

			m_targetSlim.ComputeWorldCenter(out m_targetWorld);
			m_lineUp.To = m_targetWorld;
		}

		public override void Move()
		{
			if (m_targetSlim.Closed())
			{
				m_logger.debugLog("target block closed: " + m_targetSlim.getBestName(), Logger.severity.INFO);
				m_navSet.OnTaskComplete_NavEngage();
				EnableWelders(false);
				return;
			}

			m_targetSlim.ComputeWorldCenter(out m_targetWorld);

			if (m_stage == Stage.Retreat)
			{
				Retreat();
				return;
			}

			float offsetSquared = m_offset + OffsetAdd + OffsetAdd; offsetSquared *= offsetSquared;

			if (Vector3.DistanceSquared(m_welder.WorldPosition, m_targetWorld) > offsetSquared)
			{
				EnableWelders(false);

				if (m_closestEmptyNeighbour.HasValue && Globals.UpdateCount > m_timeout_start)
				{
					m_logger.debugLog("failed to start, dropping neighbour: " + m_closestEmptyNeighbour, Logger.severity.DEBUG);

					if (m_emptyNeighbours.Count > 1)
					{
						m_emptyNeighbours.Remove(m_closestEmptyNeighbour.Value);
						m_closestEmptyNeighbour = null;
					}
					else
					{
						m_logger.debugLog("tried every empty neighbour, giving up", Logger.severity.INFO);

						EnableWelders(false);
						m_stage = Stage.Retreat;
						return;
					}
				}

				if (!m_closestEmptyNeighbour.HasValue)
				{
					GetClosestEmptyNeighbour();

					if (!m_closestEmptyNeighbour.HasValue)
					{
						m_logger.debugLog("tried every empty neighbour, giving up", Logger.severity.INFO);

						EnableWelders(false);
						m_stage = Stage.Retreat;
						return;
					}

					m_timeout_start = Globals.UpdateCount + TimeoutStart;
					Vector3D from = m_realGrid.GridIntegerToWorld(m_closestEmptyNeighbour.Value);
					m_lineUp.From = m_lineUp.To + (from - m_lineUp.To) * 100d;
				}

				Vector3 closestPoint = m_lineUp.ClosestPoint(m_welder.WorldPosition);
				if (Vector3.DistanceSquared(m_welder.WorldPosition, closestPoint) > 1f || m_navSet.Settings_Current.DistanceAngle > 0.1f)
				{
					m_stage = Stage.Lineup;
					m_mover.CalcMove(m_welder, m_lineUp.ClosestPoint(m_welder.WorldPosition), m_realGrid.Physics.LinearVelocity, false);
					return;
				}
				else // linedup up
					m_stage = Stage.Approach;
			}
			else // near target
			{
				m_stage = Stage.Weld;
				EnableWelders(true);
			}

			MoveToTarget();
		}

		private void Retreat()
		{
			float minDist = m_offset + 10f; minDist *= minDist;
			if (Vector3D.DistanceSquared(m_welder.WorldPosition, m_targetWorld) > minDist)
			{
				m_logger.debugLog("moved away from: " + m_targetSlim.getBestName(), Logger.severity.DEBUG);
				if (!m_weldProjection && m_targetSlim.Damage() < m_slimTarget_initDmg)
				{
					m_logger.debugLog("assuming ship ran out of components, damage: " + m_targetSlim.Damage(), condition: m_targetSlim.Damage() != 0f);
					m_navSet.OnTaskComplete_NavEngage();
					m_mover.StopMove();
					m_mover.StopRotate();
					return;
				}
				else
				{
					m_logger.debugLog("no welding done, trying another approach");
					if (m_emptyNeighbours.Count > 1)
					{
						// if we were very close when we started, no neighbour would have been chosen
						if (m_closestEmptyNeighbour.HasValue)
						{
							m_emptyNeighbours.Remove(m_closestEmptyNeighbour.Value);
							m_closestEmptyNeighbour = null;
						}
						m_stage = Stage.Lineup;
						return;
					}
					else
					{
						m_logger.debugLog("tried every empty neighbour, giving up", Logger.severity.INFO);
						m_navSet.OnTaskComplete_NavEngage();
						m_mover.StopMove();
						m_mover.StopRotate();
						return;
					}
				}
			}
			Vector3D direction = m_welder.WorldPosition - m_targetWorld;
			direction.Normalize();
			Vector3D destination = m_welder.WorldPosition + direction * 10d;
			m_mover.CalcMove(m_welder, destination, m_realGrid.Physics.LinearVelocity, true);
		}

		private void MoveToTarget()
		{
			m_logger.debugLog("moving to target in wrong stage", Logger.severity.FATAL, condition: m_stage != Stage.Approach && m_stage != Stage.Weld);

			if ((Globals.UpdateCount - m_lastWeld) > 1200ul)
			{
				m_logger.debugLog("failed to repair block");
				EnableWelders(false);
				m_stage = Stage.Retreat;
				return;
			}

			if (m_weldProjection)
			{
				// check for slim being placed
				IMySlimBlock placed = m_realGrid.GetCubeBlock(m_targetCell);
				if (placed != null)
				{
					m_logger.debugLog("projected target placed", Logger.severity.DEBUG);
					m_weldProjection = false;
					m_targetSlim = placed;
				}
			}

			// check for weld

			float damage = m_targetSlim.Damage();
			foreach (Vector3I cell in m_neighbours)
			{
				IMySlimBlock slim = m_realGrid.GetCubeBlock(cell);
				if (slim != null)
					damage += slim.Damage();
			}

			if (damage < m_damage)
				m_lastWeld = Globals.UpdateCount;

			m_damage = damage;

			if (!m_weldProjection && m_targetSlim.Damage() == 0f && (Globals.UpdateCount - m_lastWeld) > 120ul)
			{
				m_logger.debugLog("target block repaired: " + m_targetSlim.getBestName(), Logger.severity.DEBUG);
				EnableWelders(false);
				m_stage = Stage.Retreat;
				return;
			}
			else
			{
				float offset = m_stage == Stage.Weld ? m_offset : m_offset + OffsetAdd;
				Vector3D welderFromTarget = m_controlBlock.CubeBlock.GetPosition() - m_targetWorld;
				welderFromTarget.Normalize();
				m_mover.CalcMove(m_welder, m_targetWorld + welderFromTarget * offset, m_realGrid.Physics.LinearVelocity, true);
			}
		}

		public void Rotate()
		{
			m_mover.CalcRotate(m_welder, RelativeDirection3F.FromWorld(m_welder.Grid, m_targetWorld - m_welder.WorldPosition));
		}

		public override void AppendCustomInfo(StringBuilder customInfo)
		{
			customInfo.Append(m_stage);
			customInfo.Append(": ");
			customInfo.AppendLine(m_targetSlim.getBestName());
		}

		/// <summary>
		/// Enabled/disable all welders.
		/// </summary>
		private void EnableWelders(bool enable)
		{
			if (enable == m_weldersEnabled)
				return;
			m_weldersEnabled = enable;

			if (enable)
				m_logger.debugLog("Enabling welders", Logger.severity.DEBUG);
			else
				m_logger.debugLog("Disabling welders", Logger.severity.DEBUG);

			var cache = CubeGridCache.GetFor(m_controlBlock.CubeGrid);
			if (cache == null)
			{
				m_logger.debugLog("Failed to get cache", Logger.severity.INFO);
				return;
			}
			var allWelders = cache.GetBlocksOfType(typeof(MyObjectBuilder_ShipWelder));
			if (allWelders == null)
			{
				m_logger.debugLog("Failed to get block list", Logger.severity.INFO);
				return;
			}

			MyAPIGateway.Utilities.TryInvokeOnGameThread(() => {
				foreach (IMyShipWelder welder in allWelders)
					if (!welder.Closed)
						welder.RequestEnable(enable);
			});
		}

		private void GetClosestEmptyNeighbour()
		{
			double closest = float.MaxValue;

			List<Vector3I> removeList = null;

			foreach (Vector3I emptyCell in m_emptyNeighbours)
			{
				IMySlimBlock slim = m_realGrid.GetCubeBlock(emptyCell);
				if (slim != null)
				{
					m_logger.debugLog("block placed at " + emptyCell, Logger.severity.DEBUG);
					if (removeList == null)
						removeList = new List<Vector3I>();
					removeList.Add(emptyCell);
					continue;
				}

				Vector3D emptyPos = m_realGrid.GridIntegerToWorld(emptyCell);
				double dist = Vector3D.DistanceSquared(m_welder.WorldPosition, emptyPos);
				if (dist < closest)
				{
					closest = dist;
					m_closestEmptyNeighbour = emptyCell;
				}
			}

			if (removeList != null)
				foreach (Vector3I cell in removeList)
					m_emptyNeighbours.Remove(cell);

			m_logger.debugLog(() => "closest cell: " + m_closestEmptyNeighbour + ", closest position: " + m_realGrid.GridIntegerToWorld(m_closestEmptyNeighbour.Value), Logger.severity.DEBUG, condition: m_closestEmptyNeighbour.HasValue);
		}

	}
}
