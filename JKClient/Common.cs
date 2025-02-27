﻿using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace JKClient {
	public static class Common {
		internal const int MaxStringChars = 1024;
		internal const int MaxStringCharsMOH = 2048; // MOH
		internal const int BigInfoString = 8192;

		// TODO: These are JK specific? 
		public const int MaxClientScoreSend = 20;
		public const int MaxStats = 16;
		public const int MaxPersistant = 16;
		public const int MaxPowerUps = 16;
		public const int MaxWeapons = 16;

		internal const int GEntitynumBits = 10;
		public const int MaxGEntities = (1<<Common.GEntitynumBits);
		internal const int GibHealth = -40;
		public const string EscapeCharacter = "\u0019";
		internal static long Milliseconds => (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond);
#if NETSTANDARD2_1
		private static Action<IntPtr, byte, int> memSetDelegate;
#endif
		public static Encoding Encoding { get; set; }
		public static bool AllowAllEncodingCharacters { get; set; } = false;
		static Common() {
			Common.Encoding = Encoding.GetEncoding("windows-1252");
#if NETSTANDARD2_1
			var memSetILMethod = new DynamicMethod(
				"MemSetIL",
				MethodAttributes.Assembly | MethodAttributes.Static, CallingConventions.Standard,
				null,
				new []{
					typeof(IntPtr),
					typeof(byte),
					typeof(int)
				},
				typeof(Common),
				true
			);
			var generator = memSetILMethod.GetILGenerator();
			generator.Emit(OpCodes.Ldarg_0);
			generator.Emit(OpCodes.Ldarg_1);
			generator.Emit(OpCodes.Ldarg_2);
			generator.Emit(OpCodes.Initblk);
			generator.Emit(OpCodes.Ret);
			memSetDelegate = (Action<IntPtr, byte, int>)memSetILMethod.CreateDelegate(typeof(Action<IntPtr, byte, int>));
#endif
		}
		internal static void MemSet(object dst, byte val, int size) {
			var gcHandle = GCHandle.Alloc(dst, GCHandleType.Pinned);
			Common.MemSet(gcHandle.AddrOfPinnedObject(), val, size);
			gcHandle.Free();
		}
		internal static unsafe void VectorCopy(float* src, float* dst)
        {
			dst[0] = src[0];
			dst[1] = src[1];
			dst[2] = src[2];
        }

		public static bool ProtocolIsMOH(ProtocolVersion protocol)
        {
			return protocol >= ProtocolVersion.Protocol6 && protocol <= ProtocolVersion.Protocol8 || protocol==ProtocolVersion.Protocol17; // TODO Support 15 and 16 too?
		}

		const int NUMVERTEXNORMALS = 162;
		static float[] vec3_origin = new float[3] { 0,0,0}; // Not sure how else to do this
		static float[][] bytedirs = new float[][] 
		{
		new float[]{-0.525731f, 0.000000f, 0.850651f}, new float[]{-0.442863f, 0.238856f, 0.864188f},
		new float[]{-0.295242f, 0.000000f, 0.955423f}, new float[]{-0.309017f, 0.500000f, 0.809017f},
		new float[]{-0.162460f, 0.262866f, 0.951056f}, new float[]{0.000000f, 0.000000f, 1.000000f},
		new float[]{0.000000f, 0.850651f, 0.525731f}, new float[]{-0.147621f, 0.716567f, 0.681718f},
		new float[]{0.147621f, 0.716567f, 0.681718f}, new float[]{0.000000f, 0.525731f, 0.850651f},
		new float[]{0.309017f, 0.500000f, 0.809017f}, new float[]{0.525731f, 0.000000f, 0.850651f},
		new float[]{0.295242f, 0.000000f, 0.955423f}, new float[]{0.442863f, 0.238856f, 0.864188f},
		new float[]{0.162460f, 0.262866f, 0.951056f}, new float[]{-0.681718f, 0.147621f, 0.716567f},
		new float[]{-0.809017f, 0.309017f, 0.500000f},new float[]{-0.587785f, 0.425325f, 0.688191f},
		new float[]{-0.850651f, 0.525731f, 0.000000f},new float[]{-0.864188f, 0.442863f, 0.238856f},
		new float[]{-0.716567f, 0.681718f, 0.147621f},new float[]{-0.688191f, 0.587785f, 0.425325f},
		new float[]{-0.500000f, 0.809017f, 0.309017f}, new float[]{-0.238856f, 0.864188f, 0.442863f},
		new float[]{-0.425325f, 0.688191f, 0.587785f}, new float[]{-0.716567f, 0.681718f, -0.147621f},
		new float[]{-0.500000f, 0.809017f, -0.309017f}, new float[]{-0.525731f, 0.850651f, 0.000000f},
		new float[]{0.000000f, 0.850651f, -0.525731f}, new float[]{-0.238856f, 0.864188f, -0.442863f},
		new float[]{0.000000f, 0.955423f, -0.295242f}, new float[]{-0.262866f, 0.951056f, -0.162460f},
		new float[]{0.000000f, 1.000000f, 0.000000f}, new float[]{0.000000f, 0.955423f, 0.295242f},
		new float[]{-0.262866f, 0.951056f, 0.162460f}, new float[]{0.238856f, 0.864188f, 0.442863f},
		new float[]{0.262866f, 0.951056f, 0.162460f}, new float[]{0.500000f, 0.809017f, 0.309017f},
		new float[]{0.238856f, 0.864188f, -0.442863f},new float[]{0.262866f, 0.951056f, -0.162460f},
		new float[]{0.500000f, 0.809017f, -0.309017f},new float[]{0.850651f, 0.525731f, 0.000000f},
		new float[]{0.716567f, 0.681718f, 0.147621f}, new float[]{0.716567f, 0.681718f, -0.147621f},
		new float[]{0.525731f, 0.850651f, 0.000000f}, new float[]{0.425325f, 0.688191f, 0.587785f},
		new float[]{0.864188f, 0.442863f, 0.238856f}, new float[]{0.688191f, 0.587785f, 0.425325f},
		new float[]{0.809017f, 0.309017f, 0.500000f}, new float[]{0.681718f, 0.147621f, 0.716567f},
		new float[]{0.587785f, 0.425325f, 0.688191f}, new float[]{0.955423f, 0.295242f, 0.000000f},
		new float[]{1.000000f, 0.000000f, 0.000000f}, new float[]{0.951056f, 0.162460f, 0.262866f},
		new float[]{0.850651f, -0.525731f, 0.000000f},new float[]{0.955423f, -0.295242f, 0.000000f},
		new float[]{0.864188f, -0.442863f, 0.238856f}, new float[]{0.951056f, -0.162460f, 0.262866f},
		new float[]{0.809017f, -0.309017f, 0.500000f}, new float[]{0.681718f, -0.147621f, 0.716567f},
		new float[]{0.850651f, 0.000000f, 0.525731f}, new float[]{0.864188f, 0.442863f, -0.238856f},
		new float[]{0.809017f, 0.309017f, -0.500000f}, new float[]{0.951056f, 0.162460f, -0.262866f},
		new float[]{0.525731f, 0.000000f, -0.850651f}, new float[]{0.681718f, 0.147621f, -0.716567f},
		new float[]{0.681718f, -0.147621f, -0.716567f},new float[]{0.850651f, 0.000000f, -0.525731f},
		new float[]{0.809017f, -0.309017f, -0.500000f}, new float[]{0.864188f, -0.442863f, -0.238856f},
		new float[]{0.951056f, -0.162460f, -0.262866f}, new float[]{0.147621f, 0.716567f, -0.681718f},
		new float[]{0.309017f, 0.500000f, -0.809017f}, new float[]{0.425325f, 0.688191f, -0.587785f},
		new float[]{0.442863f, 0.238856f, -0.864188f}, new float[]{0.587785f, 0.425325f, -0.688191f},
		new float[]{0.688191f, 0.587785f, -0.425325f}, new float[]{-0.147621f, 0.716567f, -0.681718f},
		new float[]{-0.309017f, 0.500000f, -0.809017f}, new float[]{0.000000f, 0.525731f, -0.850651f},
		new float[]{-0.525731f, 0.000000f, -0.850651f}, new float[]{-0.442863f, 0.238856f, -0.864188f},
		new float[]{-0.295242f, 0.000000f, -0.955423f}, new float[]{-0.162460f, 0.262866f, -0.951056f},
		new float[]{0.000000f, 0.000000f, -1.000000f}, new float[]{0.295242f, 0.000000f, -0.955423f},
		new float[]{0.162460f, 0.262866f, -0.951056f}, new float[]{-0.442863f, -0.238856f, -0.864188f},
		new float[]{-0.309017f, -0.500000f, -0.809017f}, new float[]{-0.162460f, -0.262866f, -0.951056f},
		new float[]{0.000000f, -0.850651f, -0.525731f}, new float[]{-0.147621f, -0.716567f, -0.681718f},
		new float[]{0.147621f, -0.716567f, -0.681718f}, new float[]{0.000000f, -0.525731f, -0.850651f},
		new float[]{0.309017f, -0.500000f, -0.809017f}, new float[]{0.442863f, -0.238856f, -0.864188f},
		new float[]{0.162460f, -0.262866f, -0.951056f}, new float[]{0.238856f, -0.864188f, -0.442863f},
		new float[]{0.500000f, -0.809017f, -0.309017f}, new float[]{0.425325f, -0.688191f, -0.587785f},
		new float[]{0.716567f, -0.681718f, -0.147621f}, new float[]{0.688191f, -0.587785f, -0.425325f},
		new float[]{0.587785f, -0.425325f, -0.688191f}, new float[]{0.000000f, -0.955423f, -0.295242f},
		new float[]{0.000000f, -1.000000f, 0.000000f}, new float[]{0.262866f, -0.951056f, -0.162460f},
		new float[]{0.000000f, -0.850651f, 0.525731f}, new float[]{0.000000f, -0.955423f, 0.295242f},
		new float[]{0.238856f, -0.864188f, 0.442863f}, new float[]{0.262866f, -0.951056f, 0.162460f},
		new float[]{0.500000f, -0.809017f, 0.309017f}, new float[]{0.716567f, -0.681718f, 0.147621f},
		new float[]{0.525731f, -0.850651f, 0.000000f}, new float[]{-0.238856f, -0.864188f, -0.442863f},
		new float[]{-0.500000f, -0.809017f, -0.309017f}, new float[]{-0.262866f, -0.951056f, -0.162460f},
		new float[]{-0.850651f, -0.525731f, 0.000000f}, new float[]{-0.716567f, -0.681718f, -0.147621f},
		new float[]{-0.716567f, -0.681718f, 0.147621f}, new float[]{-0.525731f, -0.850651f, 0.000000f},
		new float[]{-0.500000f, -0.809017f, 0.309017f}, new float[]{-0.238856f, -0.864188f, 0.442863f},
		new float[]{-0.262866f, -0.951056f, 0.162460f}, new float[]{-0.864188f, -0.442863f, 0.238856f},
		new float[]{-0.809017f, -0.309017f, 0.500000f}, new float[]{-0.688191f, -0.587785f, 0.425325f},
		new float[]{-0.681718f, -0.147621f, 0.716567f}, new float[]{-0.442863f, -0.238856f, 0.864188f},
		new float[]{-0.587785f, -0.425325f, 0.688191f}, new float[]{-0.309017f, -0.500000f, 0.809017f},
		new float[]{-0.147621f, -0.716567f, 0.681718f}, new float[]{-0.425325f, -0.688191f, 0.587785f},
		new float[]{-0.162460f, -0.262866f, 0.951056f}, new float[]{0.442863f, -0.238856f, 0.864188f},
		new float[]{0.162460f, -0.262866f, 0.951056f}, new float[]{0.309017f, -0.500000f, 0.809017f},
		new float[]{0.147621f, -0.716567f, 0.681718f}, new float[]{0.000000f, -0.525731f, 0.850651f},
		new float[]{0.425325f, -0.688191f, 0.587785f}, new float[]{0.587785f, -0.425325f, 0.688191f},
		new float[]{0.688191f, -0.587785f, 0.425325f}, new float[]{-0.955423f, 0.295242f, 0.000000f},
		new float[]{-0.951056f, 0.162460f, 0.262866f}, new float[]{-1.000000f, 0.000000f, 0.000000f},
		new float[]{-0.850651f, 0.000000f, 0.525731f}, new float[]{-0.955423f, -0.295242f, 0.000000f},
		new float[]{-0.951056f, -0.162460f, 0.262866f}, new float[]{-0.864188f, 0.442863f, -0.238856f},
		new float[]{-0.951056f, 0.162460f, -0.262866f}, new float[]{-0.809017f, 0.309017f, -0.500000f},
		new float[]{-0.864188f, -0.442863f, -0.238856f}, new float[]{-0.951056f, -0.162460f, -0.262866f},
		new float[]{-0.809017f, -0.309017f, -0.500000f}, new float[]{-0.681718f, 0.147621f, -0.716567f},
		new float[]{-0.681718f, -0.147621f, -0.716567f}, new float[]{-0.850651f, 0.000000f, -0.525731f},
		new float[]{-0.688191f, 0.587785f, -0.425325f}, new float[]{-0.587785f, 0.425325f, -0.688191f},
		new float[]{-0.425325f, 0.688191f, -0.587785f}, new float[]{-0.425325f, -0.688191f, -0.587785f},
		new float[]{-0.587785f, -0.425325f, -0.688191f}, new float[]{-0.688191f, -0.587785f, -0.425325f}
		};
		internal static unsafe void ByteToDir(int b, float* dir)
		{
			if (b < 0 || b >= NUMVERTEXNORMALS)
			{
				fixed(float* p = vec3_origin)
                {
					VectorCopy(p, dir);
				}
				return;
			}
            fixed (float* p = bytedirs[b])
			{
				VectorCopy(p, dir);
			}
		}
		internal static unsafe void MemSet(void *dst, byte val, int size) {
			Common.MemSet((IntPtr)dst, val, size);
		}
		internal static unsafe void MemSet(IntPtr dst, byte val, int size) {
#if NETSTANDARD2_1
			memSetDelegate(dst, val, size);
#else
			byte *dstp = (byte *)dst;
			for (int i = 0; i < size; i++) {
				dstp[i] = val;
			}
#endif
		}
		internal static unsafe int StrLen(sbyte *str) {
			sbyte* s;
			for (s = str; *s != 0; s++);
			return (int)(s - str);
		}
		internal static unsafe int StrLen(sbyte []str) {
			fixed (sbyte *s = str) {
				return Common.StrLen(s);
			}
		}
		internal static unsafe int StrCmp(sbyte *s1, sbyte *s2, int n = 99999) {
			if (s1 == null) {
				if (s2 == null) {
					return 0;
				} else {
					return -1;
				}
			} else if (s2 == null) {
				return 1;
			}
			int c1, c2;
			do {
				c1 = *s1++;
				c2 = *s2++;
				if ((n--) == 0) {
					return 0;
				}
				if (c1 != c2) {
					return c1 < c2 ? -1 : 1;
				}
			} while (c1 != 0);
			return 0;
		}
		internal static unsafe int StriCmp(sbyte *s1, sbyte *s2, int n = 99999) {
			if (s1 == null) {
				if (s2 == null) {
					return 0;
				} else {
					return -1;
				}
			} else if (s2 == null) {
				return 1;
			}
			int c1, c2;
			do {
				c1 = *s1++;
				c2 = *s2++;
				if ((n--) == 0) {
					return 0;
				}
				if (c1 != c2) {
					if (c1 >= 97 && c1 <= 122) { //'a' 'z'
						c1 -= (97 - 65); //'a' 'A'
					}
					if (c2 >= 97 && c2 <= 122) { //'a' 'z'
						c2 -= (97 - 65); //'a' 'A'
					}
					if (c1 != c2) {
						return c1 < c2 ? -1 : 1;
					}
				}
			} while (c1 != 0);
			return 0;
		}
		public static int Atoi(this string str) {
			return int.TryParse(str, out int integer) ? integer : 0;
		}
		public static float Atof(this string str) {
			return float.TryParse(str, out float number) ? number : 0;
		}
		internal static int HashKey(sbyte []str, int maxlen) {
			int hash = 0;
			for (int i = 0; i < maxlen && str[i] != 0; i++) {
				hash += str[i] * (119 + i);
			}
			hash = (hash ^ (hash >> 10) ^ (hash >> 20));
			return hash;
		}
		internal static unsafe string ToString(byte *b, int len, Encoding encoding = null) {
			bool allowAll = encoding != null || Common.AllowAllEncodingCharacters;
			byte []s = Common.FilterUnusedEncodingCharacters(b, len, allowAll);
			encoding = encoding ?? Common.Encoding;
			return encoding.GetString(s).TrimEnd('\0');
		}
		internal static unsafe sbyte[] SByteFromString(string theString, Encoding encoding = null) // By TA. Kinda ugly? Hope it works ig.
		{
			if (theString.Length == 0) return null;
			bool allowAll = encoding != null || Common.AllowAllEncodingCharacters;
			encoding = encoding ?? Common.Encoding;
			byte[] bytes = encoding.GetBytes(theString);
			byte[] bytesFiltered = null;
			fixed (byte* b = bytes)
            {
				bytesFiltered = Common.FilterUnusedEncodingCharacters(b, bytes.Length, allowAll);
			}
			sbyte[] sbytes = new sbyte[bytesFiltered.Length+1];
			sbytes[bytesFiltered.Length] = 0;
			fixed(sbyte* sb = sbytes)
            {
				Marshal.Copy(bytesFiltered,0,(IntPtr)sb,bytesFiltered.Length);
			}
			return sbytes;
		}
		internal static unsafe string ToString(sbyte* b, int len, Encoding encoding = null) {
			return Common.ToString((byte*)b, len, encoding);
		}
		internal static unsafe string ToString(byte []b, Encoding encoding = null) {
			fixed (byte *s = b) {
				return Common.ToString(s, b.Length, encoding);
			}
		}
		internal static string ToString(sbyte []b, Encoding encoding = null) {
			return Common.ToString((byte[])(Array)b, encoding);
		}
		private static unsafe byte []FilterUnusedEncodingCharacters(byte *b, int len, bool allowAll) {
			byte []s = new byte[len];
			Marshal.Copy((IntPtr)b, s, 0, len);
			//fonts in JK don't support fancy characters, so we won't
			if (!allowAll) {
				for (int i = 0; i < len; i++) {
					if (s[i] > 126 && s[i] < 160) { //'~' ' '
						s[i] = 46; //'.'
					}
				}
			}
			return s;
		}

		// PopCount functions based on: https://github.com/dotnet/corert/blob/master/src/System.Private.CoreLib/shared/System/Numerics/BitOperations.cs

		/// <summary>
		/// Returns the population count (number of bits set) of a mask.
		/// Similar in behavior to the x86 instruction POPCNT.
		/// </summary>
		/// <param name="value">The value.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int PopCount(this uint value)
		{
			const uint c1 = 0x_55555555u;
			const uint c2 = 0x_33333333u;
			const uint c3 = 0x_0F0F0F0Fu;
			const uint c4 = 0x_01010101u;

			value -= (value >> 1) & c1;
			value = (value & c2) + ((value >> 2) & c2);
			value = (((value + (value >> 4)) & c3) * c4) >> 24;

			return (int)value;
		}

		/// <summary>
		/// Returns the population count (number of bits set) of a mask.
		/// Similar in behavior to the x86 instruction POPCNT.
		/// </summary>
		/// <param name="value">The value.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int PopCount(this ulong value)
		{
			const ulong c1 = 0x_55555555_55555555ul;
			const ulong c2 = 0x_33333333_33333333ul;
			const ulong c3 = 0x_0F0F0F0F_0F0F0F0Ful;
			const ulong c4 = 0x_01010101_01010101ul;

			value -= (value >> 1) & c1;
			value = (value & c2) + ((value >> 2) & c2);
			value = (((value + (value >> 4)) & c3) * c4) >> 56;

			return (int)value;
		}



	}
}
