﻿using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient {
	public enum PlayerMoveType : int {
		Normal,
		Jetpack,
		Float,
		Noclip,
		Spectator,
		Dead,
		Freeze,
		Intermission,
		SinglePlayerIntermission
	}
}
