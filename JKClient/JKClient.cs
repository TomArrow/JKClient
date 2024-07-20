using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Security.Cryptography;
using System.Web;

namespace System.Runtime.CompilerServices
{
	[EditorBrowsable(EditorBrowsableState.Never)]
	class IsExternalInit { }
}

namespace JKClient {

	public class DemoName_t {
		public string name;
		public DateTime time; // For name shenanigans
		public override string ToString()
        {
			return name;
        }
    }

	enum jaPlusClientSupportFlags_t
	{
		CSF_GRAPPLE_SWING = (int)0x00000001u, // Can correctly predict movement when using the grapple hook
		CSF_SCOREBOARD_LARGE = (int)0x00000002u, // Can correctly parse scoreboard messages with information for 32 clients
		CSF_SCOREBOARD_KD = (int)0x00000004u, // Can correctly parse scoreboard messages with extra K/D information
		CSF_CHAT_FILTERS = (int)0x00000008u, // Can correctly parse chat messages with proper delimiters
		CSF_FIXED_WEAPON_ANIMS = (int)0x00000010u, // Fixes the missing concussion rifle animations
		CSF_WEAPONDUEL = (int)0x00000020u,
	}

	public delegate void UserCommandGeneratedEventHandler(object sender, ref UserCommand modifiableCommand, in UserCommand previousCommand, ref List<UserCommand> insertCommands);
	public sealed partial class JKClient : NetClient {

		private static readonly string[] mohCvars = new string[]{"g_immediateswitch","r_debugSurfaceUpdate","ui_weaponsign","thereisnomonkey","ui_name","ui_disp_playergermanmodel","ui_dm_playergermanmodel","ui_disp_playermodel","ui_dm_playermodel","ui_timemessage","debugSound","viewmodelentity","g_aimLagTime","ter_count","ter_cautiousframes","ter_lock","ter_cull","cg_scoreboardpicover","cg_scoreboardpic","cg_obj_axistext3","cg_obj_axistext2","cg_obj_axistext1","cg_obj_alliedtext3","cg_obj_alliedtext2","cg_obj_alliedtext1","cg_maxclients","cg_timelimit","cg_fraglimit","cg_treadmark_test","cg_te_numCommands","cg_te_currCommand","cg_te_mode_name","cg_te_mode","cg_te_emittermodel","cg_te_zangles","cg_te_yangles","cg_te_xangles","cg_te_tag","cg_te_singlelinecommand","cg_te_command_time","cg_te_alignstretch_scale","cg_te_cone_height","cg_te_spawnrange_b","cg_te_spawnrange_a","cg_te_spritegridlighting","cg_te_varycolor","cg_te_friction","cg_te_radial_max","cg_te_radial_min","cg_te_radial_scale","cg_te_avelamp_r","cg_te_avelamp_y","cg_te_avelamp_p","cg_te_avelbase_r","cg_te_avelbase_y","cg_te_avelbase_p","cg_te_swarm_delta","cg_te_swarm_maxspeed","cg_te_swarm_freq","cg_te_axisoffsamp_z","cg_te_axisoffsamp_y","cg_te_axisoffsamp_x","cg_te_axisoffsbase_z","cg_te_axisoffsbase_y","cg_te_axisoffsbase_x","cg_te_randaxis","cg_te_volumetric","cg_te_forwardvel","cg_te_clampvelaxis","cg_te_clampvelmax_z","cg_te_clampvelmin_z","cg_te_clampvelmax_y","cg_te_clampvelmin_y","cg_te_clampvelmax_x","cg_te_clampvelmin_x","cg_te_randvelamp_z","cg_te_randvelamp_y","cg_te_randvelamp_x","cg_te_randvelbase_z","cg_te_randvelbase_y","cg_te_randvelbase_x","cg_te_anglesamp_r","cg_te_anglesamp_y","cg_te_anglesamp_p","cg_te_anglesbase_r","cg_te_anglesbase_y","cg_te_anglesbase_p","cg_te_offsamp_z","cg_te_offsamp_y","cg_te_offsamp_x","cg_te_offsbase_z","cg_te_offsbase_y","cg_te_offsbase_x","cg_te_randomroll","cg_te_collision","cg_te_flickeralpha","cg_te_align","cg_te_radius","cg_te_insphere","cg_te_sphere","cg_te_circle","cg_te_scalerate","cg_te_spawnrate","cg_te_fadein","cg_te_fadedelay","cg_te_fade","cg_te_count","cg_te_accel_z","cg_te_accel_y","cg_te_accel_x","cg_te_model","cg_te_scalemax","cg_te_scalemin","cg_te_scale","cg_te_bouncefactor","cg_te_dietouch","cg_te_life","cg_rain_drawcoverage","vss_lighting_fps","vss_default_b","vss_default_g","vss_default_r","vss_gridsize","vss_maxvisible","vss_movement_dampen","vss_wind_strength","vss_wind_z","vss_wind_y","vss_wind_x","vss_showsources","vss_color","vss_repulsion_fps","vss_physics_fps","vss_draw","cg_effect_physicsrate","cg_showtempmodels","cg_showemitters","cg_eventstats","cg_timeevents","cg_eventlimit","cg_showevents","cg_voicechat","vm_lean_lower","vm_offset_upvel","vm_offset_vel_up","vm_offset_vel_side","vm_offset_vel_front","vm_offset_vel_base","vm_offset_shotguncrouch_up","vm_offset_shotguncrouch_side","vm_offset_shotguncrouch_front","vm_offset_rocketcrouch_up","vm_offset_rocketcrouch_side","vm_offset_rocketcrouch_front","vm_offset_crouch_up","vm_offset_crouch_side","vm_offset_crouch_front","vm_offset_air_up","vm_offset_air_side","vm_offset_air_front","vm_sway_up","vm_sway_side","vm_sway_front","vm_offset_speed","vm_offset_max","cg_drawsvlag","cg_huddraw_force","cg_acidtrip","cg_hitmessages","cg_animationviewmodel","cg_shadowdebug","cg_shadowscount","cg_pmove_msec","cg_smoothClientsTime","cg_smoothClients","cg_debugfootsteps","cg_traceinfo","cg_camerascale","cg_cameraverticaldisplacement","cg_cameradist","cg_cameraheight","cg_3rd_person","cg_lagometer","cg_stereosep","g_synchronousClients","cg_hidetempmodels","cg_stats","cg_showmiss","cg_nopredict","cg_errordecay","cg_debuganimwatch","cg_debuganim","cg_animspeed","cg_marks_max","cgamedll","sv_referencedPakNames","sv_referencedPaks","g_showflypath","ai_pathcheckdist","ai_pathchecktime","ai_debugpath","ai_fallheight","ai_showpath","ai_showallnode","ai_shownode","ai_shownodenums","ai_showroutes_distance","ai_showroutes","cm_ter_usesphere","cm_FCMdebug","cm_FCMcacheall","cm_playerCurveClip","cm_noCurves","cm_noAreas","session","g_eventstats","g_watch","g_timeevents","g_eventlimit","g_showevents","g_showinfo","g_scoreboardpicover","g_scoreboardpic","g_obj_axistext3","g_obj_axistext2","g_obj_axistext1","g_obj_alliedtext3","g_obj_alliedtext2","g_obj_alliedtext1","g_spectate_allow_full_chat","g_spectatefollow_pitch","g_spectatefollow_up","g_spectatefollow_right","g_spectatefollow_forward","g_forceteamspectate","g_gotmedal","g_failed","g_success","g_playerdeltamethod","g_drawattackertime","g_viewkick_dmmult","g_viewkick_roll","g_viewkick_yaw","g_viewkick_pitch","s_debugmusic","pmove_msec","pmove_fixed","g_smoothClients","g_maxintermission","g_forcerespawn","g_forceready","g_doWarmup","g_warmup","g_allowvote","g_rankedserver","ai_debug_grenades","g_ai_soundscale","g_ai_noticescale","g_ai_notifyradius","g_showdamage","g_animdump","g_dropclips","g_droppeditemlife","g_patherror","g_spawnai","g_spawnentities","g_monitorNum","g_monitor","g_vehicle","g_ai","g_scripttrace","g_scriptdebug","g_nodecheck","g_scriptcheck","g_showopcodes","g_showtokens","g_logstats","g_debugdamage","g_debugtargets","g_showautoaim","g_statefile","g_playermodel","g_spiffyvelocity_z","g_spiffyvelocity_y","g_spiffyvelocity_x","g_spiffyplayer","g_numdebugstrings","g_numdebuglinedelays","g_showlookat","g_entinfo","g_showawareness","g_showbullettrace","g_showplayeranim","g_showplayerstate","g_showaxis","g_timeents","g_showmem","sv_crouchspeedmult","sv_dmspeedmult","sv_walkspeed","sv_runspeed","sv_cinematic","sv_waterspeed","sv_waterfriction","sv_stopspeed","sv_friction","sv_showentnums","sv_showcameras","sv_testloc_offset2_z","sv_testloc_offset2_y","sv_testloc_offset2_x","sv_testloc_radius2","sv_testloc_offset_z","sv_testloc_offset_y","sv_testloc_offset_x","sv_testloc_radius","sv_testloc_secondary","sv_testloc_num","sv_showbboxes","sv_drawtrace","sv_traceinfo","sv_gravity","sv_maxvelocity","sv_rollangle","sv_rollspeed","bosshealth","whereami","com_blood","flood_waitdelay","flood_persecond","flood_msgs","nomonsters","g_allowjointime","roundlimit","filterban","maxentities","sv_precache","gamedll","subAlpha","loadingbar","sv_location","sv_debuggamespy","g_inactivekick","g_inactivespectate","g_teamdamage","sv_gamespy","ui_dedicated","ui_multiplayersign","ui_briefingsign","g_lastsave","com_autodialdata","snd_maxdelay","snd_mindelay","snd_chance","snd_volume","snd_mindist","snd_reverblevel","snd_reverbtype","snd_yaw","snd_height","snd_length","snd_width","cg_te_alpha","cg_te_color_g","cg_te_color_r","cg_te_color_b","cg_te_filename","cam_angles_yaw","cam_angles_pitch","cam_angles_roll","viewmodelactionweight","viewmodelnormaltime","viewmodelanimnum2","viewmodelblend","viewmodelanimslot","viewmodelsyncrate","subteam3","subtitle3","subteam2","subtitle2","subteam1","subtitle1","subteam0","subtitle0","cg_hud","dlg_badsave","ui_startmap","cl_movieaudio","cl_greenfps","ui_returnmenu","ui_failed","ui_success","ui_gotmedal","ui_gmboxspam","ui_NumShotsFired","ui_NumHits","ui_NumComplete","ui_NumObjectives","ui_Accuracy","ui_PreferredWeapon","ui_NumHitsTaken","ui_NumObjectsDestroyed","ui_NumEnemysKilled","ui_HeadShots","ui_TorsoShots","ui_LeftLegShots","ui_RightLegShots","ui_LeftArmShots","ui_RightArmShots","ui_GroinShots","ui_GunneryEvaluation","ui_health_end","ui_health_start","ui_drawcoords","ui_inventoryfile","ui_newvidmode","ui_compass","ui_debugload","soundoverlay","ui_itemsbar","ui_weaponsbartime","ui_weaponsbar","ui_consoleposition","ui_gmbox","ui_minicon","s_obstruction_cal_time","s_show_sounds","s_show_num_active_sounds","s_show_cpu","s_initsound","s_dialogscale","s_testsound","s_show","s_mixPreStep","s_loadas8bit","s_separation","s_ambientvolume","net_port","net_ip","net_socksPassword","net_socksUsername","net_socksPort","net_socksServer","net_socksEnabled","net_noipx","net_noudp","graphshift","graphscale","graphheight","debuggraph","timegraph","ff_disabled","ff_developer","ff_ensureShake","ff_defaultTension","use_ff","dcl_texturescale","dcl_maxoffset","dcl_minsegment","dcl_maxsegment","dcl_pathmode","dcl_dostring","dcl_dobmodels","dcl_doterrain","dcl_doworld","dcl_dolighting","dcl_alpha","dcl_b","dcl_g","dcl_r","dcl_rotation","dcl_widthscale","dcl_heightscale","dcl_radius","dcl_shader","dcl_shiftstep","dcl_autogetinfo","dcl_showcurrent","dcl_editmode","r_gfxinfo","r_maskMinidriver","r_allowSoftwareGL","r_loadftx","r_loadjpg","ter_fastMarks","ter_minMarkRadius","r_precacheimages","r_static_shadermultiplier3","r_static_shadermultiplier2","r_static_shadermultiplier1","r_static_shadermultiplier0","r_static_shaderdata3","r_static_shaderdata2","r_static_shaderdata1","r_static_shaderdata0","r_sse","r_showportal","vss_smoothsmokelight","r_debuglines_depthmask","r_useglfog","r_lightcoronasize","r_farplane_nofog","r_farplane_nocull","r_farplane_color","r_farplane","r_skyportal_origin","r_skyportal","r_light_showgrid","r_light_nolight","r_light_int_scale","r_light_sun_line","r_light_lines","r_stipplelines","r_maxtermarks","r_maxpolyverts","r_maxpolys","r_entlight_maxcalc","r_entlight_cubefraction","r_entlight_cubelevel","r_entlight_errbound","r_entlight_scale","r_entlightmap","r_noportals","r_lockpvs","r_drawBuffer","r_offsetunits","r_offsetfactor","r_clear","r_showstaticbboxes","r_showhbox","r_shownormals","r_showsky","r_showtris","r_nobind","r_debugSurface","r_logFile","r_verbose","r_speeds","r_showcluster","r_novis","r_showcull","r_nocull","r_ignore","r_staticlod","r_drawspherelights","r_drawsprites","r_drawterrain","r_drawbrushmodels","r_drawbrushes","r_drawstaticmodelpoly","r_drawstaticmodels","r_drawentitypoly","r_drawentities","r_norefresh","r_measureOverdraw","r_skipBackEnd","r_showSmp","r_flareFade","r_flareSize","r_portalOnly","r_lightmap","r_drawworld","r_nocurves","r_printShaders","r_debugSort","lod_tool","lod_position","lod_save","lod_tris","lod_metric","lod_tikiname","lod_meshname","lod_mesh","lod_zee_val","lod_pitch_val","lod_curve_4_slider","lod_curve_3_slider","lod_curve_2_slider","lod_curve_1_slider","lod_curve_0_slider","lod_curve_4_val","lod_curve_3_val","lod_curve_2_val","lod_curve_1_val","lod_curve_0_val","lod_edit_4","lod_edit_3","lod_edit_2","lod_edit_1","lod_edit_0","lod_LOD_slider","lod_maxLOD","lod_minLOD","lod_LOD","r_showstaticlod","r_showlod","r_showImages","r_directedScale","r_ambientScale","r_primitives","r_facePlaneCull","r_swapInterval","r_finish","r_dlightBacks","r_fastsky","r_ignoreGLErrors","r_znear","r_lodCurveError","r_lerpmodels","r_singleShader","g_numdebuglines","r_intensity","r_mapOverBrightBits","r_fullbright","r_displayRefresh","r_ignoreFastPath","r_smp","r_vertexLight","r_customaspect","r_ignorehwgamma","r_overBrightBits","r_depthbits","r_stencilbits","r_stereo","r_textureDetails","r_colorMipLevels","r_roundImagesDown","r_reset_tc_array","r_geForce3WorkAround","r_ext_aniso_filter","r_ext_texture_env_combine","r_ext_texture_env_add","r_ext_compiled_vertex_array","r_ext_multitexture","r_ext_gamma_control","r_allowExtensions","r_glDriver","dm_playergermanmodel","password","m_invert_pitch","cg_forceModel","cl_maxPing","cg_autoswitch","cg_gametype","cl_langamerefreshstatus","cl_motdString","m_filter","m_side","m_up","m_forward","m_yaw","m_pitch","cl_allowDownload","cl_showmouserate","cl_mouseAccel","freelook","cl_run","cl_packetdup","cl_anglespeedkey","cl_pitchspeed","cl_yawspeed","rconAddress","cl_forceavidemo","cl_avidemo","activeAction","cl_freezeDemo","cl_showTimeDelta","cl_showSend","cl_shownet","cl_timeNudge","cl_connect_timeout","cl_timeout","cl_cdkey","cl_motd","cl_eventstats","cl_timeevents","cl_eventlimit","cl_showevents","cl_debugMove","cl_nodelta","sv_deeptracedebug","sv_drawentities","sv_mapChecksum","sv_killserver","sv_padPackets","sv_showloss","sv_reconnectlimit","sv_master5","sv_master4","sv_master3","sv_master2","sv_master1","sv_allowDownload","nextmap","sv_zombietime","sv_timeout","sv_fps","sv_privatePassword","rconPassword","sv_paks","sv_pure","sv_serverid","g_gametypestring","g_gametype","sv_maplist","sv_floodProtect","sv_maxPing","sv_minPing","sv_maxRate","sv_maxclients","sv_hostname","sv_privateClients","mapname","protocol","sv_keywords","timelimit","fraglimit","dmflags","skill","g_maxplayerhealth","net_multiLANpackets","net_qport","showdrop","showpackets","in_disablealttab","joy_threshold","in_debugjoystick","in_joyBallScale","in_joystick","in_mouse","in_mididevice","in_midichannel","in_midi","username","sys_cpuid","sys_cpustring","win_wndproc","win_hinstance","arch","arch_minor_version","arch_major_version","shortversion","version","com_buildScript","cl_running","sv_running","dedicated","timedemo","com_speeds","viewlog","com_dropsim","com_showtrace","fixedtime","timescale","fps","autopaused","paused","deathmatch","convertAnim","showLoad","low_anim_memory","dumploadedanims","pagememory","ui_legalscreen_stay","ui_legalscreen_fadeout","ui_legalscreen_fadein","ui_titlescreen_stay","ui_titlescreen_fadeout","ui_titlescreen_fadein","ui_skip_legalscreen","ui_skip_titlescreen","ui_skip_eamovie","cl_playintro","g_voiceChat","s_speaker_type","r_uselod","r_drawSun","r_flares","sensitivity","r_gamma","r_textureMode","dm_playermodel","snaps","rate","s_musicvolume","s_volume","vid_ypos","vid_xpos","r_customwidth","r_fullscreen","name","s_milesdriver","r_forceClampToEdge","r_lastValidRenderer","com_maxfps","r_customheight","s_reverb","cl_maxpackets","ui_console","config","r_ext_compressed_textures","r_drawstaticdecals","g_ddayshingleguys","g_ddayfog","g_ddayfodderguys","r_texturebits","r_colorbits","r_picmip","r_mode","cg_marks_add","s_khz","cg_shadows","cg_rain","ter_maxtris","ter_maxlod","ter_error","vss_maxcount","cg_effectdetail","r_lodviewmodelcap","r_lodcap","r_lodscale","r_subdivisions","r_fastentlight","r_fastdlights","cg_drawviewmodel","g_m6l3","g_m6l2","g_m6l1","g_m5l3","g_m5l2","g_m5l1","g_m4l3","g_m4l2","g_m4l1","g_m3l3","g_m3l2","g_m3l1","g_m2l3","g_m2l2","g_m2l1","g_m1l3","g_m1l2","g_m1l1","g_eogmedal2","g_eogmedal1","g_eogmedal0","g_medal5","g_medal4","g_medal3","g_medal2","g_medal1","g_medal0","ui_medalsign","ui_signshader","g_subtitle","g_skill","detail","ui_hostname","ui_maplist_obj","ui_maplist_round","ui_maplist_team","ui_maplist_ffa","ui_inactivekick","ui_inactivespectate","ui_connectip","ui_teamdamage","ui_timelimit","ui_fraglimit","ui_gamespy","ui_maxclients","ui_gametypestring","ui_gametype","ui_dmmap","ui_voodoo","cl_ctrlbindings","cl_altbindings","ui_crosshair","viewsize","journal","fs_filedir","mapdir","logfile","fs_restrict","fs_game","fs_basepath","fs_cdpath","fs_copyfiles","fs_debug","developer","cheats"};

