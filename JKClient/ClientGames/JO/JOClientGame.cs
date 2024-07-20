using System;

namespace JKClient {
	public class JOClientGame : ClientGame {
		public JOClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum)
			: base(client, serverMessageNum, serverCommandSequence, clientNum) {}
		internal override int GetConfigstringIndex(Configstring index) {
			switch (index) {
			case Configstring.Sounds:
				return (int)ConfigstringJO.Sounds;
			case Configstring.Players:
				return (int)ConfigstringJO.Players;
			case Configstring.LevelStartTime:
				return (int)ConfigstringJO.LevelStartTime;
			case Configstring.Scores1:
				return (int)ConfigstringJO.Scores1;
			case Configstring.Scores2:
				return (int)ConfigstringJO.Scores2;
			case Configstring.FlagStatus:
				return (int)ConfigstringJO.FlagStatus;
			case Configstring.Intermission:
				return (int)ConfigstringJO.Intermission;
			}
			return 0;
		}
		protected override EntityEvent GetEntityEvent(int entityEvent) {
			if (Enum.IsDefined(typeof(EntityEventJO), entityEvent)) {
				switch ((EntityEventJO)entityEvent) {
					case EntityEventJO.Obituary:
						return EntityEvent.Obituary;
					case EntityEventJO.CtfMessage:
						return EntityEvent.CtfMessage;
					case EntityEventJO.ForceDrained:
						return EntityEvent.ForceDrained;
					case EntityEventJO.Jump:
						return EntityEvent.Jump;
					default:
						break;
				}
			}
			return EntityEvent.None;
		}
		protected override int GetEntityFlag(EntityFlag entityFlag) {
			if (Enum.IsDefined(typeof(EntityFlagJO), (int)entityFlag)) {
				switch (entityFlag) {
				case EntityFlag.TeleportBit:
					return (int)EntityFlagJO.TeleportBit;
				case EntityFlag.PlayerEvent:
					return (int)EntityFlagJO.PlayerEvent;
				}
			}
			return 0;
		}
		protected override int GetEntityType(EntityType entityType) {
			switch (entityType) {
			default:
				return (int)entityType;
			case EntityType.Grapple:
				return (int)EntityTypeJO.Grapple;
			case EntityType.Events:
				return (int)EntityTypeJO.Events;
			case EntityType.NPC:
			case EntityType.Terrain:
			case EntityType.FX:
				throw new JKClientException($"Invalid entity type: {entityType}");
			}
		}
		public enum ConfigstringJO {
			Scores1 = 6,
			Scores2 = 7,
			LevelStartTime = 21,
			Intermission = 22,
			FlagStatus = 23,
			Sounds = 288,
			Players = 544
		}
		[Flags]
		public enum EntityFlagJO : int {
			TeleportBit = (1<<3),
			PlayerEvent = (1<<5)
		}
		public enum EntityEventJO : int {
			None,

			ClientJoin,

			FootStep,
			FootStepMetal,
			FootSplash,
			FootWade,
			Swim,

			Step4,
			Step8,
			Step12,
			Step16,

			Fall,

			JumpPad,            // BoingSoundatOrigin, JumpSoundonPlayer

			PrivateDuel,

			Jump,
			Roll,
			WaterTouch, // FootTouches
			WaterLeave, // FootLeaves
			WaterUnder, // HeadTouches
			WaterClear, // HeadLeaves

			ItemPickup,         // NormalItemPickupsArePredictable
			GlobalItemPickup,  // Powerup / TeamSoundsAreBroadcasttoEveryone

			NoAmmo,
			ChangeWeapon,
			FireWeapon,
			AltFire,
			SaberAttack,
			SaberHit,
			SaberBlock,
			SaberUnholster,
			BecomeJedimaster,
			DisruptorMainShot,
			DisruptorSniperShot,
			DisruptorSniperMiss,
			DisruptorHit,
			DisruptorZoomSound,

			PredefinedSound,

			TeamPower,

			ScreenShake,

			Use,         // +useKey

			UseItem0,
			UseItem1,
			UseItem2,
			UseItem3,
			UseItem4,
			UseItem5,
			UseItem6,
			UseItem7,
			UseItem8,
			UseItem9,
			UseItem10,
			UseItem11,
			UseItem12,
			UseItem13,
			UseItem14,
			UseItem15,

			ItemUseFail,

			ItemRespawn,
			ItemPop,
			PlayerTeleportin,
			PlayerTeleportout,

			GrenadeBounce,      // EventparmWillBetheSoundindex
			MissileStick,       // EventparmWillBetheSoundindex

			PlayEffect,
			PlayEffectId,

			MuteSound,
			GeneralSound,
			GlobalSound,        // NoAttenuation
			GlobalTeamSound,
			EntitySound,

			PlayRoff,

			GlassShatter,
			Debris,

			MissileHit,
			MissileMiss,
			MissileMissMetal,
			Bullet,              // OtherentityIstheShooter

			Pain,
			Death1,
			Death2,
			Death3,
			Obituary,

			PowerUpQuad,
			PowerUpBattlesuit,
			//PowerupRegen,

			ForceDrained,

			GibPlayer,          // Giba PreviouslyLivingPlayer
			ScorePlum,           // ScorePlum

			CtfMessage,

			SagaRoundover,
			SagaObjectivecomplete,

			DestroyGhoul2Instance,

			DestroyWeaponModel,

			GiveNewRank,
			SetFreeSaber,
			SetForceDisable,

			WeaponCharge,
			WeaponChargeAlt,

			ShieldHit,

			DebugLine,
			TestLine,
			StopLoopingSound,
			StartLoopingSound,
			Taunt,
			TauntYes,
			TauntNo,
			TauntFollowme,
			TauntGetflag,
			TauntGuardbase,
			TauntPatrol,

			BodyQueueCopy,
		}
		public enum EntityTypeJO : int {
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
			Grapple,
			Team,
			Body,
			Events
		}
	}
}
