using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient
{
    public class MBIIClientGame : ClientGame
    {
		public MBIIClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum)
			: base(client, serverMessageNum, serverCommandSequence, clientNum) { }
		internal override int GetConfigstringIndex(Configstring index)
		{
			switch (index)
			{
				case Configstring.Sounds:
					return (int)ConfigstringJA.Sounds;
				case Configstring.Players:
					return (int)ConfigstringJA.Players;
				case Configstring.LevelStartTime:
					return (int)ConfigstringJA.LevelStartTime;
			}
			return 0;
		}
		protected override EntityEvent GetEntityEvent(int entityEvent)
		{
			if (Enum.IsDefined(typeof(EntityEventMBII), entityEvent))
			{
				switch ((EntityEventMBII)entityEvent)
				{
					case EntityEventMBII.VoiceCommandSound:
					case EntityEventMBII.VoiceCommandSoundSpam:
						return EntityEvent.VoiceCommandSound;
					case EntityEventMBII.OBITUARY:
						return EntityEvent.Obituary;
					case EntityEventMBII.CTFMESSAGE:
						return EntityEvent.CtfMessage;
					default: break;
				}
			}
			return EntityEvent.None;
		}
		protected override int GetEntityFlag(EntityFlag entityFlag)
		{
			if (Enum.IsDefined(typeof(EntityFlagJA), (int)entityFlag))
			{
				switch (entityFlag)
				{
					case EntityFlag.TeleportBit:
						return (int)EntityFlagJA.TeleportBit;
					case EntityFlag.PlayerEvent:
						return (int)EntityFlagJA.PlayerEvent;
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
					return (int)EntityTypeMBII.Events;
				case EntityType.Grapple:
					throw new JKClientException($"Invalid entity type: {entityType}");
			}
		}
		protected override EntityEvent HandleEvent(EntityEventData eventData)
		{
			ref var es = ref eventData.Cent.CurrentState;
			if (es.EntityType == this.GetEntityType(EntityType.NPC))
			{
				return EntityEvent.None;
			}
			var ev = base.HandleEvent(eventData);
			switch (ev)
			{
				case EntityEvent.VoiceCommandSound:
					if (es.GroundEntityNum >= 0 && es.GroundEntityNum < this.Client.MaxClients)
					{
						int clientNum = es.GroundEntityNum;
						string description = this.Client.GetConfigstring(this.GetConfigstringIndex(Configstring.Sounds) + es.EventParm);
						string message = $"<{this.ClientInfo[clientNum].Name}^7{Common.EscapeCharacter}: {description}>";
						var command = new Command(new string[] { "vchat", message }); // Making it a normal chat kinda interferes with normal handling of chat.
						this.Client.ExecuteServerCommand(new CommandEventArgs(command));
					}
					break;
			}
			return ev;
		}
		public enum ConfigstringJA
		{
			Scores1 = 6,
			Scores2 = 7,
			LevelStartTime = 21,
			Sounds = 811,
			Players = 1131
		}
		[Flags]
		public enum EntityFlagJA : int
		{
			TeleportBit = (1 << 3),
			PlayerEvent = (1 << 5)
		}
		public enum EntityEventMBII : int
		{
			None,

			CLIENTJOIN,
			VoiceCommandSound = 78,
			VoiceCommandSoundSpam = 79,
			OBITUARY = 101,
			CTFMESSAGE = 104
		}
		public enum EntityTypeMBII : int
		{
			Events = 23
		}
	}
}