		private const int messageIntervalsMeasureCount = 32;
		private long lastMessageReceivedTime = 0;
		private int messageIntervalMeasurementIndex = 0;
		private int[] messageIntervals = new int[messageIntervalsMeasureCount];
		private int messageIntervalAverage = 1000;

		public int PingAdjust = 0; // Adjust our visible ping (can lead to instabilities)

		public int TrafficReduceUntilClientFps = 10; // Need a minimum fps of 10 for client commands. Rest can be traffic-reduced by yeeting duplicate commands.

		public volatile int SnapOrderTolerance = 100;
		public volatile bool SnapOrderToleranceDemoSkipPackets = false;
		private const int LastPacketTimeOut = 5 * 60000;
		private const int RetransmitTimeOut = 3000;
		private const int MaxPacketUserCmds = 32;
		private const string DefaultName = "AssetslessClient";
		private const string DefaultNameMOH = "UnnamedSoldier";
		private readonly string jaPlusClientSupportFlagsExplanation = ((int)(jaPlusClientSupportFlags_t.CSF_SCOREBOARD_KD | jaPlusClientSupportFlags_t.CSF_SCOREBOARD_LARGE | jaPlusClientSupportFlags_t.CSF_GRAPPLE_SWING)).ToString("X"); // This is what jaPlusClientSupportFlags is, but I can't do that because ToString() can't be assigned to constant
		private const string jaPlusClientSupportFlags = "7";
		public const string forcePowersJKClientDefault = "7-1-032330000000001333";
		public const string forcePowersAllDark = "200-2-033330333000033333";
		public const string forcePowersAllLight = "200-1-333333000330003333";
		private long skipUserInfoChangeCount = 0;
		public string NWHEngine = null; 
		private const string UserInfo = "\\name\\" + JKClient.DefaultName + "\\rate\\200000\\snaps\\1000\\model\\kyle/default\\forcepowers\\"+ forcePowersAllDark + "\\color1\\4\\color2\\4\\handicap\\100\\teamtask\\0\\sex\\male\\password\\\\cg_predictItems\\1\\saber1\\single_1\\saber2\\none\\char_color_red\\255\\char_color_green\\255\\char_color_blue\\255\\engine\\jkclient_demoRec\\cjp_client\\1.4JAPRO\\csf\\"+ jaPlusClientSupportFlags + "\\assets\\0"; // cjp_client: Pretend to be jaPRO for more scoreboard stats
		private const string UserInfoMOH = "\\name\\" + DefaultNameMOH + "\\rate\\200000\\snaps\\1000\\dm_playermodel\\american_ranger\\dm_playergermanmodel\\german_wehrmacht_soldier"; // cjp_client: Pretend to be jaPRO for more scoreboard stats
		private readonly Random random = new Random();
		private readonly int port;
		private readonly InfoString userInfo = new InfoString(UserInfo);
		private readonly ConcurrentQueue<Action> actionsQueue = new ConcurrentQueue<Action>();
		private ClientGame clientGame;
		private TaskCompletionSource<bool> connectTCS;


		private Dictionary<string, string> extraDemoMeta = new Dictionary<string, string>();
		public Statistics Stats { get; init; } = new Statistics();
        public ClientEntity[] Entities => clientGame != null? clientGame.Entities : null;
        public int playerStateClientNum => snap.PlayerState.ClientNum;
        public PlayerState PlayerState => snap.PlayerState;
        public bool IsInterMission => snap.PlayerState.PlayerMoveType == PlayerMoveType.Intermission;
        public PlayerMoveType PlayerMoveType => snap.PlayerState.PlayerMoveType;
        public int SnapNum => snap.MessageNum;
        public int ServerTime => snap.ServerTime;
		public int gameTime => this.clientGame == null ? 0: this.clientGame.GetGameTime();
        //public PlayerState CurrentPlayerState => clientGame != null? clientGame. : null;
        #region ClientConnection
        public int clientNum { get; private set; } = -1;
		private int lastPacketSentTime = 0;
		private int lastPacketTime = 0;
		private NetAddress serverAddress;
		private int connectTime = -9999;
		private int mohConnectTimeExtraDelay = 200;
		private int infoRequestTime = -9999;
		private int connectPacketCount = 0;
		private int challenge = 0;
		private string getKeyChallenge = "";
		private int checksumFeed = 0;
		private float serverFrameTime = 0; // MOH
		private int reliableSequence = 0;
		private int reliableAcknowledge = 0;
		private sbyte [][]reliableCommands;
		private int serverMessageSequence = 0;
		private int maxSequenceNum = 0;
		private int serverCommandSequence = 0;
		private int lastExecutedServerCommand = 0;
		private int desiredSnaps = 1000;
		private bool clientForceSnaps = false;
		private sbyte [][]serverCommands;
		private int[] serverCommandMessagenums;
		private NetChannel netChannel;
		#endregion
		#region DemoWriting
		// demo information
		DemoName_t DemoName;
		public string AbsoluteDemoName { get; private set; } = null;
		//bool SpDemoRecording;
		public bool Demorecording { get; private set; }
		private SortedDictionary<int, BufferedDemoMessageContainer> bufferedDemoMessages = new SortedDictionary<int, BufferedDemoMessageContainer>();
		//bool Demoplaying;
		int Demowaiting;   // don't record until a non-delta message is received. Changed to int. 0=not waiting. 1=waiting for delta message with correct deltanum. 2= waiting for full snapshot
		const double DemoRecordBufferedReorderTimeout = 10;
		int DemoLastWrittenSequenceNumber = -1;

		public void SetExtraDemoMetaData(Dictionary<string,string> extraDemoMetaDataA)
        {
			if(extraDemoMetaDataA != null)
            {
                lock (extraDemoMeta)
                {
					foreach (var kvp in extraDemoMetaDataA)
					{
						extraDemoMeta[kvp.Key] = kvp.Value;
					}
				}
            }
        }
		public void ClearExtraDemoMetaData()
        {
            lock (extraDemoMeta)
            {
				extraDemoMeta.Clear();
			}
        }

		BufferedDemoMessageContainer DemoAfkSnapsDropLastDroppedMessage = null; // With afk snap skipping, we wanna always keep the last one and write it when a change is detected, so that playing the demo doesn't result in unnatural movement from longer interpolation times.
		int DemoAfkSnapsDropLastDroppedMessageNumber = -1;
		bool LastMessageWasDemoAFKDrop = false;
		
