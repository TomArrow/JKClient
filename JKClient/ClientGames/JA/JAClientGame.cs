using System;

namespace JKClient {
	public class JAClientGame : ClientGame {
		public JAClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum)
			: base(client, serverMessageNum, serverCommandSequence, clientNum) {}
		internal override int GetConfigstringIndex(Configstring index) {
			switch (index) {
			case Configstring.Scores1:
				return (int)ConfigstringJA.Scores1;
			case Configstring.Scores2:
				return (int)ConfigstringJA.Scores2;
			case Configstring.Sounds:
				return (int)ConfigstringJA.Sounds;
			case Configstring.Players:
				return (int)ConfigstringJA.Players;
			case Configstring.LevelStartTime:
				return (int)ConfigstringJA.LevelStartTime;
			case Configstring.FlagStatus:
				return (int)ConfigstringJA.FlagStatus;
			case Configstring.Intermission:
				return (int)ConfigstringJA.Intermission;
			}
			return 0;
		}
		protected override EntityEvent GetEntityEvent(int entityEvent) {
			if (Enum.IsDefined(typeof(EntityEventJA), entityEvent)) {
				switch ((EntityEventJA)entityEvent) {
					case EntityEventJA.VoiceCommandSound:
						return EntityEvent.VoiceCommandSound;
					case EntityEventJA.OBITUARY:
						return EntityEvent.Obituary;
					case EntityEventJA.CTFMESSAGE:
						return EntityEvent.CtfMessage;
					default:break;
				}
			}
			return EntityEvent.None;
		}
		protected override int GetEntityFlag(EntityFlag entityFlag) {
			if (Enum.IsDefined(typeof(EntityFlagJA), (int)entityFlag)) {
				switch (entityFlag) {
				case EntityFlag.TeleportBit:
					return (int)EntityFlagJA.TeleportBit;
				case EntityFlag.PlayerEvent:
					return (int)EntityFlagJA.PlayerEvent;
				}
			}
			return 0;
		}
		protected override int GetEntityType(EntityType entityType) {
			switch (entityType) {
			default:
				return (int)entityType;
			case EntityType.Events:
				return (int)EntityTypeJA.Events;
			case EntityType.Grapple:
				throw new JKClientException($"Invalid entity type: {entityType}");
			}
		}
		protected override EntityEvent HandleEvent(EntityEventData eventData) {
			ref var es = ref eventData.Cent.CurrentState;
			if (es.EntityType == this.GetEntityType(EntityType.NPC)) {
				return EntityEvent.None;
			}
			var ev = base.HandleEvent(eventData);
			switch (ev) {
			case EntityEvent.VoiceCommandSound:
				if (es.GroundEntityNum >= 0 && es.GroundEntityNum < this.Client.MaxClients) {
					int clientNum = es.GroundEntityNum;
					string description = this.Client.GetConfigstring(this.GetConfigstringIndex(Configstring.Sounds) + es.EventParm);
					string message = $"<{this.ClientInfo[clientNum].Name}^7{Common.EscapeCharacter}: {description}>";
					var command = new Command(new string[] { "vchat", message }); // Making it a normal chat kinda interferes with normal handling of chat.
					this.Client.ExecuteServerCommand(new CommandEventArgs(command,-2));
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
			Intermission = 22,
			FlagStatus = 23,
			Sounds = 811,
			Players = 1131
		}
		[Flags]
		public enum EntityFlagJA : int {
			TeleportBit = (1<<3),
			PlayerEvent = (1<<5)
		}
		public enum EntityEventJA : int {
			None,

			CLIENTJOIN,

			FOOTSTEP,
			FOOTSTEP_METAL,
			FOOTSPLASH,
			FOOTWADE,
			SWIM,

			STEP_4,
			STEP_8,
			STEP_12,
			STEP_16,

			FALL,

			JUMP_PAD,           // boing sound at origin, jump sound on player

			GHOUL2_MARK,            //create a projectile impact mark on something with a client-side g2 instance.

			GLOBAL_DUEL,
			PRIVATE_DUEL,

			JUMP,
			ROLL,
			WATER_TOUCH,    // foot touches
			WATER_LEAVE,    // foot leaves
			WATER_UNDER,    // head touches
			WATER_CLEAR,    // head leaves

			ITEM_PICKUP,            // normal item pickups are predictable
			GLOBAL_ITEM_PICKUP, // powerup / team sounds are broadcast to everyone

			VEH_FIRE,

			NOAMMO,
			CHANGE_WEAPON,
			FIRE_WEAPON,
			ALT_FIRE,
			SABER_ATTACK,
			SABER_HIT,
			SABER_BLOCK,
			SABER_CLASHFLARE,
			SABER_UNHOLSTER,
			BECOME_JEDIMASTER,
			DISRUPTOR_MAIN_SHOT,
			DISRUPTOR_SNIPER_SHOT,
			DISRUPTOR_SNIPER_MISS,
			DISRUPTOR_HIT,
			DISRUPTOR_ZOOMSOUND,

			PREDEFSOUND,

			TEAM_POWER,

			SCREENSHAKE,

			LOCALTIMER,

			USE,            // +Use key

			USE_ITEM0,
			USE_ITEM1,
			USE_ITEM2,
			USE_ITEM3,
			USE_ITEM4,
			USE_ITEM5,
			USE_ITEM6,
			USE_ITEM7,
			USE_ITEM8,
			USE_ITEM9,
			USE_ITEM10,
			USE_ITEM11,
			USE_ITEM12,
			USE_ITEM13,
			USE_ITEM14,
			USE_ITEM15,

			ITEMUSEFAIL,

			ITEM_RESPAWN,
			ITEM_POP,
			PLAYER_TELEPORT_IN,
			PLAYER_TELEPORT_OUT,

			GRENADE_BOUNCE,     // eventParm will be the soundindex
			MISSILE_STICK,      // eventParm will be the soundindex

			PLAY_EFFECT,
			PLAY_EFFECT_ID,
			PLAY_PORTAL_EFFECT_ID,

			PLAYDOORSOUND,
			PLAYDOORLOOPSOUND,
			BMODEL_SOUND,

			MUTE_SOUND,
			VoiceCommandSound,
			GENERAL_SOUND,
			GLOBAL_SOUND,       // no attenuation
			GLOBAL_TEAM_SOUND,
			ENTITY_SOUND,

			PLAY_ROFF,

			GLASS_SHATTER,
			DEBRIS,
			MISC_MODEL_EXP,

			CONC_ALT_IMPACT,

			MISSILE_HIT,
			MISSILE_MISS,
			MISSILE_MISS_METAL,
			BULLET,             // otherEntity is the shooter

			PAIN,
			DEATH1,
			DEATH2,
			DEATH3,
			OBITUARY,

			POWERUP_QUAD,
			POWERUP_BATTLESUIT,
			//POWERUP_REGEN,

			FORCE_DRAINED,

			GIB_PLAYER,         // gib a previously living player
			SCOREPLUM,          // score plum

			CTFMESSAGE,

			BODYFADE,

			SIEGE_ROUNDOVER,
			SIEGE_OBJECTIVECOMPLETE,

			DESTROY_GHOUL2_INSTANCE,

			DESTROY_WEAPON_MODEL,

			GIVE_NEW_RANK,
			SET_FREE_SABER,
			SET_FORCE_DISABLE,

			WEAPON_CHARGE,
			WEAPON_CHARGE_ALT,

			SHIELD_HIT,

			DEBUG_LINE,
			TESTLINE,
			STOPLOOPINGSOUND,
			STARTLOOPINGSOUND,
			TAUNT,

			//rww - Begin NPC sound events
			ANGER1, //Say when acquire an enemy when didn't have one before
			ANGER2,
			ANGER3,

			VICTORY1,   //Say when killed an enemy
			VICTORY2,
			VICTORY3,

			CONFUSE1,   //Say when confused
			CONFUSE2,
			CONFUSE3,

			PUSHED1,        //Say when pushed
			PUSHED2,
			PUSHED3,

			CHOKE1,     //Say when choking
			CHOKE2,
			CHOKE3,

			FFWARN,     //ffire founds
			FFTURN,
			//extra sounds for ST
			CHASE1,
			CHASE2,
			CHASE3,
			COVER1,
			COVER2,
			COVER3,
			COVER4,
			COVER5,
			DETECTED1,
			DETECTED2,
			DETECTED3,
			DETECTED4,
			DETECTED5,
			LOST1,
			OUTFLANK1,
			OUTFLANK2,
			ESCAPING1,
			ESCAPING2,
			ESCAPING3,
			GIVEUP1,
			GIVEUP2,
			GIVEUP3,
			GIVEUP4,
			LOOK1,
			LOOK2,
			SIGHT1,
			SIGHT2,
			SIGHT3,
			SOUND1,
			SOUND2,
			SOUND3,
			SUSPICIOUS1,
			SUSPICIOUS2,
			SUSPICIOUS3,
			SUSPICIOUS4,
			SUSPICIOUS5,
			//extra sounds for Jedi
			COMBAT1,
			COMBAT2,
			COMBAT3,
			JDETECTED1,
			JDETECTED2,
			JDETECTED3,
			TAUNT1,
			TAUNT2,
			TAUNT3,
			JCHASE1,
			JCHASE2,
			JCHASE3,
			JLOST1,
			JLOST2,
			JLOST3,
			DEFLECT1,
			DEFLECT2,
			DEFLECT3,
			GLOAT1,
			GLOAT2,
			GLOAT3,
			PUSHFAIL,

			SIEGESPEC,
		}
		public enum EntityTypeJA : int {
			General,
			Player,
			Item,
			Missile,
			Special,
			Holocron,
			Mover,
			Beam,
			Portal,
			Speaker,
			PushTrigger,
			TeleportTrigger,
			Invisible,
			NPC,
			Team,
			Body,
			Terrain,
			FX,
			Events
		}
		public enum EntityTypeMBII : int {
			Events=23
		}
	}
}
