using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient
{
	public class MOHClientGame : ClientGame
	{
		private bool isExpansion = false;

		public MOHClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum)
			: base(client, serverMessageNum, serverCommandSequence, clientNum) {
			isExpansion = client.Protocol > (int)ProtocolVersion.Protocol8;
		}
		internal override int GetConfigstringIndex(Configstring index)
		{
			switch (index)
			{
				case Configstring.Sounds:
					return (int)ConfigstringMOH.Sounds;
				case Configstring.Players:
					return (int)ConfigstringMOH.Players;
				case Configstring.LevelStartTime:
					return isExpansion ? (int)ConfigstringMOH.LevelStartTime : (int)ConfigstringMOH.LevelStartTimeOld;
			}
			return 0;
		}
		protected override EntityEvent GetEntityEvent(int entityEvent)
		{
			// MOH doesn't relaly have events in that sense
			return EntityEvent.None;
		}
		protected override int GetEntityFlag(EntityFlag entityFlag)
		{
			if (Enum.IsDefined(typeof(EntityFlagJO), (int)entityFlag))
			{
				switch (entityFlag)
				{
					case EntityFlag.TeleportBit:
						return (int)EntityFlagJO.TeleportBit;
					case EntityFlag.PlayerEvent:
						return 0; // Doesn't really exist. Hope this will be ok LOL
				}
			}
			return 0;
		}
		protected override int GetEntityType(EntityType entityType)
		{
			switch (entityType)
			{
				default:
					return (int)entityType;
				case EntityType.Events:
					return (int)EntityTypeMOH.Events;
				case EntityType.Player:
					return (int)EntityTypeMOH.Player;
				case EntityType.NPC:
				case EntityType.Terrain:
				case EntityType.Grapple:
				case EntityType.FX:
					throw new JKClientException($"Invalid entity type: {entityType}");
			}
		}
		public enum ConfigstringMOH
		{
			LevelStartTime = 10,
			LevelStartTimeOld = 12,
			Sounds = 1076,
			Players = 1684
		}
		[Flags]
		public enum EntityFlagJO : int
		{
			TeleportBit = 0x00000004,
			//PlayerEvent = 0x00000010 // Not really valid in MOH I think
		}
		
		// No events. Events pretty much don't exist in MOH in that way.

		public enum EntityTypeMOH : int
		{
			ModelAnimSkel,
			ModelAnim,
			Vehicle,
			Player,
			Item,
			General,
			Missile,
			Mover,
			Beam,
			MultiBeam,
			Portal,
			EventOnly,
			Rain,
			Leaf,
			Speaker,
			PushTrigger,
			TeleportTrigger,
			Decal,
			Emitter,
			Rope,
			Events,
			ExecCommands
		}
	}
}
