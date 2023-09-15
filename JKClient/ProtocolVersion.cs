namespace JKClient {
	public enum ProtocolVersion {
		Unknown = 0,
		// MOHAA (later MOH parts conflict with 15 ...)
		Protocol6 = 6,
		Protocol7 = 7,
		Protocol8 = 8,
		//JO v1.02, v1.03 and MOHAA expansions (not same protocol, just same number)
		Protocol15 = 15,
		//JO v1.04 and MOHAA expansions (not same protocol, just same number)
		Protocol16 = 16,
		// MOHAA expansions
		Protocol17 = 17, 
		//JA v1.00
		Protocol25 = 25,
		//JA v1.01
		Protocol26 = 26,
		//Q3 v1.32
		Protocol68 = 68,
		//Q3 v1.32
		Protocol71 = 71
	}
}