		bool DemoSkipPacket;
		bool FirstDemoFrameSkipped;
		TaskCompletionSource<bool> demoRecordingStartPromise = null;
		TaskCompletionSource<bool> demoFirstPacketRecordedPromise = null;
		Mutex DemofileLock = new Mutex();
		FileStream Demofile;
		Int64 DemoLastFullFlush = 0;
		DateTime DemoLastFullFlushTime = DateTime.Now;
		public Int64 DemoFlushInterval = 200 * 1000; // 200 KB. At the very least every 200 KB a write to disk is forced.
		public Int64 DemoFlushTimeInterval = 60000; // 60 seconds. At the very least every 60 sseconds a write to disk is forced.
#endregion
#region ClientStatic
		private int realTime = 0;
		private string servername;
		public ConnectionStatus Status { get; private set; }
		public IClientHandler ClientHandler => this.NetHandler as IClientHandler;

		public ClientVersion Version => this.ClientHandler.Version;




		public event EventHandler<InternalCommandCreatedEventArgs> InternalCommandCreated;
		protected bool OnInternalCommandCreated(string command, Encoding encoding = null)
		{
			InternalCommandCreatedEventArgs args = new InternalCommandCreatedEventArgs(command, encoding);
			InternalCommandCreated?.Invoke(this, args);
			return !args.handledExternally;
		}

		public event EventHandler MapChangeServerCommandReceived;
		internal void OnMapChangeServerCommandReceived()
        {
			this.MapChangeServerCommandReceived?.Invoke(this, new EventArgs());
		}

		public event EventHandler<SnapshotParsedEventArgs> SnapshotParsed;
		internal void OnSnapshotParsed(SnapshotParsedEventArgs eventArgs)
        {
			this.SnapshotParsed?.Invoke(this, eventArgs);
		}
		public event UserCommandGeneratedEventHandler UserCommandGenerated;
		internal void OnUserCommandGenerated(ref UserCommand cmd, in UserCommand previousCmd, ref List<UserCommand> insertCommands)
        {
			this.UserCommandGenerated?.Invoke(this, ref cmd, in previousCmd, ref insertCommands);
		}
		public event EventHandler<object> DebugEventHappened; // Has nothing to do with q3 engine events. It's just for some special cases to comfortably debug stuff.
		internal void OnDebugEventHappened(object o)
        {
			this.DebugEventHappened?.Invoke(this, o);
		}
		public bool DebugConfigStrings { get; set; } = false;
		public bool DebugNet { get; set; } = false;
		// set to true if you want this client to get stuck as "Connecting"
		public bool GhostPeer { get; init; } = false;
		private StringBuilder showNetString = null;

		public event EventHandler Disconnected;
		internal void OnDisconnected(EventArgs eventArgs)
        {
			this.Disconnected?.Invoke(this, eventArgs);
		}
		private int MaxReliableCommands => this.ClientHandler.MaxReliableCommands;
		private string GuidKey => this.ClientHandler.GuidKey;
#endregion
		// Use this if you wwant to do multiple userinfo related edits and avoid spamming userinfo updates to the server. Just tell it howw many upcoming changes to skip
		public void SkipUserInfoUpdatesAfterNextNChanges(int countSkips)
        {
			for (int i = 0; i < countSkips; i++){

				Interlocked.Increment(ref skipUserInfoChangeCount);
			}
        }
		public string Name {
			get => this.userInfo["name"];
			set {
				string name = value;
				if (string.IsNullOrEmpty(name)) {
					name = JKClient.DefaultName;
				}/* else if (name.Length > 31) { // Let me choose pls :)
					name = name.Substring(0, 31);
				}*/
				this.userInfo["name"] = name;
				this.UpdateUserInfo();
			}
		}
		public string Skin {
			get => this.userInfo["model"];
			set {
				string skin = value;
				if (string.IsNullOrEmpty(skin)) {
					skin = "kyle/default";
				}/* else if (name.Length > 31) { // Let me choose pls :)
					name = name.Substring(0, 31);
				}*/
				this.userInfo["model"] = skin;
				this.UpdateUserInfo();
			}
		}
		public string Password {
			get => this.userInfo["password"];
			set {
				this.userInfo["password"] = value;
				this.UpdateUserInfo();
			}
		}
		public int DesiredSnaps {
			get => this.desiredSnaps;
			set {
				if(this.desiredSnaps != value) { 
					this.desiredSnaps = value;
					this.userInfo["snaps"] = value.ToString();
					this.UpdateUserInfo();
				}
			}
		}
		// Force discard packets that don't respect our DesiredSnaps setting?
		// Many servers don't respect it these days.
		public bool ClientForceSnaps {
			get => this.clientForceSnaps;
			set {
				this.clientForceSnaps = value;
			}
		}

		public bool AfkDropSnaps { get; set; } = false;
		public int AfkDropSnapsMinFPS { get; set; } = 2;
		public int AfkDropSnapsMinFPSBots { get; set; } = 1000; // safe default but we really want a bit less..
		public Guid Guid {
			get => Guid.TryParse(this.userInfo[this.GuidKey], out Guid guid) ? guid : Guid.Empty;
			set {
				this.userInfo[this.GuidKey] = value.ToString();
				this.UpdateUserInfo();
			}
		}
		public string CDKey { get; set; } = string.Empty;
		public ClientInfo []ClientInfo => this.clientGame?.ClientInfo;
		internal bool[] ClientIsConfirmedBot = new bool[128]; // clientgame will set this for us, hehe. putting 128 as the limit in case we ever support a 128 player game
		internal bool SaberModDetected = false;


		private readonly ServerInfo serverInfo = new ServerInfo();
		public ServerInfo ServerInfo {
			get {
				string serverInfoCSStr = this.GetConfigstring(GameState.ServerInfo);
				var info = new InfoString(serverInfoCSStr);
				string systemInfoCSStr = this.GetConfigstring(GameState.SystemInfo);
				var info2 = new InfoString(systemInfoCSStr);
				this.serverInfo.Address = this.serverAddress;
				this.serverInfo.Clients = this.serverInfo.ClientsIncludingBots = this.ClientInfo?.Count(ci => ci.InfoValid) ?? 0;
				this.serverInfo.SetConfigstringInfo(info);
				this.serverInfo.SetSystemConfigstringInfo(info2);
				this.ClientHandler.SetExtraConfigstringInfo(this.serverInfo, info);
				if (this.serverInfo.GameName.ToLowerInvariant().Contains("sabermod"))
				{
					this.SaberModDetected = true;
				}
				return this.serverInfo;
			}
		}
		
		
		public event Action<ServerInfo,bool> ServerInfoChanged; // bool says whether the change included a new gamestate
		public JKClient(IClientHandler clientHandler, SocksProxy? proxy = null) : base(clientHandler, proxy) {
			if(clientHandler is MOHClientHandler)
            {
				userInfo = new InfoString(UserInfoMOH,mohCvars.Reverse());
			}
			this.Status = ConnectionStatus.Disconnected;
			this.port = random.Next(1, 0xffff) & 0xffff;
			this.reliableCommands = new sbyte[this.MaxReliableCommands][];
			this.serverCommandMessagenums = new int[this.MaxReliableCommands];
			this.serverCommands = new sbyte[this.MaxReliableCommands][];
			for (int i = 0; i < this.MaxReliableCommands; i++) {
				this.serverCommands[i] = new sbyte[Common.MaxStringCharsMOH];
				this.reliableCommands[i] = new sbyte[Common.MaxStringCharsMOH];
			}
		}
		private protected override void OnStart() {
			//don't start with any pending actions
			this.DequeueActions(false);
			base.OnStart();
		}
		private protected override void OnStop(bool afterFailure) {
			this.StopRecord_f();
			this.connectTCS?.TrySetCanceled();
			this.connectTCS = null;
			this.Status = ConnectionStatus.Disconnected;
			if (afterFailure) {
				this.DequeueActions();
				this.ClearState();
				this.ClearConnection();
			}
			base.OnStop(afterFailure);
		}
		private protected override async Task Run() {
			long frameTime, lastTime = Common.Milliseconds;
			int msec;
			this.realTime = 0;
			while (true) {
				if (!this.Started) {
					break;
				}
				if (this.realTime - this.lastPacketTime > JKClient.LastPacketTimeOut && this.Status == ConnectionStatus.Active) {
					var cmd = new Command(new string []{ "disconnect", "Last packet from server was too long ago" });
					this.Disconnect();
					this.ServerCommandExecuted?.Invoke(new CommandEventArgs(cmd, this.serverMessageSequence));
				}
				this.GetPacket();
				frameTime = Common.Milliseconds;
				msec = (int)(frameTime - lastTime);
				if (msec > 5000) {
					msec = 5000;
				}
				this.DequeueActions();
				lastTime = frameTime;
				this.realTime += msec;
				this.Stats.lastFrameDelta = msec;
				this.SendCommand();
				this.CheckForResend();
				this.SetTime();
				if (this.Status >= ConnectionStatus.Primed) {
					this.clientGame.Frame(this.serverTime);
				}
				//await Task.Delay(6); // This is taking WAY longer than 3 ms. More like 20-40ms, it's insane
				System.Threading.Thread.Sleep(6); // This ALSO isn't precise (except when debugging ironically?!?! the moment i detach debugger it becomes imprecise and takes forever)
				// Actually it's fine now. I had to increase the timer resolution with the thingie thang
			}
			//complete all actions after stop
			this.DequeueActions();
		}
		private void DequeueActions(bool invoke = true) {
#if NETSTANDARD2_1
			if (!invoke) {
				this.actionsQueue.Clear();
				return;
			}
#endif
			while (this.actionsQueue.TryDequeue(out var action)) {
				if (invoke) {
					action?.Invoke();
				}
			}
		}
		public void SetUserInfoKeyValue(string key, string value) {
			key = key.ToLower();
			if (key == "name") {
				this.Name = value;
			} else if (key == "password") {
				this.Password = value;
			} else if (key == this.GuidKey) {
				this.Guid = Guid.TryParse(value, out Guid guid) ? guid : Guid.Empty;
			} else {
				this.userInfo[key] = value;
				this.UpdateUserInfo();
			}
		}
		public string GetUserInfoKeyValue(string key, out bool valueExists) {
			key = key.ToLower();
			valueExists = this.userInfo.ContainsKey(key);
			return this.userInfo[key];
		}
		private void UpdateUserInfo(bool skippable = true) {
			if (this.Status < ConnectionStatus.Challenging) {
				return;
			}
			if(skippable && Interlocked.Read(ref skipUserInfoChangeCount) > 0)
            {
				Interlocked.Decrement(ref skipUserInfoChangeCount);
			} else
            {
				this.ExecuteCommandInternal($"userinfo \"{userInfo}\"");
			}
		}


		// MOHAA stuff.
		byte[] MD5Print(byte[] input)
		{
			byte[] output = new byte[32];
			char[] hex_digits = new char[] {'0','1','2','3','4','5','6','7','8','9','a','b','c','d','e','f'};
			uint i;

			for (i = 0; i < 16; i++)
			{
				output[i * 2] = (byte)hex_digits[input[i] / 16];
				output[i * 2 + 1] = (byte)hex_digits[input[i] % 16];
			}
			return output;
		}
		enum CDResponseMethod
		{
			CDResponseMethod_NEWAUTH, // method = 0 for normal auth
			CDResponseMethod_REAUTH   // method = 1 for ison proof
		}
		private string mohCdKey = "                                ";
		// method = 0 for normal auth response from game server
		// method = 1 for reauth response originating from keymaster
		byte[] mohGCDComputeResponse(string cdkey, string challenge, CDResponseMethod method)
		{
			const int RESPONSE_SIZE = 73;
			const int RAWSIZE = 512;
			string rawout = null;
			uint anyrandom;
			string randstr = null;


			/* check to make sure we weren't passed a huge cd key/challenge */
			if (cdkey.Length * 2 + challenge.Length + 8 >= RAWSIZE)
			{
				return Encoding.ASCII.GetBytes("CD Key or challenge too long");
			}

			Random rnd = new Random();
			anyrandom = ((uint)rnd.Next(0, ((int)ushort.MaxValue) + 1) << 16 | (uint)rnd.Next(0, ((int)ushort.MaxValue) + 1));
			randstr = anyrandom.ToString("{0:X8}");

            if (method == 0)
            {
				rawout = cdkey + (anyrandom % 0xFFFF) + challenge;
            } else
			{
				rawout = challenge + (anyrandom % 0xFFFF) + cdkey;
			}

			using (MD5 md5 = MD5CryptoServiceProvider.Create())
            {
				byte[] response = new byte[RESPONSE_SIZE];
				byte[] part1 = MD5Print(md5.ComputeHash(Encoding.ASCII.GetBytes(cdkey)));
				Array.Copy(part1, response,part1.Length);
				byte[] part2 = Encoding.ASCII.GetBytes(randstr);
				Array.Copy(part2, 0, response,32, part2.Length);
				byte[] part3 = MD5Print(md5.ComputeHash(Encoding.ASCII.GetBytes(rawout)));
				Array.Copy(part3, 0, response, 40, part3.Length);
				return response;
			}
		}
		// MOHAA stuff end

