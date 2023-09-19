using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace JKClient
{

	enum GameTypeMOH
	{
		GT_SINGLE_PLAYER,   // single player
		GT_FFA,             // free for all
		GT_TEAM,            // team deathmatch
		GT_TEAM_ROUNDS,
		GT_OBJECTIVE,
		// Team Assault game mode
		GT_TOW,
		// Team Tactics game mode
		GT_LIBERATION,
		GT_MAX_GAME_TYPE
	}
	public class MOHClientHandler : MOHNetHandler, IClientHandler
	{
		public virtual new ProtocolVersion Protocol => (ProtocolVersion)base.Protocol;
		public virtual ClientVersion Version { get; private set; }
		public virtual int MaxReliableCommands => 512;
		public virtual int MaxConfigstrings => 2736;
		public virtual int MaxClients => 64;
		public virtual bool CanParseRMG => false;
		public virtual bool CanParseVehicle => false;
		public virtual string GuidKey => "";//throw new NotImplementedException(); // Don't throw here, it breaks stuff wtf.
		public virtual bool FullByteEncoding => false;
		public MOHClientHandler(ProtocolVersion protocol, ClientVersion version) : base(protocol)
		{
			this.Version = version;
		}
		public void RequestAuthorization(string CDKey, Action<NetAddress, string> authorize) { }
		public virtual void AdjustServerCommandOperations(ref ServerCommandOperations cmd)
		{
			// MOH doesn't have setgame or mapchange command
			// and it has 3 additional commands (centerprint,locprint,cgamemessage)
			if (cmd == (ServerCommandOperations)11)
			{
				cmd = ServerCommandOperations.EOF;
			}else if (cmd >= ServerCommandOperations.SetGame)
			{
				cmd+=3;
			}
		}
		public virtual void AdjustGameStateConfigstring(int i, string csStr)
		{
			// Nothing to do? Maybe someday if we support TA/TT check for protocol?
			/*if (i == GameState.ServerInfo)
			{
				var info = new InfoString(csStr);
				if (info["version"].Contains("v1.03"))
				{
					this.Version = ClientVersion.JO_v1_03;
				}
			}*/
		}
		public virtual ClientGame CreateClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum)
		{
			return new MOHClientGame(client, serverMessageNum, serverCommandSequence, clientNum);
		}
		public virtual bool CanParseSnapshot()
		{
			return true;
		}
		public virtual IList<NetField> GetEntityStateFields()
		{
			switch (this.Protocol)
			{
				default:
					throw new JKClientException("Protocol not supported");
				case ProtocolVersion.Protocol6:
				case ProtocolVersion.Protocol7:
				case ProtocolVersion.Protocol8:
					return MOHClientHandler.entityStateFields6_7_8;
				case ProtocolVersion.Protocol15:
				case ProtocolVersion.Protocol16:
				case ProtocolVersion.Protocol17:
					return MOHClientHandler.entityStateFields15_16_17;
			}
		}
		public virtual IList<NetField> GetPlayerStateFields(bool isVehicle, Func<bool> isPilot)
		{
			switch (this.Protocol)
			{
				default:
					throw new JKClientException("Protocol not supported");
				case ProtocolVersion.Protocol6:
				case ProtocolVersion.Protocol7:
				case ProtocolVersion.Protocol8:
					return MOHClientHandler.playerStateFields6_7_8;
				case ProtocolVersion.Protocol15:
				case ProtocolVersion.Protocol16:
				case ProtocolVersion.Protocol17:
					return MOHClientHandler.playerStateFields15_16_17;
			}
		}
		public virtual void ClearState() { }

		public virtual void SetExtraConfigstringInfo(in ServerInfo serverInfo, in InfoString info)
		{
			/*switch (serverInfo.Protocol)
			{
				case ProtocolVersion.Protocol15 when info["version"].Contains("v1.03"):
					serverInfo.Version = ClientVersion.JO_v1_03;
					break;
				case ProtocolVersion.Protocol15:
					serverInfo.Version = ClientVersion.JO_v1_02;
					break;
				case ProtocolVersion.Protocol16:
					serverInfo.Version = ClientVersion.JO_v1_04;
					break;
			}*/
			if (info.Count <= 0)
			{
				return;
			}
			GameTypeMOH gameTypeMOH = (GameTypeMOH)info["g_gametype"].Atoi();
            switch (gameTypeMOH)
            {
				case GameTypeMOH.GT_SINGLE_PLAYER:
					serverInfo.GameType = GameType.SinglePlayer;
					break;
				default:
				case GameTypeMOH.GT_MAX_GAME_TYPE:
				case GameTypeMOH.GT_FFA:
					serverInfo.GameType = GameType.FFA;
					break;
				case GameTypeMOH.GT_TEAM:
					serverInfo.GameType = GameType.Team;
					break;
				case GameTypeMOH.GT_TEAM_ROUNDS:
					serverInfo.GameType = GameType.TeamRounds;
					break;
				case GameTypeMOH.GT_OBJECTIVE:
					serverInfo.GameType = GameType.Objective;
					break;
				case GameTypeMOH.GT_TOW:
					serverInfo.GameType = GameType.TOW;
					break;
				case GameTypeMOH.GT_LIBERATION:
					serverInfo.GameType = GameType.Liberation;
					break;
            }

			serverInfo.GameName = info["g_gametypestring"]; // Ofc MOH is special and can't just use normal gamename :)
			serverInfo.MOHScoreboardPICover = info["g_scoreboardpicover"]; // Image shown when team wins. textures/hud/allieswin  or textures/hud/axiswin
			/*
			serverInfo.NeedPassword = info["g_needpass"].Atoi() != 0;
			serverInfo.TrueJedi = info["g_jediVmerc"].Atoi() != 0;
			if (serverInfo.GameType == GameType.Duel)
			{
				serverInfo.WeaponDisable = info["g_duelWeaponDisable"].Atoi() != 0;
			}
			else
			{
				serverInfo.WeaponDisable = info["g_weaponDisable"].Atoi() != 0;
			}
			serverInfo.ForceDisable = info["g_forcePowerDisable"].Atoi() != 0;*/
		}
		private static readonly NetFieldsArray entityStateFields6_7_8 = new NetFieldsArray(typeof(EntityState)) {
			 // Not detected
			{ NetFieldType.coord  , nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32(), 0 },
			{ NetFieldType.coord  , nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32()+ sizeof(float)*1, 0 }, 
			{ NetFieldType.angle  , nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32()+ sizeof(float)*1, 12 }, 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 0 },
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*1 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 0 }, 
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles), -13 },
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*3 * 3, -13 }, 
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*1 * 3, -13 }, 
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*2 * 3, -13 }, 
			{ NetFieldType.coord  , nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32()+ sizeof(float)*2, 0 }, 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 0 },
			{ NetFieldType.animWeight , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*1 +Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 0 }, 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*2 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 0 }, 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*3 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 0 }, 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 },
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*1 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, 
			{ NetFieldType.animWeight  , nameof(EntityState.ActionWeight), 0 },
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*2 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 0 }, 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*3 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 0 }, 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*2 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*3 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, 
			{ NetFieldType.regular  , nameof(EntityState.EntityType), 8 },
			{ NetFieldType.regular  , nameof(EntityState.ModelIndex), 16 },
			{ NetFieldType.regular  , nameof(EntityState.Parent), 16 },
			{ NetFieldType.regular  , nameof(EntityState.ConstantLight), 32 },
			{ NetFieldType.regular  , nameof(EntityState.RenderFx), 32 },
			{ NetFieldType.regular  , nameof(EntityState.BoneTag), -8 },
			{ NetFieldType.regular  , nameof(EntityState.BoneTag),  sizeof(int)*1, -8 }, 
			{ NetFieldType.regular  , nameof(EntityState.BoneTag),  sizeof(int)*2, -8 }, 
			{ NetFieldType.regular  , nameof(EntityState.BoneTag),  sizeof(int)*3, -8 }, 
			{ NetFieldType.regular  , nameof(EntityState.BoneTag),  sizeof(int)*4, -8 }, 
			{ NetFieldType.scale  , nameof(EntityState.Scale), 0 },
			{ NetFieldType.alpha  , nameof(EntityState.Alpha), 0 },
			{ NetFieldType.regular  , nameof(EntityState.UsageIndex), 16 },
			{ NetFieldType.regular  , nameof(EntityState.EntityFlags), 16 },
			{ NetFieldType.regular  , nameof(EntityState.Solid), 32 },
			{ NetFieldType.angle  , nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32()+ sizeof(float)*2, 12 }, 
			{ NetFieldType.angle  , nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32(), 12 },
			{ NetFieldType.regular  , nameof(EntityState.TagNum), 10 },
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*(1 * 3+2), -13 }, 
			{ NetFieldType.regular  , nameof(EntityState.AttachUseAngles), 1 },
			{ NetFieldType.coord  , nameof(EntityState.Origin2),  sizeof(float)*1, 0 }, 
			{ NetFieldType.coord  , nameof(EntityState.Origin2), 0 },
			{ NetFieldType.coord  , nameof(EntityState.Origin2),  sizeof(float)*2, 0 }, 
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*2, -13 }, 
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*(2 * 3+2), -13 }, 
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*(3 * 3+2), -13 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces), 8 },
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*1, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*2, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*3, 8 }, 
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*1, -13 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*4, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*5, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Time)).ToInt32(), 32 }, //  Replace PosType with real type and double check. 
			{ NetFieldType.velocity  , nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32(), 0 }, //  Replace PosType with real type and double check. 
			{ NetFieldType.velocity  , nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() + sizeof(float)*1, 0 }, //  Replace PosType with real type and double check. Also replace sizeof type near end. 
			{ NetFieldType.velocity  , nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() + sizeof(float)*2, 0 }, //  Replace PosType with real type and double check. Also replace sizeof type near end. 
			{ NetFieldType.regular  , nameof(EntityState.LoopSound), 16 },
			{ NetFieldType.regular  , nameof(EntityState.LoopSoundVolume), 0 },
			{ NetFieldType.regular  , nameof(EntityState.LoopSoundMinDist), 0 },
			{ NetFieldType.regular  , nameof(EntityState.LoopSoundMaxDist), 0 },
			{ NetFieldType.regular  , nameof(EntityState.LoopSoundPitch), 0 },
			{ NetFieldType.regular  , nameof(EntityState.LoopSoundFlags), 8 },
			{ NetFieldType.regular  , nameof(EntityState.AttachOffset), 0 },
			{ NetFieldType.regular  , nameof(EntityState.AttachOffset),  sizeof(float)*1, 0 }, 
			{ NetFieldType.regular  , nameof(EntityState.AttachOffset),  sizeof(float)*2, 0 }, 
			{ NetFieldType.regular  , nameof(EntityState.BeamEntnum), 16 },
			{ NetFieldType.regular  , nameof(EntityState.SkinNum), 16 },
			{ NetFieldType.regular  , nameof(EntityState.WasFrame), 10 },
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*4 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*5 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*6 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*7 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*8 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*9 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*10 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*11 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*12 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*13 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*14 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*15 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*4 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 0 }, 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*5 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 0 }, 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*6 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 0 }, 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*7 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 0 }, 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*8 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 0 }, 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*9 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 0 }, 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*10 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 0 }, 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*11 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 0 }, 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*12 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 0 }, 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*13 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 0 }, 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*14 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 0 }, 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*15 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 0 }, 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*4 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 0 }, 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*5 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 0 }, 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*6 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 0 }, 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*7 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 0 }, 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*8 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 0 }, 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*9 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 0 }, 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*10 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 0 }, 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*11 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 0 }, 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*12 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 0 }, 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*13 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 0 }, 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*14 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 0 }, 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*15 + Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 0 }, 
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*(1 * 3+1), -13 }, 
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*(2 * 3+1), -13 }, 
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*(3 * 3+1), -13 }, 
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*4 * 3, -13 }, 
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*(4 * 3+1), -13 }, 
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*(4 * 3+2), -13 }, 
			{ NetFieldType.regular  , nameof(EntityState.ClientNum), 8 },
			{ NetFieldType.regular  , nameof(EntityState.GroundEntityNum), Common.GEntitynumBits },
			{ NetFieldType.regular  , nameof(EntityState.ShaderData), 0 },
			{ NetFieldType.regular  , nameof(EntityState.ShaderData),  sizeof(float)*1, 0 }, 
			{ NetFieldType.regular  , nameof(EntityState.ShaderTime), 0 },
			{ NetFieldType.regular  , nameof(EntityState.EyeVector), 0 },
			{ NetFieldType.regular  , nameof(EntityState.EyeVector),  sizeof(float)*1, 0 }, 
			{ NetFieldType.regular  , nameof(EntityState.EyeVector),  sizeof(float)*2, 0 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*6, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*7, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*8, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*9, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*10, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*11, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*12, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*13, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*14, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*15, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*16, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*17, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*18, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*19, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*20, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*21, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*22, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*23, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*24, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*25, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*26, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*27, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*28, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*29, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*30, 8 }, 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*31, 8 }, 

		};
		private static unsafe readonly NetFieldsArray playerStateFields6_7_8 = new NetFieldsArray(typeof(PlayerState)) {
				{ NetFieldType.regular  , nameof(PlayerState.CommandTime), 32 },
				{ NetFieldType.coord  , nameof(PlayerState.Origin), 0 },
				{ NetFieldType.coord  , nameof(PlayerState.Origin),  sizeof(float)*1, 0 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.ViewAngles),  sizeof(float)*1, 0 }, // Replace sizeof type. 
				{ NetFieldType.velocity  , nameof(PlayerState.Velocity),  sizeof(float)*1, 0 }, // Replace sizeof type. 
				{ NetFieldType.velocity  , nameof(PlayerState.Velocity), 0 },
				{ NetFieldType.regular  , nameof(PlayerState.ViewAngles), 0 },
				{ NetFieldType.regular  , nameof(PlayerState.PlayerMoveTime), -16 },
			//	{ NetFieldType.regular  , nameof(PlayerState.WeaponTime), -16 },
				{ NetFieldType.coord  , nameof(PlayerState.Origin),  sizeof(float)*2, 0 }, // Replace sizeof type. 
				{ NetFieldType.velocity  , nameof(PlayerState.Velocity),  sizeof(float)*2, 0 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.IViewModelAnimChanged), 2 },
				{ NetFieldType.angle  , nameof(PlayerState.DamageAngles), -13 },
				{ NetFieldType.angle  , nameof(PlayerState.DamageAngles),  sizeof(float)*1, -13 }, // Replace sizeof type. 
				{ NetFieldType.angle  , nameof(PlayerState.DamageAngles),  sizeof(float)*2, -13 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.Speed), 16 },
				{ NetFieldType.regular  , nameof(PlayerState.DeltaAngles),  sizeof(float)*1, 16 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.ViewHeight), -8 },
				{ NetFieldType.regular  , nameof(PlayerState.GroundEntityNum), Common.GEntitynumBits },
				{ NetFieldType.regular  , nameof(PlayerState.DeltaAngles), 16 },
				{ NetFieldType.regular  , nameof(PlayerState.INetViewModelAnim), 4 },
				{ NetFieldType.regular  , nameof(PlayerState.FovMOHAA), 0 },
				{ NetFieldType.regular  , nameof(PlayerState.CurrentMusicMood), 8 },
				{ NetFieldType.regular  , nameof(PlayerState.Gravity), 16 },
				{ NetFieldType.regular  , nameof(PlayerState.FallbackMusicMood), 8 },
				{ NetFieldType.regular  , nameof(PlayerState.MusicVolume), 0 },
				{ NetFieldType.regular  , nameof(PlayerState.PlayerMoveFlags), 16 },
				{ NetFieldType.regular  , nameof(PlayerState.ClientNum), 8 },
				{ NetFieldType.regular  , nameof(PlayerState.FLeanAngle), 0 },
				{ NetFieldType.regular  , nameof(PlayerState.Blend),  sizeof(float)*3, 0 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.Blend), 0 },
				{ NetFieldType.regular  , nameof(PlayerState.PlayerMoveType), 8 },
				{ NetFieldType.regular  , nameof(PlayerState.FeetFalling), 8 },
				{ NetFieldType.angle  , nameof(PlayerState.CameraAngles), 16 },
				{ NetFieldType.angle  , nameof(PlayerState.CameraAngles),  sizeof(float)*1, 16 }, // Replace sizeof type. 
				{ NetFieldType.angle  , nameof(PlayerState.CameraAngles),  sizeof(float)*2, 16 }, // Replace sizeof type. 
				{ NetFieldType.coord  , nameof(PlayerState.CameraOrigin), 0 },
				{ NetFieldType.coord  , nameof(PlayerState.CameraOrigin),  sizeof(float)*1, 0 }, // Replace sizeof type. 
				{ NetFieldType.coord  , nameof(PlayerState.CameraOrigin),  sizeof(float)*2, 0 }, // Replace sizeof type. 
				{ NetFieldType.coord  , nameof(PlayerState.CameraPositionOffsets), 0 },
				{ NetFieldType.coord  , nameof(PlayerState.CameraPositionOffsets),  sizeof(float)*2, 0 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.CameraTime), 0 },
				{ NetFieldType.regular  , nameof(PlayerState.BobCycle), 8 },
				{ NetFieldType.regular  , nameof(PlayerState.DeltaAngles),  sizeof(int)*2, 16 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.ViewAngles),  sizeof(float)*2, 0 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.MusicVolumeFadeTime), 0 },
				{ NetFieldType.regular  , nameof(PlayerState.ReverbType), 6 },
				{ NetFieldType.regular  , nameof(PlayerState.ReverbLevel), 0 },
				{ NetFieldType.regular  , nameof(PlayerState.Blend),  sizeof(float)*1, 0 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.Blend),  sizeof(float)*2, 0 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.CameraOffset), 0 },
				{ NetFieldType.regular  , nameof(PlayerState.CameraOffset),  sizeof(float)*1, 0 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.CameraOffset),  sizeof(float)*2, 0 }, // Replace sizeof type. 
				{ NetFieldType.coord  , nameof(PlayerState.CameraPositionOffsets),  sizeof(float)*1, 0 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.CameraFlags), 16 },

		};

		private static readonly NetFieldsArray entityStateFields15_16_17 = new NetFieldsArray(typeof(EntityState)) {
			{ NetFieldType.coord  , nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32(), 0 }, //  Replace Trajectory with real type and double check. 
			{ NetFieldType.coord  , nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() + sizeof(float)*1, 0 }, //  Replace Trajectory with real type and double check. Also replace sizeof type near end. 
			{ NetFieldType.angle  , nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() + sizeof(float)*1, 12 }, //  Replace Trajectory with real type and double check. Also replace sizeof type near end. 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 15 },
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*1 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 15 }, // Replace sizeof type. 
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles), -13 },
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*3 * 3, -13 }, // Replace sizeof type. Replace dim1size (size of [][] second array depth)
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*1 * 3, -13 }, // Replace sizeof type. Replace dim1size (size of [][] second array depth)
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*2 * 3, -13 }, // Replace sizeof type. Replace dim1size (size of [][] second array depth)
			{ NetFieldType.coord  , nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() + sizeof(float)*2, 0 }, //  Replace Trajectory with real type and double check. Also replace sizeof type near end. 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 8 },
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*1 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 8 }, // Replace sizeof type. 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*2 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 15 }, // Replace sizeof type. 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*3 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 15 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 },
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*1 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, // Replace sizeof type. 
			{ NetFieldType.animWeight  , nameof(EntityState.ActionWeight), 8 },
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*2 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 8 }, // Replace sizeof type. 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*3 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*2 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*3 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.EntityType), 8 },
			{ NetFieldType.regular  , nameof(EntityState.ModelIndex), 16 },
			{ NetFieldType.regular  , nameof(EntityState.Parent), 16 },
			{ NetFieldType.regular  , nameof(EntityState.ConstantLight), 32 },
			{ NetFieldType.regular  , nameof(EntityState.RenderFx), 32 },
			{ NetFieldType.regular  , nameof(EntityState.BoneTag), -8 },
			{ NetFieldType.regular  , nameof(EntityState.BoneTag),  sizeof(int)*1, -8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.BoneTag),  sizeof(int)*2, -8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.BoneTag),  sizeof(int)*3, -8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.BoneTag),  sizeof(int)*4, -8 }, // Replace sizeof type. 
			{ NetFieldType.scale  , nameof(EntityState.Scale), 10 },
			{ NetFieldType.alpha  , nameof(EntityState.Alpha), 8 },
			{ NetFieldType.regular  , nameof(EntityState.UsageIndex), 16 },
			{ NetFieldType.regular  , nameof(EntityState.EntityFlags), 16 },
			{ NetFieldType.regular  , nameof(EntityState.Solid), 32 },
			{ NetFieldType.angle  , nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() + sizeof(float)*2, 12 }, //  Replace Trajectory with real type and double check. Also replace sizeof type near end. 
			{ NetFieldType.angle  , nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32(), 12 }, //  Replace Trajectory with real type and double check. 
			{ NetFieldType.regular  , nameof(EntityState.TagNum), 10 },
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*(1 * 3+2), -13 }, // Replace sizeof type. Replace dim1size (size of [][] second array depth)
			{ NetFieldType.regular  , nameof(EntityState.AttachUseAngles), 1 },
			{ NetFieldType.coord  , nameof(EntityState.Origin2),  sizeof(float)*1, 0 }, // Replace sizeof type. 
			{ NetFieldType.coord  , nameof(EntityState.Origin2), 0 },
			{ NetFieldType.coord  , nameof(EntityState.Origin2),  sizeof(float)*2, 0 }, // Replace sizeof type. 
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*2, -13 }, // Replace sizeof type. 
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*(2 * 3+2), -13 }, // Replace sizeof type. Replace dim1size (size of [][] second array depth)
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*(3 * 3+2), -13 }, // Replace sizeof type. Replace dim1size (size of [][] second array depth)
			{ NetFieldType.regular  , nameof(EntityState.Surfaces), 8 },
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*1, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*2, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*3, 8 }, // Replace sizeof type. 
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*1, -13 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*4, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*5, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Time)).ToInt32(), 32 }, //  Replace Trajectory with real type and double check. 
			{ NetFieldType.velocity  , nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32(), 0 }, //  Replace Trajectory with real type and double check. 
			{ NetFieldType.velocity  , nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() + sizeof(float)*1, 0 }, //  Replace Trajectory with real type and double check. Also replace sizeof type near end. 
			{ NetFieldType.velocity  , nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() + sizeof(float)*2, 0 }, //  Replace Trajectory with real type and double check. Also replace sizeof type near end. 
			{ NetFieldType.regular  , nameof(EntityState.LoopSound), 16 },
			{ NetFieldType.regular  , nameof(EntityState.LoopSoundVolume), 0 },
			{ NetFieldType.regular  , nameof(EntityState.LoopSoundMinDist), 0 },
			{ NetFieldType.regular  , nameof(EntityState.LoopSoundMaxDist), 0 },
			{ NetFieldType.regular  , nameof(EntityState.LoopSoundPitch), 0 },
			{ NetFieldType.regular  , nameof(EntityState.LoopSoundFlags), 8 },
			{ NetFieldType.regular  , nameof(EntityState.AttachOffset), 0 },
			{ NetFieldType.regular  , nameof(EntityState.AttachOffset),  sizeof(float)*1, 0 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.AttachOffset),  sizeof(float)*2, 0 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.BeamEntnum), 16 },
			{ NetFieldType.regular  , nameof(EntityState.SkinNum), 16 },
			{ NetFieldType.regular  , nameof(EntityState.WasFrame), 10 },
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*4 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*5 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*6 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*7 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*8 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*9 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*10 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*11 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*12 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*13 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*14 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*15 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Index)).ToInt32(), 12 }, // Replace sizeof type. 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*4 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 15 }, // Replace sizeof type. 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*5 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 15 }, // Replace sizeof type. 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*6 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 15 }, // Replace sizeof type. 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*7 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 15 }, // Replace sizeof type. 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*8 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 15 }, // Replace sizeof type. 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*9 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 15 }, // Replace sizeof type. 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*10 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 15 }, // Replace sizeof type. 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*11 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 15 }, // Replace sizeof type. 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*12 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 15 }, // Replace sizeof type. 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*13 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 15 }, // Replace sizeof type. 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*14 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 15 }, // Replace sizeof type. 
			{ NetFieldType.animTime  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*15 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Time)).ToInt32(), 15 }, // Replace sizeof type. 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*4 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 8 }, // Replace sizeof type. 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*5 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 8 }, // Replace sizeof type. 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*6 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 8 }, // Replace sizeof type. 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*7 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 8 }, // Replace sizeof type. 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*8 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 8 }, // Replace sizeof type. 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*9 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 8 }, // Replace sizeof type. 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*10 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 8 }, // Replace sizeof type. 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*11 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 8 }, // Replace sizeof type. 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*12 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 8 }, // Replace sizeof type. 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*13 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 8 }, // Replace sizeof type. 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*14 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 8 }, // Replace sizeof type. 
			{ NetFieldType.animWeight  , nameof(EntityState.frameInfo),  FrameInfo.ASSUMEDSIZE*15 +  Marshal.OffsetOf(typeof(FrameInfo),nameof(FrameInfo.Weight)).ToInt32(), 8 }, // Replace sizeof type. 
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*(1 * 3+1), -13 }, // Replace sizeof type. Replace dim1size (size of [][] second array depth)
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*(2 * 3+1), -13 }, // Replace sizeof type. Replace dim1size (size of [][] second array depth)
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*(3 * 3+1), -13 }, // Replace sizeof type. Replace dim1size (size of [][] second array depth)
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*4 * 3, -13 }, // Replace sizeof type. Replace dim1size (size of [][] second array depth)
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*(4 * 3+1), -13 }, // Replace sizeof type. Replace dim1size (size of [][] second array depth)
			{ NetFieldType.angle  , nameof(EntityState.BoneAngles),  sizeof(float)*(4 * 3+2), -13 }, // Replace sizeof type. Replace dim1size (size of [][] second array depth)
			{ NetFieldType.regular  , nameof(EntityState.ClientNum), 8 },
			{ NetFieldType.regular  , nameof(EntityState.GroundEntityNum), Common.GEntitynumBits },
			{ NetFieldType.regular  , nameof(EntityState.ShaderData), 0 },
			{ NetFieldType.regular  , nameof(EntityState.ShaderData),  sizeof(float)*1, 0 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.ShaderTime), 0 },
			{ NetFieldType.regular  , nameof(EntityState.EyeVector), 0 },
			{ NetFieldType.regular  , nameof(EntityState.EyeVector),  sizeof(float)*1, 0 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.EyeVector),  sizeof(float)*2, 0 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*6, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*7, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*8, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*9, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*10, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*11, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*12, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*13, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*14, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*15, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*16, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*17, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*18, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*19, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*20, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*21, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*22, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*23, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*24, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*25, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*26, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*27, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*28, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*29, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*30, 8 }, // Replace sizeof type. 
			{ NetFieldType.regular  , nameof(EntityState.Surfaces),  sizeof(byte)*31, 8 }, // Replace sizeof type.  

		};
		private static unsafe readonly NetFieldsArray playerStateFields15_16_17 = new NetFieldsArray(typeof(PlayerState)) {
				{ NetFieldType.regular  , nameof(PlayerState.CommandTime), 32 },
				{ NetFieldType.coordExtra  , nameof(PlayerState.Origin), 0 },
				{ NetFieldType.coordExtra  , nameof(PlayerState.Origin),  sizeof(float)*1, 0 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.ViewAngles),  sizeof(float)*1, 0 }, // Replace sizeof type. 
				{ NetFieldType.velocity  , nameof(PlayerState.Velocity),  sizeof(float)*1, 0 }, // Replace sizeof type. 
				{ NetFieldType.velocity  , nameof(PlayerState.Velocity), 0 },
				{ NetFieldType.regular  , nameof(PlayerState.ViewAngles), 0 },
				{ NetFieldType.coordExtra  , nameof(PlayerState.Origin),  sizeof(float)*2, 0 }, // Replace sizeof type. 
				{ NetFieldType.velocity  , nameof(PlayerState.Velocity),  sizeof(float)*2, 0 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.IViewModelAnimChanged), 2 },
				{ NetFieldType.angle  , nameof(PlayerState.DamageAngles), -13 },
				{ NetFieldType.angle  , nameof(PlayerState.DamageAngles),  sizeof(float)*1, -13 }, // Replace sizeof type. 
				{ NetFieldType.angle  , nameof(PlayerState.DamageAngles),  sizeof(float)*2, -13 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.Speed), 16 },
				{ NetFieldType.regular  , nameof(PlayerState.DeltaAngles),  sizeof(int)*1, 16 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.ViewHeight), -8 },
				{ NetFieldType.regular  , nameof(PlayerState.GroundEntityNum), Common.GEntitynumBits },
				{ NetFieldType.regular  , nameof(PlayerState.DeltaAngles), 16 },
				{ NetFieldType.regular  , nameof(PlayerState.INetViewModelAnim), 4 },
				{ NetFieldType.regular  , nameof(PlayerState.FovMOHAA), 0 },
				{ NetFieldType.regular  , nameof(PlayerState.CurrentMusicMood), 8 },
				{ NetFieldType.regular  , nameof(PlayerState.Gravity), 16 },
				{ NetFieldType.regular  , nameof(PlayerState.FallbackMusicMood), 8 },
				{ NetFieldType.regular  , nameof(PlayerState.MusicVolume), 0 },
				{ NetFieldType.regular  , nameof(PlayerState.PlayerMoveFlags), 16 },
				{ NetFieldType.regular  , nameof(PlayerState.ClientNum), 8 },
				{ NetFieldType.regular  , nameof(PlayerState.FLeanAngle), 0 },
				{ NetFieldType.regular  , nameof(PlayerState.Blend),  sizeof(float)*3, 0 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.Blend), 0 },
				{ NetFieldType.regular  , nameof(PlayerState.PlayerMoveType), 8 },
				{ NetFieldType.regular  , nameof(PlayerState.FeetFalling), 8 },
				{ NetFieldType.regular  , nameof(PlayerState.RadarInfo), 26 },
				{ NetFieldType.angle  , nameof(PlayerState.CameraAngles), 16 },
				{ NetFieldType.angle  , nameof(PlayerState.CameraAngles),  sizeof(float)*1, 16 }, // Replace sizeof type. 
				{ NetFieldType.angle  , nameof(PlayerState.CameraAngles),  sizeof(float)*2, 16 }, // Replace sizeof type. 
				{ NetFieldType.coordExtra  , nameof(PlayerState.CameraOrigin), 0 },
				{ NetFieldType.coordExtra  , nameof(PlayerState.CameraOrigin),  sizeof(float)*1, 0 }, // Replace sizeof type. 
				{ NetFieldType.coordExtra  , nameof(PlayerState.CameraOrigin),  sizeof(float)*2, 0 }, // Replace sizeof type. 
				{ NetFieldType.coordExtra  , nameof(PlayerState.CameraPositionOffsets), 0 },
				{ NetFieldType.coordExtra  , nameof(PlayerState.CameraPositionOffsets),  sizeof(float)*2, 0 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.CameraTime), 0 },
				{ NetFieldType.regular  , nameof(PlayerState.Voted), 1 },
				{ NetFieldType.regular  , nameof(PlayerState.BobCycle), 8 },
				{ NetFieldType.regular  , nameof(PlayerState.DeltaAngles),  sizeof(int)*2, 16 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.ViewAngles),  sizeof(float)*2, 0 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.MusicVolumeFadeTime), 0 },
				{ NetFieldType.regular  , nameof(PlayerState.ReverbType), 6 },
				{ NetFieldType.regular  , nameof(PlayerState.ReverbLevel), 0 },
				{ NetFieldType.regular  , nameof(PlayerState.Blend),  sizeof(float)*1, 0 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.Blend),  sizeof(float)*2, 0 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.CameraOffset), 0 },
				{ NetFieldType.regular  , nameof(PlayerState.CameraOffset),  sizeof(float)*1, 0 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.CameraOffset),  sizeof(float)*2, 0 }, // Replace sizeof type. 
				{ NetFieldType.coordExtra  , nameof(PlayerState.CameraPositionOffsets),  sizeof(float)*1, 0 }, // Replace sizeof type. 
				{ NetFieldType.regular  , nameof(PlayerState.CameraFlags), 16 },

		};
	}
}
