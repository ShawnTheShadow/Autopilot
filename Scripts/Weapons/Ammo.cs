﻿using System;
using System.Collections.Generic;
using Rynchodon.AntennaRelay;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;

namespace Rynchodon.Weapons
{
	public class Ammo
	{
		public class AmmoDescription
		{
			public static AmmoDescription CreateFrom(MyAmmoDefinition ammo)
			{
				if (string.IsNullOrWhiteSpace(ammo.DescriptionString))
					return null;

				AmmoDescription desc = new AmmoDescription(ammo.Id.SubtypeName);
				try
				{
					XML_Amendments<AmmoDescription> ammender = new XML_Amendments<AmmoDescription>(desc);
					ammender.primarySeparator = new char[] { ';' };
					ammender.AmendAll(ammo.DescriptionString, true);
					return ammender.Deserialize();
				}
				catch (Exception ex)
				{
					Logger.debugNotify("Failed to load description for an ammo", 10000, Logger.severity.ERROR);
					desc.myLogger.alwaysLog("Failed to load description for an ammo", "CreateFrom()", Logger.severity.ERROR);
					desc.myLogger.alwaysLog("Exception: " + ex, "CreateFrom()", Logger.severity.ERROR);
					return null;
				}
			}

			public float GuidanceSeconds;

			#region Performance

			public float RotationPerUpdate = 0.0349065850398866f; // 2°
			/// <summary>In metres per second</summary>
			public float Acceleration;

			/// <summary>For ICBM, distance from launcher when boost phase ends</summary>
			public float BoostDistance;

			#endregion Performance
			#region Tracking

			/// <summary>Range of turret magic.</summary>
			public float TargetRange;
			/// <summary>If true, missile can receive LastSeen information from radio antennas.</summary>
			public bool HasAntenna;
			/// <summary>Description of radar equipment</summary>
			public string Radar = string.Empty;
			/// <summary>If true, is a semi-active laser homing missile, superseeds other targeting.</summary>
			public bool SemiActiveLaser;

			#endregion Tracking
			#region Payload

			/// <summary>Detonate when this close to target.</summary>
			public float DetonateRange;

			public int EMP_Strength;
			public float EMP_Seconds;

			#region Cluster

			/// <summary>Seconds from last cluster missile being fired until it can fire again.</summary>
			public float ClusterCooldown;

			#endregion Cluster
			#endregion Payload

			private readonly Logger myLogger;

			public AmmoDescription()
			{
				myLogger = new Logger("AmmoDescription", null, () => "Deserialized");
			}

			private AmmoDescription(string SubtypeName)
			{
				myLogger = new Logger("AmmoDescription", null, () => SubtypeName);
			}
		}

		private static Dictionary<MyDefinitionId, Ammo> KnownDefinitions_Ammo = new Dictionary<MyDefinitionId, Ammo>();

		static Ammo()
		{
			MyAPIGateway.Entities.OnCloseAll += Entities_OnCloseAll;
		}

		private static void Entities_OnCloseAll()
		{
			MyAPIGateway.Entities.OnCloseAll -= Entities_OnCloseAll;
			KnownDefinitions_Ammo = null;
		}

		public static Ammo GetLoadedAmmo(IMyCubeBlock weapon)
		{
			MyEntity entity = (MyEntity)weapon;
			if (!entity.HasInventory)
				throw new InvalidOperationException("Has no inventory: " + weapon.getBestName());

			MyInventoryBase inv = entity.GetInventoryBase(0);
			if (inv.GetItemsCount() == 0)
				return null;

			MyDefinitionId magazineId;
			try { magazineId = inv.GetItems()[0].Content.GetId(); }
			catch (IndexOutOfRangeException)
			{ return null; }
			Ammo value;
			if (KnownDefinitions_Ammo.TryGetValue(magazineId, out value))
				return value;

			MyAmmoMagazineDefinition magDef = MyDefinitionManager.Static.GetAmmoMagazineDefinition(magazineId);
			if (magDef == null)
				throw new InvalidOperationException("inventory contains item that is not a magazine: " + weapon.getBestName());

			value = new Ammo(magDef);
			KnownDefinitions_Ammo.Add(magazineId, value);
			return value;
		}

		public readonly MyAmmoDefinition AmmoDefinition;
		public readonly MyMissileAmmoDefinition MissileDefinition;
		public readonly MyAmmoMagazineDefinition MagazineDefinition;