		private void CheckForResend() {
			if (this.Status != ConnectionStatus.Connecting && this.Status != ConnectionStatus.Challenging && this.Status != ConnectionStatus.Authorizing) {
				return;
			}
			if (this.realTime - this.connectTime < JKClient.RetransmitTimeOut) {
				return;
			}
			this.connectTime = this.realTime;
			this.connectPacketCount++;
			switch (this.Status) {
			case ConnectionStatus.Authorizing: // MOH stuff.
				byte[] response = this.mohGCDComputeResponse(this.mohCdKey,this.getKeyChallenge,CDResponseMethod.CDResponseMethod_REAUTH);
				string responseString = Encoding.ASCII.GetString(response);
					Debug.WriteLine($"Sending authorizeThis (reauth) command to {this.serverAddress.ToString()}");
					this.OutOfBandPrint(this.serverAddress, $"authorizeThis {responseString}");
				break;
			case ConnectionStatus.Connecting:
				this.ClientHandler.RequestAuthorization(this.CDKey, (address, data2) => {
					this.OutOfBandPrint(address, data2);
				});
				if(this.ClientHandler is MOHClientHandler)
				{
					Debug.WriteLine($"Sending getchallenge command to {this.serverAddress.ToString()}");
					this.OutOfBandPrint(this.serverAddress, $"getchallenge");
				} else
				{
					Debug.WriteLine($"Sending getinfo xxx and getchallenge command to {this.serverAddress.ToString()}");
					this.OutOfBandPrint(this.serverAddress, "getinfo xxx");
					this.infoRequestTime = this.realTime;
					this.OutOfBandPrint(this.serverAddress, $"getchallenge {this.challenge}");
				}
				break;
			case ConnectionStatus.Challenging:
                if (this.ClientHandler is MOHClientHandler || this.serverInfo.InfoPacketReceived) // Don't challenge until we have serverInfo.
                {
                    if (!(this.ClientHandler is MOHClientHandler) && this.serverInfo.NWH)
                    {
						string nwhEngineString = NWHEngine;
						this.userInfo["engine"] = nwhEngineString != null ? nwhEngineString : "demoBot"; // Try not to get influenced by servers blocking JKChat
                    }

					string data = "";
					Debug.WriteLine($"Sending connect command to {this.serverAddress.ToString()}");
					if(this.ClientHandler is MOHClientHandler)
                    {
						if(this.Protocol >= (int)ProtocolVersion.Protocol6 && this.Protocol <= (int)ProtocolVersion.Protocol8)
                        {

							data = $"connect \"\\challenge\\{this.challenge}\\qport\\{this.port}\\protocol\\{this.Protocol}{this.userInfo}\"";
                        } else
                        {

							data = $"connect \"\\clientType\\Breakthrough\\challenge\\{this.challenge}\\qport\\{this.port}\\protocol\\{this.Protocol}{this.userInfo}\"";
                        }
					}
                    else
                    {
						data = $"connect \"{this.userInfo}\\protocol\\{this.Protocol}\\qport\\{this.port}\\challenge\\{this.challenge}\"";
					}
					this.OutOfBandData(this.serverAddress, data, data.Length);
				} else if (this.realTime - this.infoRequestTime >= JKClient.RetransmitTimeOut) // Maybe the request or answer to the request got lost somewhere... let's ask again.
                {
					this.OutOfBandPrint(this.serverAddress, "getinfo xxx");
					this.infoRequestTime = this.realTime;
				}
				break;
			}
		}
		private unsafe void Encode(in Message msg) {
			if (msg.CurSize <= 12) {
				return;
			}
			msg.SaveState();
			msg.BeginReading();
			int serverId = msg.ReadLong();
			int messageAcknowledge = msg.ReadLong();
			int reliableAcknowledge = msg.ReadLong();
			msg.RestoreState();
			fixed (sbyte *b = this.serverCommands[reliableAcknowledge & (this.MaxReliableCommands-1)]) {
				fixed (byte *d = msg.Data) {
					byte *str = (byte*)b;
					int index = 0;
					byte key = (byte)(this.challenge ^ serverId ^ messageAcknowledge);
					for (int i = 12; i < msg.CurSize; i++) {
						if (str[index] == 0)
							index = 0;
						if ((!this.ClientHandler.FullByteEncoding && str[index] > 127) || str[index] == 37) { //'%'
							key ^= (byte)(46 << (i & 1)); //'.'
						} else {
							key ^= (byte)(str[index] << (i & 1));
						}
						index++;
						*(d + i) = (byte)(*(d + i) ^ key);
					}
				}
			}
		}
		private unsafe void Decode(in Message msg) {
			msg.SaveState();
			msg.Bitstream();
			int reliableAcknowledge = msg.ReadLong();
			msg.RestoreState();
			fixed (sbyte *b = this.reliableCommands[reliableAcknowledge & (this.MaxReliableCommands-1)]) {
				fixed (byte *d = msg.Data) {
					byte *str = (byte*)b;
					int index = 0;
					byte key = (byte)(this.challenge ^ *(uint*)d);
					for (int i = msg.ReadCount + 4; i < msg.CurSize; i++) {
						if (str[index] == 0)
							index = 0;
						if ((!this.ClientHandler.FullByteEncoding && str[index] > 127) || str[index] == 37) { //'%'
							key ^= (byte)(46 << (i & 1)); //'.'
						} else {
							key ^= (byte)(str[index] << (i & 1));
						}
						index++;
						*(d + i) = (byte)(*(d + i) ^ key);
					}
				}
			}
#if STRONGREADDEBUG
			msg.doDebugLogExt($"Decode()");
#endif
		}


		private void AddServerFpsMeasurementSample(int messageCount)
        {
			if(messageCount <= 0)
            {
				// Maybe at start of connection? idk
				Debug.WriteLine("AddServerFpsMeasurementSample: messageCount is 0?!");
				return;
            }
			else if(messageCount >= 10)
            {
				// Could this possibly ever happen? Not sure. Maybe with some weird sort of reset or reconnect? Well, or with a really terrible connection obviously.
				Debug.WriteLine("AddServerFpsMeasurementSample: messageCount >= 10, let's skip this to be safe. Maybe it's some glitch.");
				return;
            }

			// For measuring the "fps" we get from the server.
			long messageReceivedTime = Common.Milliseconds;
			int delta = ((int)(messageReceivedTime - this.lastMessageReceivedTime))/ messageCount;
			if(delta > 0 && delta < 999) // No use subtracting against 0 at start for example, or during maploads and such, doesn't really tell us much.
			{
				this.messageIntervals[this.messageIntervalMeasurementIndex++ % messageIntervalsMeasureCount] = delta;
				if(this.messageIntervalMeasurementIndex >= messageIntervalsMeasureCount)
                {
					int total = 0;
					for(int i=0;i< messageIntervalsMeasureCount; i++)
                    {
						total += this.messageIntervals[i];
					}
					this.messageIntervalAverage = total / messageIntervalsMeasureCount;

				}
			}
			this.lastMessageReceivedTime = messageReceivedTime;
		}

		private unsafe bool MessageCheckSuperSkippable(Message msg, int deltaNum, ref bool superSkippableButBotMovement)
        {
			int maxClients = this.ClientHandler.MaxClients;
			int randomTmpValue = 0;
			bool isMOH = this.ClientHandler is MOHClientHandler;
			ProtocolVersion protocol = (ProtocolVersion)this.Protocol;
			bool isPilot()
			{
				return msg.ReadBits(1) != 0;
			}

			// Do more investigation. See if the delta snapshot contains any new info, or if all entities have just remained exactly same. If there is not a single changed entity or playerstate thingie,
			// mark superSkippable
			int snapFlags = msg.ReadByte();

			// Areabytess
			int len = msg.ReadByte();
			msg.ReadData(null, len);

			// If any fields changed, not super skippable
			var fields = this.ClientHandler.GetPlayerStateFields(false, isPilot);
			int lc = msg.ReadByte();
            if (lc > fields.Count)
            {
				throw new JKClientException($"MessageCheckSuperSkippable: ps lc was {lc} but field count is {fields.Count}");
			}
			int psClientNum = this.snap.PlayerState.ClientNum;
			if (lc > 0) {
				bool psSuperSkippable = true;
				for (int i = 0; i < lc; i++)
				{
					if (msg.ReadBits(1) != 0)
					{
						if (fields[i].Name != nameof(PlayerState.CommandTime))
						{
							psSuperSkippable = false;
						}// else
                        {
							int value = -1;
							if (isMOH)
							{
								switch (fields[i].Type)
								{
									case NetFieldType.regular:
										msg.ReadRegularSimple(fields[i].Bits, &randomTmpValue, protocol);
										break;
									case NetFieldType.angle:
										msg.ReadPackedAngle(fields[i].Bits, protocol);
										break;
									case NetFieldType.coord:
										msg.ReadPackedCoord(0, fields[i].Bits, protocol);
										break;
									case NetFieldType.coordExtra:
										msg.ReadPackedCoordExtra(0, fields[i].Bits, protocol);
										break;
									case NetFieldType.velocity:
										msg.ReadPackedVelocity(fields[i].Bits);
										break;
									default:
										break;
								}
							} else { 
								int bits = fields[i].Bits;
								randomTmpValue = msg.ReadBits( bits == 0 ? (msg.ReadBits(1) == 0 ? Message.FloatIntBits : 32) : bits); // Very short form of reading a field and discarding it the result. Edit: actually we keep the result now to read clientnum

							}
							if (fields[i].Name == nameof(PlayerState.ClientNum))
							{
								psClientNum = randomTmpValue;
							}
						}

					}
				}
                if (!psSuperSkippable)
                {
                    if (psClientNum >=0 && psClientNum < maxClients && this.ClientIsConfirmedBot[psClientNum])
                    {
						superSkippableButBotMovement = true;
					} else
                    {
						return false;
                    }
                }
			}

			// If any additional values/stats changed, not super skippable
			if (msg.ReadBits(1) != 0) return false;

			// Sad, since nothing changed we have to check if the PS we'd be deltaing from has vehiclenum for JKA
			if (this.ClientHandler.CanParseVehicle)
			{
				bool vehicleSuperSkippable = true;
				var oldSnapHandle = GCHandle.Alloc(this.snapshots, GCHandleType.Pinned);
				var oldSnap = ((ClientSnapshot*)oldSnapHandle.AddrOfPinnedObject()) + (deltaNum & JKClient.PacketMask);
				if (oldSnap->PlayerState.VehicleNum != 0) {
					// If any fields changed, not super skippable
					lc = msg.ReadByte();
					if (lc > 0) {
						var vehFields = this.ClientHandler.GetPlayerStateFields(true, isPilot);
						// Some fields changed
						// We will allow a change for "CommandTime" because that one is updated even if someone is afk.
						for (int i = 0; i < lc; i++)
                        {
							if (msg.ReadBits(1) != 0)
							{
								if (vehFields[i].Name != nameof(PlayerState.CommandTime))
								{
									vehicleSuperSkippable = false;
									//return false; // hmm how to deal with bots and vehicles? maybe solve another time, not sure how this is communicated.
								}
								//else
								{
									int bits = vehFields[i].Bits;
									msg.ReadBits(bits == 0 ? (msg.ReadBits(1) == 0 ? Message.FloatIntBits : 32) : bits); // Very short form of reading a field and discarding it the result.
								}

							}
						}
					}

					// If any additional values/stats changed, not super skippable
					if (msg.ReadBits(1) != 0) return false;
				}
				oldSnapHandle.Free();
				if (!vehicleSuperSkippable)
				{
					if (psClientNum >= 0 && psClientNum < maxClients && this.ClientIsConfirmedBot[psClientNum])
					{
						superSkippableButBotMovement = true;
					}
					else
					{
						return false;
					}
				}
			}

			// Entities
			int safetyIndex = -1; // Safety index should NEVER be needed but a malformed message could result in this and cause an infinite loop perhaps? Though at that point we likely have other problems...
			var eFields = this.ClientHandler.GetEntityStateFields();
			int entityStateCommandTimeOffset = Marshal.OffsetOf<EntityState>(nameof(EntityState.Position)).ToInt32() + Marshal.OffsetOf<Trajectory>(nameof(Trajectory.Time)).ToInt32();
			while (true && ++safetyIndex <= Common.MaxGEntities)
            {
				int newnum = msg.ReadBits(Common.GEntitynumBits);
				if (newnum == Common.MaxGEntities - 1) break;
				if (msg.ReadBits(1) == 1) return false; // It's a removal, hence a change.
				if (msg.ReadBits(1) == 1) {
					// It has chnaged fields, hence a change.
					// But we might allow commandtime change.
					lc = msg.ReadByte();
					if (lc > eFields.Count)
					{
						throw new Exception($"MessageCheckSuperSkippable: es lc was {lc} but field count is {fields.Count}");
					}
					for (int i = 0; i < lc; i++)
					{
						if (msg.ReadBits(1) != 0)
						{
							if (eFields[i].Offset != entityStateCommandTimeOffset)
							{
								if (newnum >= 0 && newnum < maxClients && this.ClientIsConfirmedBot[newnum])
								{
									superSkippableButBotMovement = true;
								}
								else
								{
									return false;
								}
							}
							//else
							{
                                if (isMOH)
                                {

									switch (eFields[i].Type)
									{
										case NetFieldType.regular:
											msg.ReadRegular(eFields[i].Bits, &randomTmpValue, protocol);
											break;
										case NetFieldType.angle: // angles, what a mess! it wouldnt surprise me if something goes wrong here ;)
											msg.ReadPackedAngle(eFields[i].Bits, protocol);
											break;
										case NetFieldType.animTime: // time
											msg.ReadPackedAnimTime(eFields[i].Bits, 0, 0, protocol);
											break;
										case NetFieldType.animWeight: // nasty!
											msg.ReadPackedAnimWeight(eFields[i].Bits, protocol);
											break;
										case NetFieldType.scale:
											msg.ReadPackedScale(eFields[i].Bits, protocol);
											break;
										case NetFieldType.alpha:
											msg.ReadPackedAlpha(eFields[i].Bits, protocol);
											break;
										case NetFieldType.coord:
											msg.ReadPackedCoord(0, eFields[i].Bits, protocol);
											break;
										case NetFieldType.coordExtra:
											msg.ReadPackedCoordExtra(0, eFields[i].Bits, protocol);
											break;
										case NetFieldType.velocity:
											msg.ReadPackedVelocity(eFields[i].Bits);
											break;
										case NetFieldType.simple:
											msg.ReadPackedSimple(0, eFields[i].Bits);
											break;
										default:
											throw new Exception($"MessageCheckSuperSkippable (MOH): unrecognized entity field type {i} for field\n");
											//break;
									}
								} else
                                {

									int bits = eFields[i].Bits;
									if(msg.ReadBits(1) != 0)
									{
										msg.ReadBits(bits == 0 ? (msg.ReadBits(1) == 0 ? Message.FloatIntBits : 32) : bits);
									}
								}
							}

						}
					}
				}

			}
            if (!isMOH)
            {
				return safetyIndex <= Common.MaxGEntities; // Uh. Hm. If safetyIndex is > Common.MaxGEntities (or anywhere close really) we have some messed up message to deal with, so would be better to discard anyway ig? lol whatever we're not determining that here.
			}
			if(safetyIndex > Common.MaxGEntities)
            {
				return false;
            }

			// MOH
			if (msg.ReadBits(1) != 0) // this is MSG_ReadSounds. If ReadBits(1) is 0 then no change.
            {
				return false;
            }

			return msg.ReadByte() == 11; // Check if it's an EOF. MOH can have other message type stuff appended too. If so, it's not super skippable.


		}

		// Basically: Is this message's first server command a snapshot (indicating no gamestate/commands in this message)?
		// And: Is the snapshot delta? 
		// We don't ever wanna skip messasges with gamestate or commands, or with non-delta messages.
		// But we can skip delta messages to artificially limit snaps.
		// Superskippable: delta snapshot with no changes.
		private bool MessageIsSkippable(in Message msg, int newSnapNum, ref int serverTimeHere, ref bool superSkippable, ref bool superSkippableButBotMovement)
        {
			bool isMOH = this.ClientHandler is MOHClientHandler;

			superSkippable = false;
			bool canSkip = false;
			// Pre-parse a bit of the messsage to see if it contains anything but snapshot as first thing
			msg.SaveState();
			msg.Bitstream();
			_ = msg.ReadLong(); // Reliable acknowledge, don't care.
			if (msg.ReadCount > msg.CurSize)
			{
				throw new JKClientException("ParseServerMessage (pre): read past end of server message");
			}
			ServerCommandOperations cmd = (ServerCommandOperations)msg.ReadByte();
			this.ClientHandler.AdjustServerCommandOperations(ref cmd);
			bool newCommands = false;
			while(cmd == ServerCommandOperations.ServerCommand)
            {
				int seq = msg.ReadLong();
				_ = msg.ReadString((ProtocolVersion)this.Protocol);
				if (this.serverCommandSequence < seq)
				{
					newCommands = true;
					Stats.messagesUnskippableNewCommands++;
				}
				cmd = (ServerCommandOperations)msg.ReadByte();
				this.ClientHandler.AdjustServerCommandOperations(ref cmd);
			}
            if (!newCommands)
            {
				if (cmd == ServerCommandOperations.Snapshot)
				{
					serverTimeHere = msg.ReadLong();
                    if (isMOH)
                    {
						msg.ReadByte(); // serverTimeResidual
					}
					int deltaNum = msg.ReadByte();
					int theDeltaNum = 0;
					if (deltaNum == 0)
					{
						theDeltaNum = -1;
					}
					else
					{
						theDeltaNum = newSnapNum - deltaNum;
					}
					if (theDeltaNum > 0)
					{
						Stats.messagesSkippable++;
						canSkip = true; // This is the only situation where we wanna skip. Message contains no gamestate or commands, only snapshot, and it's a delta snapshot.

						superSkippable = MessageCheckSuperSkippable(msg, theDeltaNum, ref superSkippableButBotMovement);
                        if (superSkippable)
                        {
                            if (superSkippableButBotMovement)
                            {
								Stats.messagesSuperSkippableButBotMovement++;
							} else
							{
								Stats.messagesSuperSkippable++;
							}
						}
					}
					else
					{
						Stats.messagesUnskippableNonDelta++;
					}
				}
				else
				{
					Stats.messagesUnskippableSvc++;
				}
			}
			msg.RestoreState();
			return canSkip;
		}

		private protected override unsafe void PacketEvent(in NetAddress address, in Message msg) {

			bool isMOH = this.ClientHandler is MOHClientHandler;

//			this.lastPacketTime = this.realTime;
			int headerBytes;
			fixed (byte *b = msg.Data) {
				if (msg.CurSize >= 4 && *(int*)b == -1) {
					this.ConnectionlessPacket(address, msg);
					return;
				}
				if (this.Status < ConnectionStatus.Connected) {
					return;
				}
				if (msg.CurSize < 4) {
					return;
				}
				if (address != this.netChannel.Address) {
					return;
				}
				int sequenceNumber =0;
				bool validButOutOfOrder=false;
				bool process = this.netChannel.Process(msg,isMOH, ref sequenceNumber, ref validButOutOfOrder);
#if STRONGREADDEBUG
				msg.doDebugLogExt($"PacketEvent: process {process}, sequenceNumber {sequenceNumber}, validButOutOfOrder {validButOutOfOrder}");
#endif
				bool detectSuperSkippable = true;

				// Save to demo queue
				if(process || validButOutOfOrder)
                {
					bool LastMessageWasDemoAFKDropRemember = LastMessageWasDemoAFKDrop;
					LastMessageWasDemoAFKDrop = false;

                    if (!LastMessageWasDemoAFKDropRemember)
                    {
						DemoAfkSnapsDropLastDroppedMessage = null;
						DemoAfkSnapsDropLastDroppedMessageNumber = -1;
                    }

					this.Decode(msg);

					Stats.totalMessages++;
                    if (validButOutOfOrder)
                    {
						Stats.messagesOutOfOrder++;
					}

					// Clientside snaps limiting if requested
					if (process && (clientForceSnaps || AfkDropSnaps))
					{
						int newSnapNum = *(int*)b;
						int newServerTime = 0;

						bool didWeSkipThis = false;
						bool superSkippable = false;
						bool superSkippableButBotMovement = false;
						if (MessageIsSkippable(in msg, newSnapNum, ref newServerTime, ref superSkippable, ref superSkippableButBotMovement))
						{
							// TODO Measure round trip from client to server and back 
							// Then subtract that from measured PACKET_BACKUP(32)/sv snaps
							// Then give a bit of safety delay (50ms?)
							// and make that the maximum time that is skipped
							// Reason: If we go past the server's PACKET_BACKUP, we start getting non-delta snaps. Bad, those are big and unskippabble.
							// Roundtrip because it seems that just measuring my own ping to Australia for example and duplicating it doesn't yield the correct
							// value to prevent non-delta snaps. The actual fps value can be lower than that number would suggest.
							// How to measure roundtrip? Send a reliable message, remember when it was sent, and see when it is acked.
							bool wasExplicitlyNotSkipped = false;
							int oldServerTime = this.snap.ServerTime;
							int timeDelta = newServerTime - oldServerTime;
							if (clientForceSnaps)
                            {
								int minDelta = 1000 / this.desiredSnaps;

								// Give it a bit of tolerance. 5 percent
								// Because for example snaps 2 will result in time distances like 493 instead of 500 ms.
								// That's technically only 2 percent. But whatever, let's give it 5 percent tolerance.
								// And it's rounded anyway. So it'd only apply with minDelta 20+ ms anyway, which would be 50 fps. So if we requesst 50fps, it might allow 52 fps.
								minDelta -= minDelta / 20;

								minDelta = Math.Min(this.deltaSnapMaxDelay, minDelta); // Safety maximum to avoid non-delta snaps. Is dynamically adjusted because it depends on a lot of things (ping, sv_fps, snaps etc)

								if (timeDelta < minDelta)
								{
									didWeSkipThis = true;
									Stats.messagesSkipped++;
									return; // We're skipping this one.
								}
								else
								{
									//Stats.messagesNotSkippedTime++;
									wasExplicitlyNotSkipped = true;
								}
							}
							if(!didWeSkipThis && AfkDropSnaps)
                            {
                                if (superSkippable) { 

									int maxDelta = superSkippableButBotMovement ? (1000 / this.AfkDropSnapsMinFPSBots) : (1000 / this.AfkDropSnapsMinFPS);

									maxDelta = Math.Min(this.deltaSnapMaxDelay, maxDelta); // Dynamically adjusted safety to avoid non-delta snaps.

									// Afk works with min fps instead of maxfps. Because we'd love to skip ALL afk messages ofc. 
									// But if we skip too many, we start getting non-delta packs. So find a compromise that makes sense.
									// It also depends on ping and sv_fps/server snaps. Server keeps a certain amount of PACKET_BACKUP.
									// Once server runs out of PACKET_BACKUP, we start getting non-delta snaps, which take up more space.
									// For now, we leave it up to the user to choose a reasonable setting. But we could maybe automate it at some point.
									if(timeDelta <= maxDelta)
									{
										DemoAfkSnapsDropLastDroppedMessage = new BufferedDemoMessageContainer()
										{
											msg = msg.Clone(),
											time = DateTime.Now,
											serverTime = newServerTime,
											containsFullSnapshot = false // To be determined
										};
										DemoAfkSnapsDropLastDroppedMessageNumber = sequenceNumber;
										LastMessageWasDemoAFKDrop = true;
										didWeSkipThis = true;
										Stats.messagesSkipped++;
										return; // We're skipping this one.
									}
									else
									{
										//Stats.messagesNotSkippedTime++;
										wasExplicitlyNotSkipped = true;
									}
                                }
								
							}
                            if (wasExplicitlyNotSkipped)
                            {
								Stats.messagesNotSkippedTime++;
							}
						}


						if (LastMessageWasDemoAFKDropRemember && Demorecording && !didWeSkipThis && DemoAfkSnapsDropLastDroppedMessageNumber != -1 && DemoAfkSnapsDropLastDroppedMessage != null)
						{
							lock (bufferedDemoMessages)
							{
								if (bufferedDemoMessages.ContainsKey(DemoAfkSnapsDropLastDroppedMessageNumber))
								{
									// VERY WEIRD. 
								}
								else
								{
									bufferedDemoMessages.Add(DemoAfkSnapsDropLastDroppedMessageNumber, DemoAfkSnapsDropLastDroppedMessage);
									/*if (validButOutOfOrder)
									{
										this.Stats.messagesDropped--;
									}*/ // Hmm might need some handling for better stats when using this afk dropping stuff? Oh well.
								}
							}
						}
					}
					

					if (Demorecording)
					{
						lock (bufferedDemoMessages)
						{
							if (bufferedDemoMessages.ContainsKey(sequenceNumber))
							{
								// VERY WEIRD. 
							}
							else
							{
								bufferedDemoMessages.Add(sequenceNumber, new BufferedDemoMessageContainer()
								{
									msg = msg.Clone(),
									time = DateTime.Now,
									containsFullSnapshot = false // To be determined
								});
                                if (validButOutOfOrder)
                                {
									this.Stats.messagesDropped--;
								}
							}
						}
						
					}
				}


				if (!process)
				{
					return;
				} else
                {
					this.Stats.messagesDropped += this.netChannel.dropped;
				}

				lastActiveMessage = msg;

				// the header is different lengths for reliable and unreliable messages
				headerBytes = msg.ReadCount;

				AddServerFpsMeasurementSample(*(int*)b - this.serverMessageSequence);

				this.serverMessageSequence = *(int*)b;
				this.maxSequenceNum = Math.Max(this.serverMessageSequence,this.maxSequenceNum);
				this.lastPacketTime = this.realTime;
				this.ParseServerMessage(msg);


				if ((this.nonDeltaSnapsBitmask & (1UL << (int)(this.nonDeltaSnapsBitmaskIndex % 64))) != 0) // Last message we got was non delta.
				{
					// TODO Burst resistance when internet is acting a bit weird/spiking for a short amount of time? To not go down too much?

					// We received non delta snaps.
					// Increase the maximum delay a bit.
					if(this.lastDeltaSnapMaxDelayAdjustmentWasUp){
						this.deltaSnapMaxDelay -= 10;
					}
					this.deltaSnapMaxDelay -= Math.Max(1, this.nonDeltaSnapsBitmask.PopCount()*5/64);
					if (this.deltaSnapMaxDelay < 0)
					{
						this.deltaSnapMaxDelay = 0;
					}
					this.lastDeltaSnapMaxDelayAdjustment = DateTime.Now;
					this.lastDeltaSnapMaxDelayAdjustmentWasUp = false;
				} else if(this.nonDeltaSnapsBitmask == 0 && (DateTime.Now- this.lastDeltaSnapMaxDelayAdjustment).TotalMilliseconds > 5000)
                {
					// Not received any non-deltas for 5 seconds. Try going up a bit again?
					this.deltaSnapMaxDelay += 1;
					if(this.deltaSnapMaxDelay > 1000)
                    {
						this.deltaSnapMaxDelay = 1000;
					}
					this.lastDeltaSnapMaxDelayAdjustment = DateTime.Now;
					this.lastDeltaSnapMaxDelayAdjustmentWasUp = true;
				}
				this.Stats.deltaSnapMaxDelay = this.deltaSnapMaxDelay;
				

				//
				// we don't know if it is ok to save a demo message until
				// after we have parsed the frame
				//
				if (Demorecording && Demowaiting==0 && !DemoSkipPacket)
				{
					//WriteDemoMessage(msg, headerBytes);
					WriteBufferedDemoMessages();
				}
				//DemoSkipPacket = false; // Reset again for next message
											 // TODO Maybe instead make a queue of packages to be written to the demo file.
											 // Then just read them in the correct order. That way we can integrate even packages out of order.
											 // However it's low priority bc this error is relatively rare.
			}
		}
		private void ConnectionlessPacket(NetAddress address, Message msg) {
			bool isMOH = this.ClientHandler is MOHClientHandler;
			msg.BeginReading(true);
			msg.ReadLong();

			if(this.ClientHandler is MOHClientHandler)
            {
				msg.ReadByte(); // Direction byte. Just ignore. MOH stuff.
			}

			string s = msg.ReadStringLineAsString((ProtocolVersion)this.Protocol);
			var command = new Command(s);
			string c = command.Argv(0);
			if (string.Compare(c, "infoResponse", StringComparison.OrdinalIgnoreCase) == 0)
			{
				// Minimalistic handling. We only need some basic info.
				var info = new InfoString(msg.ReadStringAsString((ProtocolVersion)this.Protocol));
				this.serverInfo.Ping = (int)(Common.Milliseconds - serverInfo.Start);
				this.serverInfo.SetInfo(info);
				this.serverInfo.InfoPacketReceived = true;
				this.serverInfo.InfoPacketReceivedTime = DateTime.Now;
				this.ServerCommandExecuted?.Invoke(new CommandEventArgs(command, -1));
			}
			else if (string.Compare(c, "getKey", StringComparison.OrdinalIgnoreCase) == 0) {
				if (this.Status != ConnectionStatus.Connecting) {
					return;
				}
				//c = command.Argv(2);
				if (address != this.serverAddress) {
					//if (string.IsNullOrEmpty(c) || c.Atoi() != this.challenge)
						return;
				}
				this.Status = ConnectionStatus.Authorizing;
				this.getKeyChallenge = command.Argv(1);
				byte[] response = this.mohGCDComputeResponse(this.mohCdKey, this.getKeyChallenge, CDResponseMethod.CDResponseMethod_NEWAUTH);
				string responseString = Encoding.ASCII.GetString(response);
				Debug.WriteLine($"Sending authorizeThis (newauth) command to {this.serverAddress.ToString()}");
				this.OutOfBandPrint(this.serverAddress, $"authorizeThis {responseString}");
				this.connectTime = isMOH ? (this.realTime - JKClient.RetransmitTimeOut + this.mohConnectTimeExtraDelay) : -99999; // MOH is weird. If you send two connectionless commands very shortly after each other, for some reason the server will not properly process/receive the second one. 
				this.connectPacketCount = 0;
				this.serverAddress = address;
				this.ServerCommandExecuted?.Invoke(new CommandEventArgs(command, -1));
			} else if (string.Compare(c, "challengeResponse", StringComparison.OrdinalIgnoreCase) == 0) {
				if (this.Status != ConnectionStatus.Connecting && this.Status != ConnectionStatus.Authorizing) {
					return;
				}
				c = command.Argv(2);
				if (address != this.serverAddress) {
					if (string.IsNullOrEmpty(c) || c.Atoi() != this.challenge)
						return;
				}
				this.challenge = command.Argv(1).Atoi();
				this.Status = ConnectionStatus.Challenging;
				this.connectPacketCount = 0;
				this.connectTime = isMOH ? (this.realTime-JKClient.RetransmitTimeOut+this.mohConnectTimeExtraDelay) : -99999; // MOH is weird. If you send two connectionless commands very shortly after each other, for some reason the server will not properly process/receive the second one. 
				this.serverAddress = address;
				this.ServerCommandExecuted?.Invoke(new CommandEventArgs(command, -1));
			} else if (string.Compare(c, "connectResponse", StringComparison.OrdinalIgnoreCase) == 0) {
                if (!this.GhostPeer) {  // Stay in perpetual challenging mode
					if (this.Status != ConnectionStatus.Challenging) {
						return;
					}
					if (address != this.serverAddress) {
						return;
					}
					this.netChannel = new NetChannel(this.net, address, this.port, this.ClientHandler.MaxMessageLength);
                    this.netChannel.ErrorMessageCreated += NetChannel_ErrorMessageCreated;
					this.Status = ConnectionStatus.Connected;
					this.lastPacketSentTime = -9999;
				}
				this.ServerCommandExecuted?.Invoke(new CommandEventArgs(command, -1));
			} else if (string.Compare(c, "disconnect", StringComparison.OrdinalIgnoreCase) == 0) {
				if (this.netChannel == null) {
					return;
				}
				if (address != this.netChannel.Address) {
					return;
				}
				if (this.realTime - this.lastPacketTime < 3000) {
					return;
				}
				this.ServerCommandExecuted?.Invoke(new CommandEventArgs(command, -1));
				this.Disconnect();
			} else if (string.Compare(c, "echo", StringComparison.OrdinalIgnoreCase) == 0) {
				this.OutOfBandPrint(address, command.Argv(1));
				this.ServerCommandExecuted?.Invoke(new CommandEventArgs(command, -1));
			} else if (string.Compare(c, "print", StringComparison.OrdinalIgnoreCase) == 0) {
				if (address == this.serverAddress) {
					s = msg.ReadStringAsString((ProtocolVersion)this.Protocol,true);
					if (isMOH && this.Status== ConnectionStatus.Challenging && s == "Server is for low pings only\n")
					{
						this.mohConnectTimeExtraDelay -= 10;

						if (this.mohConnectTimeExtraDelay < 0)
						{
							this.mohConnectTimeExtraDelay = 200;
							Debug.WriteLine("JKClient: Cannot connect due to ping. Resetting extra connect delay to 200 and restarting connection process.");
						}
						else
						{
							Debug.WriteLine($"JKClient: Cannot connect due to ping. Trying to lower extra connect delay to {this.mohConnectTimeExtraDelay} and restarting connection process.");
						}
						this.connectTime = -9999;
						this.infoRequestTime = -9999;
						this.connectPacketCount = 0;
						this.Status = ConnectionStatus.Connecting;
					}
					var cmd = new Command(new string []{ "print", s });
					this.ServerCommandExecuted?.Invoke(new CommandEventArgs(cmd, -1));
					Debug.WriteLine(s);
				} else
                {
					this.ServerCommandExecuted?.Invoke(new CommandEventArgs(command, -1));
				}
			} else if (string.Compare(c, "droperror", StringComparison.OrdinalIgnoreCase) == 0) {
				if (address == this.serverAddress) {
					s = msg.ReadStringAsString((ProtocolVersion)this.Protocol,true); 
					if(isMOH && this.Status == ConnectionStatus.Challenging && s == "Server is for low pings only")
                    {
						this.mohConnectTimeExtraDelay -= 10;

						if (this.mohConnectTimeExtraDelay < 0)
                        {
							this.mohConnectTimeExtraDelay = 200;
							Debug.WriteLine("JKClient: Cannot connect due to ping. Resetting extra connect delay to 200 and restarting connection process.");
						} else
                        {
							Debug.WriteLine($"JKClient: Cannot connect due to ping. Trying to lower extra connect delay to {this.mohConnectTimeExtraDelay} and restarting connection process.");
						}
						this.connectTime = -9999;
						this.infoRequestTime = -9999;
						this.connectPacketCount = 0;
						this.Status = ConnectionStatus.Connecting;
					}
					var cmd = new Command(new string []{ "droperror", s }); 
					this.ServerCommandExecuted?.Invoke(new CommandEventArgs(cmd, -1));
					Debug.WriteLine(s);
				} else
                {
					this.ServerCommandExecuted?.Invoke(new CommandEventArgs(command, -1));
				}
			} else {
				Debug.WriteLine(c);
				this.ServerCommandExecuted?.Invoke(new CommandEventArgs(command, -1));
			}
		}