		public readonly float TimeToMaxSpeed;
		public readonly float DistanceToMaxSpeed;

		public readonly AmmoDescription Description;
		public readonly RadarEquipment.Definition RadarDefinition;

		public readonly bool IsCluster;

		private readonly Logger myLogger;

		private Ammo(MyAmmoMagazineDefinition ammoMagDef)
		{
			MyAmmoDefinition ammoDef = MyDefinitionManager.Static.GetAmmoDefinition(ammoMagDef.AmmoDefinitionId);
			this.myLogger = new Logger("Ammo", () => ammoMagDef.Id.ToString(), () => ammoDef.Id.ToString());

			this.AmmoDefinition = ammoDef;
			this.MissileDefinition = AmmoDefinition as MyMissileAmmoDefinition;
			this.MagazineDefinition = ammoMagDef;

			if (MissileDefinition != null && !MissileDefinition.MissileSkipAcceleration)
			{
				this.TimeToMaxSpeed = (MissileDefinition.DesiredSpeed - MissileDefinition.MissileInitialSpeed) / MissileDefinition.MissileAcceleration;
				this.DistanceToMaxSpeed = (MissileDefinition.DesiredSpeed + MissileDefinition.MissileInitialSpeed) / 2 * TimeToMaxSpeed;
			}
			else
			{
				this.TimeToMaxSpeed = 0;
				this.DistanceToMaxSpeed = 0;
			}

			Description = AmmoDescription.CreateFrom(AmmoDefinition);

			if (Description == null)
				return;

			if (Description.ClusterCooldown > 0f)
			{
				myLogger.debugLog("Is a cluster missile", "Ammo()");
				IsCluster = true;
			}
			if (!string.IsNullOrWhiteSpace(Description.Radar))
			{
				try
				{
					RadarDefinition = new RadarEquipment.Definition();
					XML_Amendments<RadarEquipment.Definition> ammender = new XML_Amendments<RadarEquipment.Definition>(RadarDefinition);
					ammender.primarySeparator = new char[] { ',' };
					ammender.AmendAll(Description.Radar, true);
					RadarDefinition = ammender.Deserialize();
					myLogger.debugLog("Loaded description for radar", "Ammo()", Logger.severity.DEBUG);
				}
				catch (Exception ex)
				{
					Logger.debugNotify("Failed to load radar description for an ammo", 10000, Logger.severity.ERROR);
					myLogger.alwaysLog("Failed to load radar description for an ammo", "Ammo()", Logger.severity.ERROR);
					myLogger.alwaysLog("Exception: " + ex, "Ammo()", Logger.severity.ERROR);
					RadarDefinition = null;
				}
			}
		}

		public float MissileSpeed(float distance)
		{
			myLogger.debugLog("distance = " + distance + ", DistanceToMaxSpeed = " + DistanceToMaxSpeed, "LoadedAmmoSpeed()");
			if (distance < DistanceToMaxSpeed)
			{
				float finalSpeed = (float)Math.Sqrt(MissileDefinition.MissileInitialSpeed * MissileDefinition.MissileInitialSpeed + 2 * MissileDefinition.MissileAcceleration * distance);

				//myLogger.debugLog("close missile calc: " + ((missileAmmo.MissileInitialSpeed + finalSpeed) / 2), "LoadedAmmoSpeed()");
				return (MissileDefinition.MissileInitialSpeed + finalSpeed) / 2;
			}
			else
			{
				float distanceAfterMaxVel = distance - DistanceToMaxSpeed;
				float timeAfterMaxVel = distanceAfterMaxVel / MissileDefinition.DesiredSpeed;

				myLogger.debugLog("DistanceToMaxSpeed = " + DistanceToMaxSpeed + ", TimeToMaxSpeed = " + TimeToMaxSpeed + ", distanceAfterMaxVel = " + distanceAfterMaxVel + ", timeAfterMaxVel = " + timeAfterMaxVel
					+ ", average speed = " + (distance / (TimeToMaxSpeed + timeAfterMaxVel)), "LoadedAmmoSpeed()");
				//myLogger.debugLog("far missile calc: " + (distance / (LoadedAmmo.TimeToMaxSpeed + timeAfterMaxVel)), "LoadedAmmoSpeed()");
				return distance / (TimeToMaxSpeed + timeAfterMaxVel);
			}
		}

	}
}