        private void NetChannel_ErrorMessageCreated(string arg1, MessageCopy arg2)
        {
			OnErrorMessageCreated(arg1, null, arg2);
        }

        private void CreateNewCommand()
		{
			if (this.Status < ConnectionStatus.Primed) {
				return;
			}
			int userCmdDelta = this.clServerTime - this.cmds[this.cmdNumber & UserCommand.CommandMask].ServerTime;
			/*if (userCmdDelta==0 && this.clServerTime > 0) // I commented this block out but originally it was userCmdDelta<1 and caused issues. ==0 should be ok but we don't really need this anyway.
            {
				return; // Never let time flow backwards.
			}*/ // actually this seems to do more harm than good. we're already limited with thread.sleep and sometimes servers reset time and we can accidentally get sstuck in MoveNoDelta and record HUGE files.
			
			UserCommand newCmd = new UserCommand() { ServerTime = this.clServerTime };


			List<UserCommand> insertCommands =	new List<UserCommand>();

			//OnUserCommandGenerated(ref this.cmds[this.cmdNumber & UserCommand.CommandMask], in this.cmds[(this.cmdNumber-1) & UserCommand.CommandMask]);
			OnUserCommandGenerated(ref newCmd, in this.cmds[(this.cmdNumber) & UserCommand.CommandMask], ref insertCommands);

			foreach(UserCommand insertCmd in insertCommands) // Allow event handlers to attach additional commands in between the last and current one.
            {
				if(insertCmd.ServerTime > this.cmds[(this.cmdNumber) & UserCommand.CommandMask].ServerTime && insertCmd.ServerTime < newCmd.ServerTime)
                {
					if (!insertCmd.forceWriteThisCmd && insertCmd.IdenticalTo(this.cmds[this.cmdNumber & UserCommand.CommandMask]) && CanCullUserCmd(insertCmd.ServerTime, this.cmds[this.cmdNumber & UserCommand.CommandMask].ServerTime))
					{
						// Skip this. Traffic reduction.
						Debug.WriteLine($"Warning: insert command was traffic reduced. Might wanna use forceWriteThisCmd? Oldservertime: {this.cmds[(this.cmdNumber) & UserCommand.CommandMask].ServerTime}, cmd servertime: {newCmd.ServerTime}");
						this.Stats.userCmdCulled(true);
						continue;
					} else
                    {
						this.Stats.userCmdCulled(false);
					}
					this.cmdNumber++;
					this.cmds[this.cmdNumber & UserCommand.CommandMask] = default;
					this.cmds[this.cmdNumber & UserCommand.CommandMask] = insertCmd;
				} else
                {
					Debug.WriteLine($"Warning: insert command serverTime was {insertCmd.ServerTime} which does not fit within {this.cmds[(this.cmdNumber) & UserCommand.CommandMask].ServerTime} and {newCmd.ServerTime}");
                }
			}

            if (!newCmd.forceWriteThisCmd && newCmd.IdenticalTo(this.cmds[this.cmdNumber & UserCommand.CommandMask]) && CanCullUserCmd(newCmd.ServerTime, this.cmds[this.cmdNumber & UserCommand.CommandMask].ServerTime)) 
            {
				// Skip this. Traffic reduction.
				this.Stats.userCmdCulled(true);
				return;
            } else
			{
				this.Stats.userCmdCulled(false);
			}

			this.Stats.lastUserCommandDelta = userCmdDelta;

			this.cmdNumber++;
			this.cmds[this.cmdNumber & UserCommand.CommandMask] = default;
			this.cmds[this.cmdNumber & UserCommand.CommandMask] = newCmd;

			this.Stats.keyActiveW = newCmd.ForwardMove > 0;
			this.Stats.keyActiveS = newCmd.ForwardMove < 0;
			this.Stats.keyActiveA = newCmd.RightMove < 0;
			this.Stats.keyActiveD = newCmd.RightMove > 0;
			this.Stats.keyActiveJump = newCmd.Upmove > 0;
			this.Stats.keyActiveCrouch = newCmd.Upmove < 0;
			this.Stats.keyActive0 = 0 < (newCmd.Buttons & (1 << 0));
			this.Stats.keyActive1 = 0 < (newCmd.Buttons & (1 << 1));
			this.Stats.keyActive2 = 0 < (newCmd.Buttons & (1 << 2));
			this.Stats.keyActive3 = 0 < (newCmd.Buttons & (1 << 3));
			this.Stats.keyActive4 = 0 < (newCmd.Buttons & (1 << 4));
			this.Stats.keyActive5 = 0 < (newCmd.Buttons & (1 << 5));
			this.Stats.keyActive6 = 0 < (newCmd.Buttons & (1 << 6));
			this.Stats.keyActive7 = 0 < (newCmd.Buttons & (1 << 7));
			this.Stats.keyActive8 = 0 < (newCmd.Buttons & (1 << 8));
			this.Stats.keyActive9 = 0 < (newCmd.Buttons & (1 << 9));
			this.Stats.keyActive10 = 0 < (newCmd.Buttons & (1 << 10));
			this.Stats.keyActive11 = 0 < (newCmd.Buttons & (1 << 11));
			this.Stats.keyActive12 = 0 < (newCmd.Buttons & (1 << 12));
			this.Stats.keyActive13 = 0 < (newCmd.Buttons & (1 << 13));
			this.Stats.keyActive14 = 0 < (newCmd.Buttons & (1 << 14));
			this.Stats.keyActive15 = 0 < (newCmd.Buttons & (1 << 15));
			this.Stats.keyActive16 = 0 < (newCmd.Buttons & (1 << 16));
			this.Stats.keyActive17 = 0 < (newCmd.Buttons & (1 << 17));
			this.Stats.keyActive18 = 0 < (newCmd.Buttons & (1 << 18));
			this.Stats.keyActive19 = 0 < (newCmd.Buttons & (1 << 19));
			this.Stats.keyActive20 = 0 < (newCmd.Buttons & (1 << 20));

		}
		private void SendCommand() {
			if (this.Status < ConnectionStatus.Connected) {
				return;
			}
			this.CreateNewCommand();
			int oldPacketNum = (this.netChannel.OutgoingSequence - 1) & JKClient.PacketMask;
			int delta = this.realTime - this.outPackets[oldPacketNum].RealTime;
			//if (delta < 10) { // Don't limit this. We're already limiting the main loop.
			if (delta < 1) { // Ok let's not be ridiculous.
				return;
			}
			this.Stats.lastUserPacketDelta = delta;
			this.WritePacket();
		}

		private bool CanCullUserCmd(int serverTime, int oldServerTime)
        {
			int serverTimeDeltaSinceLastUserCmd = serverTime - oldServerTime;

			// Reduce network traffic.
			// We have no new commands to sent (neither reliable nor user) and last packet was sent X milliseconds ago where X is smaller than the millisecond value of the minimum client fps we want.
			// Don't wanna be seen as ddosing people but sometimes we do need a high fps (like if we are doing actual gameplay)
			return this.TrafficReduceUntilClientFps > 0 && serverTimeDeltaSinceLastUserCmd > 0 && serverTimeDeltaSinceLastUserCmd < (1000 / this.TrafficReduceUntilClientFps);
		}

		private void WritePacket() {
			if (this.netChannel == null) {
				return;
			}
			bool isMOH = this.ClientHandler is MOHClientHandler;
			lock (this.netChannel) {
				var oldcmd = new UserCommand();
				byte[] data = new byte[this.ClientHandler.MaxMessageLength];
				var msg = new Message(data, sizeof(byte)*this.ClientHandler.MaxMessageLength);
                msg.ErrorMessageCreated += Msg_ErrorMessageCreated;
				msg.Bitstream();
				msg.WriteLong(this.serverId);
				int pingAdjust = PingAdjust;
				if (pingAdjust == 0)
				{
					msg.WriteLong(this.serverMessageSequence);
				}
                else
                {
					msg.WriteLong(Math.Max(this.serverMessageSequence - (pingAdjust / this.messageIntervalAverage),0));
				}
				msg.WriteLong(this.serverCommandSequence);
				int reliableCount = 0;
				for (int i = this.reliableAcknowledge + 1; i <= this.reliableSequence; i++) {
					msg.WriteByte((int)ClientCommandOperations.ClientCommand);
					msg.WriteLong(i);
					msg.WriteString(this.reliableCommands[i & (this.MaxReliableCommands-1)],(ProtocolVersion)this.Protocol);
					reliableCount++;
				}
				
				// Actually new messages since last actually sent command
				int realOldPacketNum = (this.netChannel.OutgoingSequence - 1) & JKClient.PacketMask;
				int realCount = this.cmdNumber - this.outPackets[realOldPacketNum].CommandNumber;
				int realTimeSinceLastPacket = this.realTime - this.outPackets[realOldPacketNum].RealTime;
				int reliableSeqDiffSinceLastPacket = this.reliableSequence - this.outPackets[realOldPacketNum].ReliableSequence;

				int oldPacketNum = (this.netChannel.OutgoingSequence - 1 - 1) & JKClient.PacketMask; // With packetdup default of 1 assumed.
				int count = this.cmdNumber - this.outPackets[oldPacketNum].CommandNumber;
				if (count > JKClient.MaxPacketUserCmds) {
					count = JKClient.MaxPacketUserCmds;
				}
				if (count >= 1) {
					if (!this.snap.Valid || this.serverMessageSequence != this.snap.MessageNum || Demowaiting == 2 || pingAdjust != 0) {
						msg.WriteByte((int)ClientCommandOperations.MoveNoDelta);
					} else {
						msg.WriteByte((int)ClientCommandOperations.Move);
					}
					msg.WriteByte(count);

                    if (isMOH)
                    { // MOH sends eye info here. Let's just pretend it's never changing and send the simplified "no change".
						msg.WriteBits(0, 1);
                    }

					int key = this.checksumFeed;
					key ^= this.serverMessageSequence;
					key ^= Common.HashKey(this.serverCommands[this.serverCommandSequence & (this.MaxReliableCommands-1)], 32);
					for (int i = 0; i < count; i++) {
						int j = (this.cmdNumber - count + i + 1) & UserCommand.CommandMask;
						msg.WriteDeltaUsercmdKey(key, ref oldcmd, ref this.cmds[j],isMOH);
						oldcmd = this.cmds[j];
					}
				}

				if (this.TrafficReduceUntilClientFps > 0 && realTimeSinceLastPacket > 0 && (reliableCount == 0 || reliableSeqDiffSinceLastPacket == 0) && realCount == 0 && realTimeSinceLastPacket < (1000 / this.TrafficReduceUntilClientFps))
				{
					// Reduce network traffic.
					// We have no new commands to sent:
					// - No new usercmds
					// - No new reliable commands that weren't already sent (or all already acknowledged)
					// and last packet was sent X milliseconds ago where X is smaller than the millisecond value of the minimum client fps we want.
					// Don't wanna be seen as ddosing people but sometimes we do need a high fps (like if we are doing actual gameplay)
					this.Stats.userPacketCulled(true);
					return;
				}
                else
                {
					this.Stats.userPacketCulled(false);
				}

				int packetNum = this.netChannel.OutgoingSequence & JKClient.PacketMask;
				this.outPackets[packetNum].RealTime = this.realTime;
				this.outPackets[packetNum].ServerTime = oldcmd.ServerTime;
				this.outPackets[packetNum].CommandNumber = this.cmdNumber;
				this.outPackets[packetNum].ReliableSequence = this.reliableSequence;
				msg.WriteByte((int)ClientCommandOperations.EOF);
				this.Encode(msg);
                if (!this.GhostPeer) // As a ghost peer we want to get stuck in CON_CONNECTING forever. Hack for rare circumstances
                {
					this.netChannel.Transmit(msg.CurSize, msg.Data);
					while (this.netChannel.UnsentFragments)
					{
						this.netChannel.TransmitNextFragment();
					}
				}
			}
		}


        private unsafe void AddReliableCommand(string cmd, bool disconnect = false, Encoding encoding = null) {
			int unacknowledged = this.reliableSequence - this.reliableAcknowledge;
			fixed (sbyte *reliableCommand = this.reliableCommands[++this.reliableSequence & (this.MaxReliableCommands-1)]) {
				encoding = encoding ?? Common.Encoding;
				Marshal.Copy(encoding.GetBytes(cmd+'\0'), 0, (IntPtr)(reliableCommand), encoding.GetByteCount(cmd)+1);
			}
			this.Stats.lastCommand = cmd;
		}
		public int GetUnacknowledgedReliableCommandCount()
        {
			return this.reliableSequence - this.reliableAcknowledge;

		}

		protected void ExecuteCommandInternal(string cmd, Encoding encoding = null) {

            if (OnInternalCommandCreated(cmd, encoding)) // Returns true if we have to send it ourselves (event handlers can decide to handle it on their own, e.g. to integrate it with some command flood protection system)
			{
				ExecuteCommand(cmd, encoding);
			}
		}
		public void ExecuteCommand(string cmd, Encoding encoding = null) {
			void executeCommand() {
				if (cmd.StartsWith("rcon ", StringComparison.OrdinalIgnoreCase)) {
					this.ExecuteCommandDirectly(cmd, encoding);
				} else {
					this.AddReliableCommand(cmd, encoding: encoding);
				}
			}
			this.actionsQueue.Enqueue(executeCommand);
		}
		private void ExecuteCommandDirectly(string cmd, Encoding encoding) {
			this.OutOfBandPrint(this.serverAddress, cmd);
		}
		public Task Connect(in ServerInfo serverInfo) {
			if (serverInfo == null) {
				throw new JKClientException(new ArgumentNullException(nameof(serverInfo)));
			}
			return this.Connect(serverInfo.Address.ToString());
		}
		public async Task Connect(string address) {
			this.connectTCS?.TrySetCanceled();
			this.connectTCS = null;
			var serverAddress = await NetSystem.StringToAddressAsync(address);
			if (serverAddress == null) {
				throw new JKClientException("Bad server address");
			}
			this.connectTCS = new TaskCompletionSource<bool>();
			void connect() {
				this.servername = address;
				this.serverAddress = serverAddress;
				this.challenge = ((random.Next() << 16) ^ random.Next()) ^ (int)Common.Milliseconds;
				this.connectTime = -9999;
				this.mohConnectTimeExtraDelay = 200;
				this.infoRequestTime = -9999;
				this.connectPacketCount = 0;
				this.Status = ConnectionStatus.Connecting;
			}
			this.actionsQueue.Enqueue(connect);
			await this.connectTCS.Task;
		}
		public void Disconnect() {
			var status = this.Status;
			this.Status = ConnectionStatus.Disconnected;
			void disconnect() {
				this.StopRecord_f();
				this.connectTCS?.TrySetCanceled();
				this.connectTCS = null;
				if (status >= ConnectionStatus.Connected) {
					this.AddReliableCommand("disconnect", true);
					this.WritePacket();
					this.WritePacket();
					this.WritePacket();
				}
				this.ClearState();
				this.ClearConnection();
				OnDisconnected(EventArgs.Empty);
			}
			this.actionsQueue.Enqueue(disconnect);
		}
		public static IClientHandler GetKnownClientHandler(in ServerInfo serverInfo) {
			if (serverInfo == null) {
				throw new JKClientException(new ArgumentNullException(nameof(serverInfo)));
			}
			return JKClient.GetKnownClientHandler(serverInfo.Protocol, serverInfo.Version);
		}
		public static IClientHandler GetKnownClientHandler(ProtocolVersion protocol, ClientVersion version) {
			switch (protocol) {
			case ProtocolVersion.Protocol6:
			case ProtocolVersion.Protocol7:
			case ProtocolVersion.Protocol8:
			case ProtocolVersion.Protocol17:
				return new MOHClientHandler(protocol, version);
			case ProtocolVersion.Protocol25:
			case ProtocolVersion.Protocol26:
				return new JAClientHandler(protocol, version);
			case ProtocolVersion.Protocol15:
			case ProtocolVersion.Protocol16:
				return new JOClientHandler(protocol, version);
			case ProtocolVersion.Protocol68:
			case ProtocolVersion.Protocol71:
				return new Q3ClientHandler(protocol);
			}
			throw new JKClientException($"There isn't any known client handler for given protocol: {protocol}");
		}


		/*
		====================
		WriteDemoMessage

		Dumps the current net message, prefixed by the length
		====================
		*/
		void WriteDemoMessage(Message msg, int headerBytes,int sequenceNumber, int? serverTime)
		{
			int len, swlen;

            lock (DemofileLock) {
				if (!Demorecording)
				{
					//Com_Printf("Not recording a demo.\n");
					return;
				}

				// write the packet sequence
				//len = serverMessageSequence;
				len = sequenceNumber;
				Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));

				// skip the packet sequencing information
				len = msg.CurSize - headerBytes;
				Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));
				Demofile.Write(msg.Data, headerBytes, len);


				this.currentDemoWrittenSequenceNumber = sequenceNumber;
				this.currentDemoMaxSequenceNumber = currentDemoMaxSequenceNumber.HasValue ? Math.Max(sequenceNumber, this.currentDemoMaxSequenceNumber.Value) : sequenceNumber;

				if (serverTime.HasValue) // This messsage contains a snapshot. Update the server time of actually written messages (we write delayed for reordering)
                {
					currentDemoWrittenServerTime = serverTime.Value;
					this.UpdateDemoTime();
				}

				if (demoFirstPacketRecordedPromise != null)
				{
					demoFirstPacketRecordedPromise.SetResult(true); // Just in case the outside code wants to do something particular once actual packets are being recorded.
					demoFirstPacketRecordedPromise = null;
				}
                if ((DemoLastFullFlush + DemoFlushInterval) < Demofile.Position || (DateTime.Now-DemoLastFullFlushTime).TotalMilliseconds > DemoFlushTimeInterval)
                {
					Demofile.Flush(true);
					DemoLastFullFlush = Demofile.Position;
					DemoLastFullFlushTime = DateTime.Now;
					Stats.demoSizeFullFlushed = DemoLastFullFlush;
				}
				Stats.demoSize = Demofile.Position;
			}
		}

		/*
		====================
		WriteBufferedDemoMessages
		Writes messages from the buffered demo packets map into the demo if they are either 
		follow ups to a previously written messages without a gap or if they are at least the timeout age.
		If called with qtrue parameter, timeout will be ignored and all messages will be flushed and written
		into the demo file.
		====================
		*/
		void WriteBufferedDemoMessages(bool forceWriteAll = false)
		{
            lock (bufferedDemoMessages) { 
				//static msg_t tmpMsg;
				//static byte tmpMsgData[MAX_MSGLEN];
				//tmpMsg.data = tmpMsgData;

				// First write messages that exist without a gap.
				//while (bufferedDemoMessages.find(clc.demoLastWrittenSequenceNumber + 1) != bufferedDemoMessages.end())
				while (bufferedDemoMessages.ContainsKey(DemoLastWrittenSequenceNumber + 1))
				{
					// While we have all the messages without any gaps, we can just dump them all into the demo file.
					Message tmpMsg = bufferedDemoMessages[DemoLastWrittenSequenceNumber + 1].msg;
					WriteDemoMessage(tmpMsg, tmpMsg.ReadCount, DemoLastWrittenSequenceNumber + 1, bufferedDemoMessages[DemoLastWrittenSequenceNumber + 1].serverTime);
					DemoLastWrittenSequenceNumber = DemoLastWrittenSequenceNumber + 1;
					bufferedDemoMessages.Remove(DemoLastWrittenSequenceNumber);
				}

				// Now write messages that are older than the timeout. Also do a bit of cleanup while we're at it.
				// bufferedDemoMessages is a map and maps are ordered, so the key (sequence number) should be incrementing.
				List<int> itemsToErase = new List<int>();
				foreach (KeyValuePair<int,BufferedDemoMessageContainer> tmpMsg in bufferedDemoMessages)
				{
					if (tmpMsg.Key <= DemoLastWrittenSequenceNumber)
					{ // Older or identical number to stuff we already wrote. Discard.
						itemsToErase.Add(tmpMsg.Key);
						continue;
					}
					// First potential candidate.
					//if (forceWriteAll || tmpIt->second.time + cl_demoRecordBufferedReorderTimeout->integer < Com_RealTime(NULL)) {
					if (forceWriteAll || ((DateTime.Now - tmpMsg.Value.time).TotalSeconds) > DemoRecordBufferedReorderTimeout)
					{
						WriteDemoMessage(tmpMsg.Value.msg, tmpMsg.Value.msg.ReadCount, tmpMsg.Key, tmpMsg.Value.serverTime);
						DemoLastWrittenSequenceNumber = tmpMsg.Key;
						itemsToErase.Add(tmpMsg.Key);
					}
					else
					{
						// Not old enough. When there are gaps we want to wait X amount of seconds before writing a new
						// message so that older ones can still arrive.
						break; // Since the messages in the map are ordered, if we're not writing this one, no need to continue.
					}
				}
				foreach(int itemToErase in itemsToErase)
				{
					bufferedDemoMessages.Remove(itemToErase);
				}
			}
		}

		/*
		====================
		StopRecording_f

		stop recording a demo
		====================
		*/
		public void StopRecord_f()
		{
			int len;


			WriteBufferedDemoMessages(true); // Flush all messages into the demo file.

			lock (DemofileLock) {


				if (!Demorecording)
				{
					//Com_Printf("Not recording a demo.\n");
					return;
				}

				// finish up
				len = -1;
				Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));
				Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));
				Demofile.Flush(true);
				DemoLastFullFlush = Demofile.Position;
				Demofile.Close();
				Demofile.Dispose();
				Demofile = null;
				DemoLastFullFlush = 0;
				DemoLastFullFlushTime = DateTime.Now;
				Demorecording = false;
				AbsoluteDemoName = null;
				DemoName = null;
				Demowaiting = 0;
				//Com_Printf("Stopped demo.\n");
				Stats.demoSize = 0;
				Stats.demoSizeFullFlushed = DemoLastFullFlush;

				UpdateDemoTime();
			}
		}


		/*
		==================
		DemoFilename
		==================
		*/
		DemoName_t DemoFilename()
		{
			DateTime now = DateTime.Now;
			return new DemoName_t { name = "demo" + now.ToString("yyyy-MM-dd_HH-mm-ss"), time=now };
		}

		// firstPacketRecordedTCS in case you want to do anything once the first packet has recorded, like send some command that you want the response recorded of
		public async Task<bool> Record_f(DemoName_t demoName,TaskCompletionSource<bool> firstPacketRecordedTCS = null)
        {
			if(demoRecordingStartPromise != null)
            {
				firstPacketRecordedTCS.TrySetResult(false);
				return false;
            }

			DemoName = demoName;

			demoRecordingStartPromise = new TaskCompletionSource<bool>();
			if(firstPacketRecordedTCS != null)
            {
				demoFirstPacketRecordedPromise = firstPacketRecordedTCS;
			}

			actionsQueue.Enqueue(()=> {
				demoRecordingStartPromise.TrySetResult(StartRecording(DemoName));
				demoRecordingStartPromise = null;
			});

			return await demoRecordingStartPromise.Task;
		}

		public DemoName_t getDemoName()
        {
			return DemoName;
        }

		Message constructMetaMessage()
        {
			bool isMOH = this.ClientHandler is MOHClientHandler;
			byte[] data = new byte[ClientHandler.MaxMessageLength];
			var msg = new Message(data, sizeof(byte) * ClientHandler.MaxMessageLength);
			msg.ErrorMessageCreated += Msg_ErrorMessageCreated;
			msg.Bitstream(); 
			msg.WriteLong(reliableSequence);
			StringBuilder sb = new StringBuilder();
			sb.Append("{"); // original filename
			//sb.Append("\"of\":\""); // original filename // Maybe don't do this as someone might use this to record demos and then rename, then it doesn't count as original filename anymore? And we have time after all.
			//sb.Append(this.AbsoluteDemoName);
			//sb.Append("\","); // original start time
			sb.Append("\"wr\":\""); // writer
			sb.Append("jkclient_demoRec"); // jkClient_demoRec
			sb.Append("\","); // original start time
			sb.Append("\"ost\":"); // original start time
			sb.Append(((DateTimeOffset)this.DemoName.time.ToUniversalTime()).ToUnixTimeSeconds());
			if(this.serverAddress != null)
            {
				sb.Append(",\"oip\":\""); // original IP
				sb.Append(this.serverAddress.ToString());
			}
            lock (extraDemoMeta) { 
				foreach(var kvp in extraDemoMeta)
				{
					if(kvp.Value != null && kvp.Key != null)
					{
						sb.Append($"\",\"{HttpUtility.JavaScriptStringEncode(kvp.Key)}\":\"{HttpUtility.JavaScriptStringEncode(kvp.Value)}");
					}
				}
			}
			sb.Append("\"}");
			Debug.WriteLine($"JKClient demo meta: {sb.ToString()}");
			string metaData = sb.ToString();
			int eofOperation = ClientHandler is JOClientHandler ? (int)ServerCommandOperations.EOF - 1 : (int)ServerCommandOperations.EOF;
			if (isMOH)
			{
				eofOperation = 11;
			}
			HiddenMetaStuff.createMetaMessage(msg, metaData, eofOperation);
			return msg;
		}


		static Mutex demoUniqueFilenameMutex = new Mutex();

		// Demo recording
		private unsafe bool StartRecording(DemoName_t demoName,bool timeStampDemoname=false)
        {

			bool isMOH = this.ClientHandler is MOHClientHandler;

			if (Demorecording)
			{
				return false;
			}

			if (Status != ConnectionStatus.Active)
			{
				//Com_Printf("You must be in a level to record.\n");
				return false;
			}


            if (timeStampDemoname)
            {
				demoName = DemoFilename();
            }

            lock (demoUniqueFilenameMutex) { // Make sure we don't accidentally try writing to an identical filename twice from different instances of the client. It can happen when the timing is really tight on a reconnect and then you get a serious error thrown your way.

				string demoExtension = isMOH ? ".dm3" :( ".dm_" + ((int)Protocol).ToString());

				string name = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"JKWatcher", "demos/" + demoName + demoExtension);
				int filenameIncrement = 2;
				while (File.Exists(name))
				{
					name = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "demos/" + demoName + $" ({filenameIncrement++})" + demoExtension);

					//Com_Printf("Record: Couldn't create a file\n");
					//return false;
				}

				lock (DemofileLock) {

					// open the demo file
					//Com_Printf("recording to %s.\n", name);
					Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "demos"));
					DemoLastFullFlush = 0;
					DemoLastFullFlushTime = DateTime.Now;
					Demofile = new FileStream(name,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
					/*if (!Demofile)
					{
						Com_Printf("ERROR: couldn't open.\n");
						return;
					}*/
					Demorecording = true;

					this.AbsoluteDemoName = name;
					this.DemoName = demoName;

					Demowaiting = 2; // request non-delta message with value 2.
					 //DemoSkipPacket = false;
					DemoLastWrittenSequenceNumber = 0;

					int len;

					// Metadata
					Message metaMsg = constructMetaMessage();
					len = this.serverMessageSequence - 2;
					Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));
					len = metaMsg.CurSize;
					Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));
					Demofile.Write(metaMsg.Data, 0, metaMsg.CurSize);

					//byte[] data = new byte[Message.MaxLength];
					byte[] data = new byte[ClientHandler.MaxMessageLength];

					// write out the gamestate message
					var msg = new Message(data, sizeof(byte) * ClientHandler.MaxMessageLength);
					msg.ErrorMessageCreated += Msg_ErrorMessageCreated;

					msg.Bitstream();

					// NOTE, MRE: all server->client messages now acknowledge
					msg.WriteLong(reliableSequence);

					msg.WriteByte((int)ServerCommandOperations.Gamestate);
					msg.WriteLong(serverCommandSequence);


					// configstrings
					for (int i = 0; i < ClientHandler.MaxConfigstrings; i++)
					{
						if (0 == gameState.StringOffsets[i])
						{
							continue;
						}
						fixed (sbyte* s = this.gameState.StringData)
						{
							sbyte* cs = s + gameState.StringOffsets[i];
							msg.WriteByte((int)ServerCommandOperations.Configstring);
							msg.WriteShort(i);
							len = Common.StrLen(cs);
							byte[] bytes = new byte[len+1];
							Marshal.Copy((IntPtr)cs, bytes, 0, len+1);
							msg.WriteBigString((sbyte[])(Array)bytes,(ProtocolVersion)this.Protocol);
						}
					}

					// baselines
					EntityState nullstate;
                    if (isMOH)
                    {
						nullstate = Message.GetNullEntityState();
                    }
					for (int i = 0; i < Common.MaxGEntities; i++)
					{

						fixed(EntityState* ent = &entityBaselines[i])
						{
							if (0 == ent->Number)
							{
								continue;
							}
							msg.WriteByte((int)ServerCommandOperations.Baseline);
							msg.WriteDeltaEntity(&nullstate, ent, true,this.Version,this.ClientHandler,this.serverFrameTime);
						}
					}

					int eofOperation = ClientHandler is JOClientHandler ? (int)ServerCommandOperations.EOF -1 : (int)ServerCommandOperations.EOF;
                    if (isMOH)
                    {
						eofOperation = 11;
					}
					msg.WriteByte(eofOperation);

					// finished writing the gamestate stuff

					// write the client num
					msg.WriteLong(this.clientNum);
					// write the checksum feed
					msg.WriteLong(this.checksumFeed);

                    if (isMOH && this.Protocol > (int)ProtocolVersion.Protocol8)
                    {
						msg.WriteFloat(this.serverFrameTime); // MOHAA expansion packs are weird. Weirder than MOHAA.
                    }

                    if (this.ClientHandler is JAClientHandler) // RMG nonsense. Take the easy way for now. Maybe do nicer someday.
					{
						/*// RMG stuff
						if (clc.rmgHeightMapSize)
						{
							int i;

							// Height map
							MSG_WriteShort(&buf, (unsigned short)clc.rmgHeightMapSize );
							MSG_WriteBits(&buf, 0, 1);
							MSG_WriteData(&buf, clc.rmgHeightMap, clc.rmgHeightMapSize);

							// Flatten map
							MSG_WriteShort(&buf, (unsigned short)clc.rmgHeightMapSize );
							MSG_WriteBits(&buf, 0, 1);
							MSG_WriteData(&buf, clc.rmgFlattenMap, clc.rmgHeightMapSize);

							// Seed 
							MSG_WriteLong(&buf, clc.rmgSeed);

							// Automap symbols
							MSG_WriteShort(&buf, (unsigned short)clc.rmgAutomapSymbolCount );
							for (i = 0; i < clc.rmgAutomapSymbolCount; i++)
							{
								MSG_WriteByte(&buf, (unsigned char)clc.rmgAutomapSymbols[i].mType );
								MSG_WriteByte(&buf, (unsigned char)clc.rmgAutomapSymbols[i].mSide );
								MSG_WriteLong(&buf, (long)clc.rmgAutomapSymbols[i].mOrigin[0]);
								MSG_WriteLong(&buf, (long)clc.rmgAutomapSymbols[i].mOrigin[1]);
							}
						}
						else*/
						{

							msg.WriteShort(0);
						}
					}

					// finished writing the client packet
					msg.WriteByte(eofOperation);

					// write it to the demo file
					len = this.serverMessageSequence - 1;

					Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));

					len = msg.CurSize;
					Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));
					Demofile.Write(msg.Data, 0, msg.CurSize);

					// the rest of the demo file will be copied from net messages

					Demofile.Flush(true);
					DemoLastFullFlush = Demofile.Position;
					DemoLastFullFlushTime = DateTime.Now;
					Stats.demoSize = Demofile.Position;
					Stats.demoSizeFullFlushed = DemoLastFullFlush;

					return true;

				}
			}

		}

		
	}
}
