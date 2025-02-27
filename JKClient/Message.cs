﻿
#define FASTHUFFMAN // Based on: https://github.com/mightycow/uberdemotools/commit/685b132abc4803f4c813fa07928cd9a4099e5d59

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace JKClient {

	public enum NetFieldType // For MOH
	{
		regular,
		angle,
		animTime,
		animWeight,
		scale,
		alpha,
		coord,
		// This field was introduced in TA.
		coordExtra,
		velocity,
		// not sure what is this, but it's only present in the Mac build (since AA)
		simple
	}

	static class HiddenMetaStuff
	{
		static readonly Dictionary<int, int> demoEofValues = new Dictionary<int, int>()
		{
			{ 14,8},
			{ 15,9},
			{ 16,9},
			{ 25,10},
			{ 26,10},
			{ 66,8},
			{ 67,8},
			{ 68,8},
		};

		public const string postEOFMetadataMarker = "HIDDENMETA";
		public static readonly int metaMarkerLength = postEOFMetadataMarker.Length;
		public const int maxBytePerByteSaved = 2;
		public static readonly int metaMarkerPresenceMinimumByteLengthExtra = metaMarkerLength * maxBytePerByteSaved;

		public static string getMetaDataFromDemoFile(string demoPath,bool mohScrambledString)
		{
			string lowerExtension = Path.GetExtension(demoPath).Trim().ToLower();
			if (!lowerExtension.StartsWith(".dm_"))
			{
				return null;
			}

			int protocolNumber = 0;
			string protocolNumberString = lowerExtension.Substring(4);
			if (!int.TryParse(protocolNumberString, out protocolNumber))
			{
				return null;
			}

			if (!demoEofValues.ContainsKey(protocolNumber))
			{
				return null;
			}

			int eofValue = demoEofValues[protocolNumber];

#if !DEBUG
			try
            {
#endif

			using (FileStream fs = new FileStream(demoPath, FileMode.Open, FileAccess.Read, FileShare.None))
			{
				return ParseDemoUntilMetaOrNothing(fs, fs.Length, eofValue, mohScrambledString);

			}
#if !DEBUG
			} catch(Exception e)
			{
				//MessageBox.Show("Error trying to read from demo file: "+e.ToString());
				return null;
			}
#endif
		}

		static public void createMetaMessage(Message emptyMessage,string meta, int eofValue)
        {
			int requiredCursize = emptyMessage.CurSize + metaMarkerPresenceMinimumByteLengthExtra; // We'll just set it to this value at the end if it ends up smaller.

			emptyMessage.WriteByte(eofValue);
			for (int i = 0; i < metaMarkerLength; i++)
			{
				emptyMessage.WriteByte(postEOFMetadataMarker[i]);
			}
			emptyMessage.WriteBigString(Common.SByteFromString(meta),ProtocolVersion.Protocol15);

			emptyMessage.WriteByte(eofValue);

			if (emptyMessage.CurSize < requiredCursize)
			{
				emptyMessage.CurSize = requiredCursize;
			}
		}

		static string ParseDemoUntilMetaOrNothing(FileStream fs, long demoSize, int eofValue, bool mohScrambledString)
		{
			long oldSize = demoSize;

			var msg = new Message(new byte[49152], sizeof(byte) * 49152);
			//msg.ErrorMessageCreated += Msg_ErrorMessageCreated;
			Common.MemSet(msg.Data, 0, sizeof(byte) * msg.MaxSize);
			using (BinaryReader br = new BinaryReader(fs))
			{
				int serverMessageSequence = br.ReadInt32();
				oldSize -= 4;
				msg.CurSize = br.ReadInt32();
				oldSize -= 4;
				if (msg.CurSize < 0)
					return null;
				if (msg.CurSize > msg.MaxSize)
					return null;
				byte[] messageData = br.ReadBytes(msg.CurSize);
				Array.Copy(messageData, msg.Data, messageData.Length);

				msg.BeginReading();
				int reliableSequenceAcknowledge = msg.ReadLong();
				int cmdByte = msg.ReadByte();

				if (cmdByte != eofValue)
				{
					return null;
				}

				if (msg.CurSize < msg.ReadCount + metaMarkerPresenceMinimumByteLengthExtra) // check if the metamarker can even be present, if there's enough space for it.
				{
					return null;
				}

				for (int i = 0; i < metaMarkerLength; i++) // check for meta marker.
				{
					if (msg.CurSize < msg.ReadCount + maxBytePerByteSaved)
					{
						return null;
					}
					if (postEOFMetadataMarker[i] != msg.ReadByte())
					{
						return null;
					}
				}

				string metaData = msg.ReadBigStringAsString(mohScrambledString);
				return metaData;

			}



		}
	}

	static class FastHuffman
	{
		// Fast Huffman decoder from:
		// from: https://github.com/mightycow/uberdemotools/commit/685b132abc4803f4c813fa07928cd9a4099e5d59
#if FASTHUFFMAN
		internal static readonly UInt16[] HuffmanDecoderTable = new UInt16[2048] 
		{
			2512, 2182, 512, 2763, 1859, 2808, 512, 2360, 1918, 1988, 512, 1803, 2158, 2358, 512, 2180,
			1798, 2053, 512, 1804, 2603, 1288, 512, 2166, 2285, 2167, 512, 1281, 1640, 2767, 512, 1664,
			1731, 2116, 512, 2788, 1791, 1808, 512, 1840, 2153, 1921, 512, 2708, 2723, 1549, 512, 2046,
			1893, 2717, 512, 2602, 1801, 1288, 512, 1568, 2480, 2062, 512, 1281, 2145, 2711, 512, 1543,
			1909, 2150, 512, 2077, 2338, 2762, 512, 2162, 1794, 2024, 512, 2168, 1922, 2447, 512, 2334,
			1857, 2117, 512, 2100, 2240, 1288, 512, 2186, 2321, 1908, 512, 1281, 1640, 2242, 512, 1664,
			1731, 2729, 512, 2633, 1791, 1919, 512, 2184, 1917, 1802, 512, 2710, 1795, 1549, 512, 2172,
			2375, 2789, 512, 2171, 2187, 1288, 512, 1568, 2095, 2163, 512, 1281, 1858, 1923, 512, 1543,
			2374, 2446, 512, 2181, 1859, 2160, 512, 2183, 1918, 1988, 512, 1803, 2161, 2751, 512, 2413,
			1798, 2529, 512, 1804, 2344, 1288, 512, 2404, 2156, 2786, 512, 1281, 1640, 2641, 512, 1664,
			1731, 2052, 512, 2170, 1791, 1808, 512, 1840, 2395, 1921, 512, 2586, 2319, 1549, 512, 2046,
			1893, 2101, 512, 2159, 1801, 1288, 512, 1568, 2247, 2773, 512, 1281, 2365, 2410, 512, 1543,
			1909, 2781, 512, 2097, 2411, 2740, 512, 2396, 1794, 2024, 512, 2734, 1922, 2733, 512, 2112,
			1857, 2528, 512, 2593, 2079, 1288, 512, 2648, 2143, 1908, 512, 1281, 1640, 2770, 512, 1664,
			1731, 2169, 512, 2714, 1791, 1919, 512, 2185, 1917, 1802, 512, 2398, 1795, 1549, 512, 2098,
			2801, 2361, 512, 2400, 2328, 1288, 512, 1568, 2783, 2713, 512, 1281, 1858, 1923, 512, 1543,
			2816, 2182, 512, 2497, 1859, 2397, 512, 2794, 1918, 1988, 512, 1803, 2158, 2772, 512, 2180,
			1798, 2053, 512, 1804, 2464, 1288, 512, 2166, 2285, 2167, 512, 1281, 1640, 2764, 512, 1664,
			1731, 2116, 512, 2620, 1791, 1808, 512, 1840, 2153, 1921, 512, 2716, 2384, 1549, 512, 2046,
			1893, 2448, 512, 2722, 1801, 1288, 512, 1568, 2472, 2062, 512, 1281, 2145, 2376, 512, 1543,
			1909, 2150, 512, 2077, 2366, 2709, 512, 2162, 1794, 2024, 512, 2168, 1922, 2735, 512, 2407,
			1857, 2117, 512, 2100, 2240, 1288, 512, 2186, 2779, 1908, 512, 1281, 1640, 2242, 512, 1664,
			1731, 2359, 512, 2705, 1791, 1919, 512, 2184, 1917, 1802, 512, 2642, 1795, 1549, 512, 2172,
			2394, 2645, 512, 2171, 2187, 1288, 512, 1568, 2095, 2163, 512, 1281, 1858, 1923, 512, 1543,
			2450, 2771, 512, 2181, 1859, 2160, 512, 2183, 1918, 1988, 512, 1803, 2161, 2585, 512, 2403,
			1798, 2619, 512, 1804, 2777, 1288, 512, 2355, 2156, 2362, 512, 1281, 1640, 2380, 512, 1664,
			1731, 2052, 512, 2170, 1791, 1808, 512, 1840, 2811, 1921, 512, 2402, 2601, 1549, 512, 2046,
			1893, 2101, 512, 2159, 1801, 1288, 512, 1568, 2247, 2719, 512, 1281, 2747, 2776, 512, 1543,
			1909, 2725, 512, 2097, 2445, 2765, 512, 2638, 1794, 2024, 512, 2444, 1922, 2774, 512, 2112,
			1857, 2727, 512, 2644, 2079, 1288, 512, 2800, 2143, 1908, 512, 1281, 1640, 2580, 512, 1664,
			1731, 2169, 512, 2646, 1791, 1919, 512, 2185, 1917, 1802, 512, 2588, 1795, 1549, 512, 2098,
			2322, 2504, 512, 2623, 2350, 1288, 512, 1568, 2323, 2721, 512, 1281, 1858, 1923, 512, 1543,
			2512, 2182, 512, 2746, 1859, 2798, 512, 2360, 1918, 1988, 512, 1803, 2158, 2358, 512, 2180,
			1798, 2053, 512, 1804, 2745, 1288, 512, 2166, 2285, 2167, 512, 1281, 1640, 2806, 512, 1664,
			1731, 2116, 512, 2796, 1791, 1808, 512, 1840, 2153, 1921, 512, 2582, 2761, 1549, 512, 2046,
			1893, 2793, 512, 2647, 1801, 1288, 512, 1568, 2480, 2062, 512, 1281, 2145, 2738, 512, 1543,
			1909, 2150, 512, 2077, 2338, 2715, 512, 2162, 1794, 2024, 512, 2168, 1922, 2447, 512, 2334,
			1857, 2117, 512, 2100, 2240, 1288, 512, 2186, 2321, 1908, 512, 1281, 1640, 2242, 512, 1664,
			1731, 2795, 512, 2750, 1791, 1919, 512, 2184, 1917, 1802, 512, 2732, 1795, 1549, 512, 2172,
			2375, 2604, 512, 2171, 2187, 1288, 512, 1568, 2095, 2163, 512, 1281, 1858, 1923, 512, 1543,
			2374, 2446, 512, 2181, 1859, 2160, 512, 2183, 1918, 1988, 512, 1803, 2161, 2813, 512, 2413,
			1798, 2529, 512, 1804, 2344, 1288, 512, 2404, 2156, 2743, 512, 1281, 1640, 2748, 512, 1664,
			1731, 2052, 512, 2170, 1791, 1808, 512, 1840, 2395, 1921, 512, 2637, 2319, 1549, 512, 2046,
			1893, 2101, 512, 2159, 1801, 1288, 512, 1568, 2247, 2812, 512, 1281, 2365, 2410, 512, 1543,
			1909, 2799, 512, 2097, 2411, 2802, 512, 2396, 1794, 2024, 512, 2649, 1922, 2595, 512, 2112,
			1857, 2528, 512, 2790, 2079, 1288, 512, 2634, 2143, 1908, 512, 1281, 1640, 2724, 512, 1664,
			1731, 2169, 512, 2730, 1791, 1919, 512, 2185, 1917, 1802, 512, 2398, 1795, 1549, 512, 2098,
			2605, 2361, 512, 2400, 2328, 1288, 512, 1568, 2787, 2810, 512, 1281, 1858, 1923, 512, 1543,
			2803, 2182, 512, 2497, 1859, 2397, 512, 2758, 1918, 1988, 512, 1803, 2158, 2598, 512, 2180,
			1798, 2053, 512, 1804, 2464, 1288, 512, 2166, 2285, 2167, 512, 1281, 1640, 2726, 512, 1664,
			1731, 2116, 512, 2583, 1791, 1808, 512, 1840, 2153, 1921, 512, 2712, 2384, 1549, 512, 2046,
			1893, 2448, 512, 2639, 1801, 1288, 512, 1568, 2472, 2062, 512, 1281, 2145, 2376, 512, 1543,
			1909, 2150, 512, 2077, 2366, 2731, 512, 2162, 1794, 2024, 512, 2168, 1922, 2766, 512, 2407,
			1857, 2117, 512, 2100, 2240, 1288, 512, 2186, 2809, 1908, 512, 1281, 1640, 2242, 512, 1664,
			1731, 2359, 512, 2587, 1791, 1919, 512, 2184, 1917, 1802, 512, 2643, 1795, 1549, 512, 2172,
			2394, 2635, 512, 2171, 2187, 1288, 512, 1568, 2095, 2163, 512, 1281, 1858, 1923, 512, 1543,
			2450, 2749, 512, 2181, 1859, 2160, 512, 2183, 1918, 1988, 512, 1803, 2161, 2778, 512, 2403,
			1798, 2791, 512, 1804, 2775, 1288, 512, 2355, 2156, 2362, 512, 1281, 1640, 2380, 512, 1664,
			1731, 2052, 512, 2170, 1791, 1808, 512, 1840, 2805, 1921, 512, 2402, 2741, 1549, 512, 2046,
			1893, 2101, 512, 2159, 1801, 1288, 512, 1568, 2247, 2769, 512, 1281, 2739, 2780, 512, 1543,
			1909, 2737, 512, 2097, 2445, 2596, 512, 2757, 1794, 2024, 512, 2444, 1922, 2599, 512, 2112,
			1857, 2804, 512, 2744, 2079, 1288, 512, 2707, 2143, 1908, 512, 1281, 1640, 2782, 512, 1664,
			1731, 2169, 512, 2742, 1791, 1919, 512, 2185, 1917, 1802, 512, 2718, 1795, 1549, 512, 2098,
			2322, 2504, 512, 2581, 2350, 1288, 512, 1568, 2323, 2597, 512, 1281, 1858, 1923, 512, 1543,
			2512, 2182, 512, 2763, 1859, 2808, 512, 2360, 1918, 1988, 512, 1803, 2158, 2358, 512, 2180,
			1798, 2053, 512, 1804, 2603, 1288, 512, 2166, 2285, 2167, 512, 1281, 1640, 2767, 512, 1664,
			1731, 2116, 512, 2788, 1791, 1808, 512, 1840, 2153, 1921, 512, 2708, 2723, 1549, 512, 2046,
			1893, 2717, 512, 2602, 1801, 1288, 512, 1568, 2480, 2062, 512, 1281, 2145, 2711, 512, 1543,
			1909, 2150, 512, 2077, 2338, 2762, 512, 2162, 1794, 2024, 512, 2168, 1922, 2447, 512, 2334,
			1857, 2117, 512, 2100, 2240, 1288, 512, 2186, 2321, 1908, 512, 1281, 1640, 2242, 512, 1664,
			1731, 2729, 512, 2633, 1791, 1919, 512, 2184, 1917, 1802, 512, 2710, 1795, 1549, 512, 2172,
			2375, 2789, 512, 2171, 2187, 1288, 512, 1568, 2095, 2163, 512, 1281, 1858, 1923, 512, 1543,
			2374, 2446, 512, 2181, 1859, 2160, 512, 2183, 1918, 1988, 512, 1803, 2161, 2751, 512, 2413,
			1798, 2529, 512, 1804, 2344, 1288, 512, 2404, 2156, 2786, 512, 1281, 1640, 2641, 512, 1664,
			1731, 2052, 512, 2170, 1791, 1808, 512, 1840, 2395, 1921, 512, 2586, 2319, 1549, 512, 2046,
			1893, 2101, 512, 2159, 1801, 1288, 512, 1568, 2247, 2773, 512, 1281, 2365, 2410, 512, 1543,
			1909, 2781, 512, 2097, 2411, 2740, 512, 2396, 1794, 2024, 512, 2734, 1922, 2733, 512, 2112,
			1857, 2528, 512, 2593, 2079, 1288, 512, 2648, 2143, 1908, 512, 1281, 1640, 2770, 512, 1664,
			1731, 2169, 512, 2714, 1791, 1919, 512, 2185, 1917, 1802, 512, 2398, 1795, 1549, 512, 2098,
			2801, 2361, 512, 2400, 2328, 1288, 512, 1568, 2783, 2713, 512, 1281, 1858, 1923, 512, 1543,
			3063, 2182, 512, 2497, 1859, 2397, 512, 2794, 1918, 1988, 512, 1803, 2158, 2772, 512, 2180,
			1798, 2053, 512, 1804, 2464, 1288, 512, 2166, 2285, 2167, 512, 1281, 1640, 2764, 512, 1664,
			1731, 2116, 512, 2620, 1791, 1808, 512, 1840, 2153, 1921, 512, 2716, 2384, 1549, 512, 2046,
			1893, 2448, 512, 2722, 1801, 1288, 512, 1568, 2472, 2062, 512, 1281, 2145, 2376, 512, 1543,
			1909, 2150, 512, 2077, 2366, 2709, 512, 2162, 1794, 2024, 512, 2168, 1922, 2735, 512, 2407,
			1857, 2117, 512, 2100, 2240, 1288, 512, 2186, 2779, 1908, 512, 1281, 1640, 2242, 512, 1664,
			1731, 2359, 512, 2705, 1791, 1919, 512, 2184, 1917, 1802, 512, 2642, 1795, 1549, 512, 2172,
			2394, 2645, 512, 2171, 2187, 1288, 512, 1568, 2095, 2163, 512, 1281, 1858, 1923, 512, 1543,
			2450, 2771, 512, 2181, 1859, 2160, 512, 2183, 1918, 1988, 512, 1803, 2161, 2585, 512, 2403,
			1798, 2619, 512, 1804, 2777, 1288, 512, 2355, 2156, 2362, 512, 1281, 1640, 2380, 512, 1664,
			1731, 2052, 512, 2170, 1791, 1808, 512, 1840, 2811, 1921, 512, 2402, 2601, 1549, 512, 2046,
			1893, 2101, 512, 2159, 1801, 1288, 512, 1568, 2247, 2719, 512, 1281, 2747, 2776, 512, 1543,
			1909, 2725, 512, 2097, 2445, 2765, 512, 2638, 1794, 2024, 512, 2444, 1922, 2774, 512, 2112,
			1857, 2727, 512, 2644, 2079, 1288, 512, 2800, 2143, 1908, 512, 1281, 1640, 2580, 512, 1664,
			1731, 2169, 512, 2646, 1791, 1919, 512, 2185, 1917, 1802, 512, 2588, 1795, 1549, 512, 2098,
			2322, 2504, 512, 2623, 2350, 1288, 512, 1568, 2323, 2721, 512, 1281, 1858, 1923, 512, 1543,
			2512, 2182, 512, 2746, 1859, 2798, 512, 2360, 1918, 1988, 512, 1803, 2158, 2358, 512, 2180,
			1798, 2053, 512, 1804, 2745, 1288, 512, 2166, 2285, 2167, 512, 1281, 1640, 2806, 512, 1664,
			1731, 2116, 512, 2796, 1791, 1808, 512, 1840, 2153, 1921, 512, 2582, 2761, 1549, 512, 2046,
			1893, 2793, 512, 2647, 1801, 1288, 512, 1568, 2480, 2062, 512, 1281, 2145, 2738, 512, 1543,
			1909, 2150, 512, 2077, 2338, 2715, 512, 2162, 1794, 2024, 512, 2168, 1922, 2447, 512, 2334,
			1857, 2117, 512, 2100, 2240, 1288, 512, 2186, 2321, 1908, 512, 1281, 1640, 2242, 512, 1664,
			1731, 2795, 512, 2750, 1791, 1919, 512, 2184, 1917, 1802, 512, 2732, 1795, 1549, 512, 2172,
			2375, 2604, 512, 2171, 2187, 1288, 512, 1568, 2095, 2163, 512, 1281, 1858, 1923, 512, 1543,
			2374, 2446, 512, 2181, 1859, 2160, 512, 2183, 1918, 1988, 512, 1803, 2161, 2813, 512, 2413,
			1798, 2529, 512, 1804, 2344, 1288, 512, 2404, 2156, 2743, 512, 1281, 1640, 2748, 512, 1664,
			1731, 2052, 512, 2170, 1791, 1808, 512, 1840, 2395, 1921, 512, 2637, 2319, 1549, 512, 2046,
			1893, 2101, 512, 2159, 1801, 1288, 512, 1568, 2247, 2812, 512, 1281, 2365, 2410, 512, 1543,
			1909, 2799, 512, 2097, 2411, 2802, 512, 2396, 1794, 2024, 512, 2649, 1922, 2595, 512, 2112,
			1857, 2528, 512, 2790, 2079, 1288, 512, 2634, 2143, 1908, 512, 1281, 1640, 2724, 512, 1664,
			1731, 2169, 512, 2730, 1791, 1919, 512, 2185, 1917, 1802, 512, 2398, 1795, 1549, 512, 2098,
			2605, 2361, 512, 2400, 2328, 1288, 512, 1568, 2787, 2810, 512, 1281, 1858, 1923, 512, 1543,
			2803, 2182, 512, 2497, 1859, 2397, 512, 2758, 1918, 1988, 512, 1803, 2158, 2598, 512, 2180,
			1798, 2053, 512, 1804, 2464, 1288, 512, 2166, 2285, 2167, 512, 1281, 1640, 2726, 512, 1664,
			1731, 2116, 512, 2583, 1791, 1808, 512, 1840, 2153, 1921, 512, 2712, 2384, 1549, 512, 2046,
			1893, 2448, 512, 2639, 1801, 1288, 512, 1568, 2472, 2062, 512, 1281, 2145, 2376, 512, 1543,
			1909, 2150, 512, 2077, 2366, 2731, 512, 2162, 1794, 2024, 512, 2168, 1922, 2766, 512, 2407,
			1857, 2117, 512, 2100, 2240, 1288, 512, 2186, 2809, 1908, 512, 1281, 1640, 2242, 512, 1664,
			1731, 2359, 512, 2587, 1791, 1919, 512, 2184, 1917, 1802, 512, 2643, 1795, 1549, 512, 2172,
			2394, 2635, 512, 2171, 2187, 1288, 512, 1568, 2095, 2163, 512, 1281, 1858, 1923, 512, 1543,
			2450, 2749, 512, 2181, 1859, 2160, 512, 2183, 1918, 1988, 512, 1803, 2161, 2778, 512, 2403,
			1798, 2791, 512, 1804, 2775, 1288, 512, 2355, 2156, 2362, 512, 1281, 1640, 2380, 512, 1664,
			1731, 2052, 512, 2170, 1791, 1808, 512, 1840, 2805, 1921, 512, 2402, 2741, 1549, 512, 2046,
			1893, 2101, 512, 2159, 1801, 1288, 512, 1568, 2247, 2769, 512, 1281, 2739, 2780, 512, 1543,
			1909, 2737, 512, 2097, 2445, 2596, 512, 2757, 1794, 2024, 512, 2444, 1922, 2599, 512, 2112,
			1857, 2804, 512, 2744, 2079, 1288, 512, 2707, 2143, 1908, 512, 1281, 1640, 2782, 512, 1664,
			1731, 2169, 512, 2742, 1791, 1919, 512, 2185, 1917, 1802, 512, 2718, 1795, 1549, 512, 2098,
			2322, 2504, 512, 2581, 2350, 1288, 512, 1568, 2323, 2597, 512, 1281, 1858, 1923, 512, 1543
		};

		private static readonly UInt16[] HuffmanEncoderTable = new ushort[256]
		{
			34, 437, 1159, 1735, 2584, 280, 263, 1014, 341, 839, 1687, 183, 311, 726, 920, 2761,
			599, 1417, 7945, 8073, 7642, 16186, 8890, 12858, 3913, 6362, 2746, 13882, 7866, 1080, 1273, 3400,
			886, 3386, 1097, 11482, 15450, 16282, 12506, 15578, 2377, 6858, 826, 330, 10010, 12042, 8009, 1928,
			631, 3128, 3832, 6521, 1336, 2840, 217, 5657, 121, 3865, 6553, 6426, 4666, 3017, 5193, 7994,
			3320, 1287, 1991, 71, 536, 1304, 2057, 1801, 5081, 1594, 11642, 14106, 6617, 10938, 7290, 13114,
			4809, 2522, 5818, 14010, 7482, 5914, 7738, 9018, 3450, 11450, 5897, 2697, 3193, 4185, 3769, 3464,
			3897, 968, 6841, 6393, 2425, 775, 1048, 5369, 454, 648, 3033, 3145, 2440, 2297, 200, 2872,
			2136, 2248, 1144, 1944, 1431, 1031, 376, 408, 1208, 3608, 2616, 1848, 1784, 1671, 135, 1623,
			502, 663, 1223, 2007, 248, 2104, 24, 2168, 1656, 3704, 1400, 1864, 7353, 7241, 2073, 1241,
			4889, 5690, 6153, 15738, 698, 5210, 1722, 986, 12986, 3994, 3642, 9306, 4794, 794, 16058, 7066,
			4425, 8090, 4922, 714, 11738, 7194, 12762, 7450, 5001, 1562, 11834, 13402, 9914, 3290, 3258, 5338,
			905, 15386, 9178, 15306, 3162, 15050, 15930, 10650, 15674, 8522, 8250, 7114, 10714, 14362, 9786, 2266,
			1352, 4153, 1496, 518, 151, 15482, 12410, 2952, 7961, 8906, 1114, 58, 4570, 7258, 13530, 474,
			9, 15258, 3546, 6170, 4314, 2970, 7386, 14666, 7130, 6474, 14554, 5514, 15322, 3098, 15834, 3978,
			3353, 2329, 2458, 12170, 570, 1818, 11578, 14618, 1175, 8986, 4218, 9754, 8762, 392, 8282, 11290,
			7546, 3850, 11354, 12298, 15642, 14986, 8666, 20491, 90, 13706, 12186, 6794, 11162, 10458, 759, 582
		};



		internal static unsafe void HuffmanPutBitFast(byte* fout, Int32 bitIndex, Int32 bit)
		{
			Int32 byteIndex = bitIndex >> 3;
			Int32 bitOffset = bitIndex & 7;
			if (bitOffset == 0) // Is this the first bit of a new byte?
			{
				// We don't need to preserve what's already in there,
				// so we can write that byte immediately.
				fout[byteIndex] = (byte)bit;
				return;
			}

			fout[(bitIndex >> 3)] |= (byte)(bit << (bitIndex & 7));
		}

		internal static unsafe void HuffmanOffsetTransmitFast(byte* fout, Int32* offset, Int32 ch)
		{
			UInt16 result = HuffmanEncoderTable[ch];
			UInt16 bitCount = (UInt16)(result & 15);
			UInt16 code = (UInt16)((result >> 4) & 0x7FF);
			UInt32 bitIndex = *(UInt32*)offset;

			Int32 bits = (Int32)code;
			for (UInt32 i = 0; i < bitCount; ++i)
			{
				HuffmanPutBitFast(fout, (int)(bitIndex + i), bits & 1);
				bits >>= 1;
			}

			*offset += (Int32)bitCount;
		}

#endif
	}



	internal class BufferedDemoMessageContainer
    {
		public Message msg;
		public DateTime time;
		public bool containsFullSnapshot;
		public int? serverTime = null;
    }

    public class MessageCopy {
		public bool Overflowed { get; init; }
		public bool OOB { get; init; }
		public byte[] Data { get; init; }
		public int MaxSize { get; init; }
		public int CurSize { get; init; } = 0;
		public int ReadCount { get; init; } = 0;
		public int Bit { get; init; } = 0;
		public string debugMessage = null;
	}

    
	internal sealed partial class Message {
		
		public const int FloatIntBits = 13;
		private const int FloatIntBias = (1<<(Message.FloatIntBits-1));
		private int bit = 0;
		private int bitSaved = 0;
		private bool oobSaved = false;
		private int readCountSaved = 0;
		public bool Overflowed { get; private set; }
		public bool OOB { get; private set; }
		public byte []Data { get; init; }
		public int MaxSize { get; init; }
		public int CurSize { get; set; } = 0;
		public int ReadCount { get; private set; } = 0;
		public int Bit {
			get => this.bit;
			private set => this.bit = value;
		}
		private static readonly Huffman compressor, decompressor;
		static Message() {
			Message.compressor = new Huffman();
			Message.decompressor = new Huffman(true);
			for (int i = 0; i < 256; i++) {
				for (int j = 0; j < Message.hData[i]; j++) {
					Message.compressor.AddReference((byte)i);
					Message.decompressor.AddReference((byte)i);
				}
			}
		}
		public Message() {}
		public Message(byte []data, int length, bool oob = false) {
			this.Data = data;
			this.MaxSize = length;
			this.OOB = oob;
		}
		public Message Clone()
        {
			Message retVal = new Message() { MaxSize= this.MaxSize, Data= (byte[])this.Data.Clone() };
			retVal.Overflowed = this.Overflowed;
			retVal.OOB = this.OOB;
			//retVal.MaxSize = this.MaxSize;
			retVal.CurSize = this.CurSize;
			retVal.ReadCount = this.ReadCount;
			retVal.Bit = this.Bit;
			//retVal.Data = (byte[])this.Data.Clone();
			return retVal;
        }
		public MessageCopy MakePublicCopy()
		{
			MessageCopy retVal = new MessageCopy()
			{
				MaxSize = this.MaxSize,
				Data = (byte[])this.Data.Clone(),
				Overflowed = this.Overflowed,
				OOB = this.OOB,
				CurSize = this.CurSize,
				ReadCount = this.ReadCount,
				Bit = this.Bit,
				debugMessage = msgDebugLog.ToString()
			};
			return retVal;
        }

		private StringBuilder msgDebugLog = new StringBuilder();

		internal event EventHandler<Tuple<string, string>> ErrorMessageCreated;
		private void OnErrorMessageCreated(string errorMessage)
        {
			ErrorMessageCreated?.Invoke(this,new Tuple<string,string>(errorMessage, msgDebugLog.ToString()));
        }

		private void doDebugLog(string details)
        {
			//(string calledMethod, string callingMethod) = getCallingAndCalledMethod(2);
			//msgDebugLog.Append($"{calledMethod}<={callingMethod}: {details}\n");
			//(string calledMethod, string callingMethod) = getCallingAndCalledMethod(2);
			msgDebugLog.Append($"{details}\n");
		}
		internal void doDebugLogExt(string details)
        {
			doDebugLog($"External message manipulation: {details}");
		}
		private (string,string) getCallingAndCalledMethod(int skipFrames = 1)
        {
			StringBuilder sb = new StringBuilder();
			StackTrace st = new StackTrace();

			int selfIndex = 0;
			//string calledMethod = null;
			for (int i = skipFrames; i < st.FrameCount; i++)
            {
                StackFrame frame = st.GetFrame(i);
				if (!frame.HasMethod()) continue;
                System.Reflection.MethodBase method = frame.GetMethod();
				if (method.DeclaringType != typeof(Message))
                {
					// find the first method in the stacktrace that isn't inside Message and return it.
					return (sb.ToString(), $"{method.ReflectedType.ToString()}::{method.Name.ToString()}({frame.GetFileLineNumber()})"); 
                } else
                {
					if(selfIndex > 0)
					{
						sb.Append($"<={method.Name.ToString()}({frame.GetFileLineNumber()})");
					} else
					{
						sb.Append($"{method.Name.ToString()}({frame.GetFileLineNumber()})");
					}
					selfIndex++;
				}
            }
			return (sb.ToString(), null);
        }

		// MOHAA Scrambled String table
		// Scrambled string conversion (write)
		static byte[] StrCharToNetByte = new byte[256]{
			254, 120, 89, 13, 27, 73, 103, 78, 74, 102, 21, 117, 76, 86, 238, 96, 88, 62, 59, 60,
			40, 84, 52, 119, 251, 51, 75, 121, 192, 85, 44, 54, 114, 87, 25, 53, 35, 224, 67, 31,
			82, 41, 45, 99, 233, 112, 255, 11, 46, 115, 8, 32, 19, 100, 110, 95, 116, 48, 58, 107,
			70, 91, 104, 81, 118, 109, 36, 24, 17, 39, 43, 65, 49, 83, 56, 57, 33, 64, 80, 28,
			184, 160, 18, 105, 42, 20, 194, 38, 29, 26, 61, 50, 9, 90, 37, 128, 79, 2, 108, 34,
			4, 0, 47, 12, 101, 10, 92, 15, 5, 7, 22, 55, 23, 14, 3, 1, 66, 16, 63, 30,
			6, 97, 111, 248, 72, 197, 191, 122, 176, 245, 250, 68, 195, 77, 232, 106, 228, 93, 240, 98,
			208, 69, 164, 144, 186, 222, 94, 246, 148, 170, 244, 190, 205, 234, 252, 202, 230, 239, 174, 225,
			226, 209, 236, 216, 237, 151, 149, 231, 129, 188, 200, 172, 204, 154, 168, 71, 133, 217, 196, 223,
			134, 253, 173, 177, 219, 235, 214, 182, 132, 227, 183, 175, 137, 152, 158, 221, 243, 150, 210, 136,
			167, 211, 179, 193, 218, 124, 140, 178, 213, 249, 185, 113, 127, 220, 180, 145, 138, 198, 123, 162,
			189, 203, 166, 126, 159, 156, 212, 207, 146, 181, 247, 139, 142, 169, 242, 241, 171, 187, 153, 135,
			201, 155, 161, 125, 163, 130, 229, 206, 165, 157, 141, 147, 143, 199, 215, 131
		};

		// Scrambled string conversion (read)
		static byte[] NetByteToStrChar = new byte[256]
		{
			101, 115, 97, 114, 100, 108, 120, 109, 50, 92, 105, 47, 103, 3, 113, 107, 117, 68, 82, 52,
			85, 10, 110, 112, 67, 34, 89, 4, 79, 88, 119, 39, 51, 76, 99, 36, 66, 94, 87, 69,
			20, 41, 84, 70, 30, 42, 48, 102, 57, 72, 91, 25, 22, 35, 31, 111, 74, 75, 58, 18,
			19, 90, 17, 118, 77, 71, 116, 38, 131, 141, 60, 175, 124, 5, 8, 26, 12, 133, 7, 96,
			78, 63, 40, 73, 21, 29, 13, 33, 16, 2, 93, 61, 106, 137, 146, 55, 15, 121, 139, 43,
			53, 104, 9, 6, 62, 83, 135, 59, 98, 65, 54, 122, 45, 211, 32, 49, 56, 11, 64, 23,
			1, 27, 127, 218, 205, 243, 223, 212, 95, 168, 245, 255, 188, 176, 180, 239, 199, 192, 216, 231,
			206, 250, 232, 252, 143, 215, 228, 251, 148, 166, 197, 165, 193, 238, 173, 241, 225, 249, 194, 224,
			81, 242, 219, 244, 142, 248, 222, 200, 174, 233, 149, 236, 171, 182, 158, 191, 128, 183, 207, 202,
			214, 229, 187, 190, 80, 210, 144, 237, 169, 220, 151, 126, 28, 203, 86, 132, 178, 125, 217, 253,
			170, 240, 155, 221, 172, 152, 247, 227, 140, 161, 198, 201, 226, 208, 186, 254, 163, 177, 204, 184,
			213, 195, 145, 179, 37, 159, 160, 189, 136, 246, 156, 167, 134, 44, 153, 185, 162, 164, 14, 157,
			138, 235, 234, 196, 150, 129, 147, 230, 123, 209, 130, 24, 154, 181, 0, 46
		};
		// MOHAA Scrambled String table end




		public void Bitstream([CallerMemberName] string callingMethod = null) {
			this.OOB = false;
#if STRONGREADDEBUG
			doDebugLog($"{callingMethod}=>Bitstream()");
#endif
		}
		public void SaveState([CallerMemberName] string callingMethod = null) {
			this.bitSaved = this.bit;
			this.oobSaved = this.OOB;
			this.readCountSaved = this.ReadCount;
#if STRONGREADDEBUG
			doDebugLog($"{callingMethod}=>SaveState()");
#endif
		}
		public void RestoreState([CallerMemberName] string callingMethod = null) {
			this.bit = this.bitSaved;
			this.OOB = this.oobSaved;
			this.ReadCount = this.readCountSaved;
#if STRONGREADDEBUG
			doDebugLog($"{callingMethod}=>RestoreState()");
#endif
		}
		public unsafe void WriteBits(int value, int bits) {
			if (this.MaxSize - this.CurSize < 4) {
				this.Overflowed = true;
				return;
			}
			if (bits == 0 || bits < -31 || bits > 32) {
				throw new JKClientException($"WriteBits: bad bits {bits}");
			}
			if (bits < 0) {
				bits = -bits;
			}
			if (this.OOB) {
				if (bits == 8) {
					this.Data[this.CurSize] = (byte)value;
					this.CurSize += 1;
					this.bit += 8;
				} else if (bits == 16) {
					byte []temp = BitConverter.GetBytes((short)value);
					Array.Copy(temp, 0, this.Data, this.CurSize, 2);
					this.CurSize += 2;
					this.bit += 16;
				} else if (bits == 32) {
					byte []temp = BitConverter.GetBytes(value);
					Array.Copy(temp, 0, this.Data, this.CurSize, 4);
					this.CurSize += 4;
					this.bit += 32;
				}
			} else {
#if FASTHUFFMAN
				value &= (int)(0xffffffff >> (32 - bits));
				if ((bits & 7)!=0) {
					int nbits;
					nbits = bits & 7;
					for (int i = 0; i < nbits; i++) {
						//Huff_putBit((value & 1), msg->data, &msg->bit);
						fixed(byte* dataBytes = this.Data)
                        {
							FastHuffman.HuffmanPutBitFast(dataBytes, this.Bit, (value & 1));
						}
						value = (value >> 1);
						this.Bit++;
					}
					bits = bits - nbits;
				}
				if (bits != 0) {
					for (int i = 0; i < bits; i += 8) {
						int thisBit = this.Bit; // Ridiculous, I know. But we can't get the address of member. C# things.
						fixed (byte* dataBytes = this.Data )
						{
							FastHuffman.HuffmanOffsetTransmitFast(dataBytes, &thisBit, (value & 0xff));
						}
						this.Bit = thisBit; // Ridiculous, I know. But we can't get the address of member. C# things.
						value = (value >> 8);
					}
				}
				this.CurSize = (this.Bit >> 3) + 1;
#else
				lock (Message.compressor) {
					value &= (int)(0xffffffff>>(32-bits));
					if ((bits&7) != 0) {
						int nbits = bits&7;
						for (int i = 0; i < nbits; i++) {
							Message.compressor.PutBit((value&1), this.Data, ref this.bit);
							value >>= 1;
						}
						bits -= nbits;
					}
					if (bits != 0) {
						for (int i = 0; i < bits; i+=8) {
							Message.compressor.OffsetTransmit((value&0xff), this.Data, ref this.bit);
							value >>= 8;
						}
					}
					this.CurSize = (this.bit>>3)+1;
				}
#endif
			}
		}
		public void WriteByte(int c) {
			this.WriteBits(c, 8);
		}
		public unsafe void WriteData(byte []data, int length) {
			fixed (byte *d = data) {
				this.WriteData(d, length);
			}
		}
		public unsafe void WriteData(byte *data, int length) {
			for (int i = 0; i < length; i++) {
				this.WriteByte(data[i]);
			}
		}
		public void WriteShort(int c) {
			this.WriteBits(c, 16);
		}
		public void WriteLong(int c) {
			this.WriteBits(c, 32);
		}
		public unsafe void WriteString(sbyte []s, ProtocolVersion protocol) {

			bool isMOH = Common.ProtocolIsMOH(protocol);
			bool isMOHWithScrambledString = isMOH && protocol > ProtocolVersion.Protocol8;
			if (s == null || s.Length <= 0) {
				this.WriteByte(isMOHWithScrambledString ? StrCharToNetByte[0] : 0);
			} else {
				int l = Common.StrLen(s);
				if (l >= (isMOH ? Common.MaxStringCharsMOH : Common.MaxStringChars)) {
					this.WriteByte(isMOHWithScrambledString ? StrCharToNetByte[0] : 0);
					return;
				}
                if (isMOHWithScrambledString)
                {
					for (int i = 0; i <= l; i++)
					{
						this.WriteByte(StrCharToNetByte[(byte)s[i]]);
					}
				} else
                {
					byte []b = new byte[l+1];
					fixed (sbyte *ss = s) {
						Marshal.Copy((IntPtr)ss, b, 0, l);
					}
					this.WriteData(b, l+1);
                }
			}
		}

		public unsafe void WriteBigString(sbyte []s, ProtocolVersion protocol) { // TODO someday: moh scrambled messages.

			bool isMOH = Common.ProtocolIsMOH(protocol);
			bool isMOHWithScrambledString = isMOH && protocol > ProtocolVersion.Protocol8;

			if (s == null || s.Length <= 0) {
				this.WriteByte(isMOHWithScrambledString ? StrCharToNetByte[0] : 0);
			} else {
				int l = Common.StrLen(s);
				if (l >= Common.BigInfoString) {
					this.WriteByte(isMOHWithScrambledString ? StrCharToNetByte[0] : 0);
					return;
				}
				if (isMOHWithScrambledString)
                {
					for (int i = 0; i <= l; i++)
					{
						this.WriteByte(StrCharToNetByte[(byte)s[i]]);
					}
				} else
                {
					byte[] b = new byte[l + 1];
					fixed (sbyte* ss = s)
					{
						Marshal.Copy((IntPtr)ss, b, 0, l);
					}
					this.WriteData(b, l + 1);
				}
			}
		}

		public void WriteDeltaKey(int key, int oldV, int newV, int bits)
		{
			if (oldV == newV)
			{
				this.WriteBits(0, 1);
				return;
			}
			this.WriteBits(1, 1);
			this.WriteBits((newV ^ key) & ((1 << bits) - 1), bits);
		}

		public unsafe void WriteDeltaUsercmdKey(int key, ref UserCommand from, ref UserCommand to, bool isMOH) {
			if (to.ServerTime - from.ServerTime < 256) {
				this.WriteBits(1, 1);
				this.WriteBits(to.ServerTime - from.ServerTime, 8);
			} else {
				this.WriteBits(0, 1);
				this.WriteBits(to.ServerTime, 32);
			}
			if (from.Angles[0] == to.Angles[0] &&
				from.Angles[1] == to.Angles[1] &&
				from.Angles[2] == to.Angles[2] &&
				from.ForwardMove == to.ForwardMove &&
				from.RightMove == to.RightMove &&
				from.Upmove == to.Upmove &&
				from.Buttons == to.Buttons &&
				(isMOH ||
					(
					from.Weapon == to.Weapon &&
					from.ForceSelection == to.ForceSelection &&
					from.InventorySelection == to.InventorySelection &&
					from.GenericCmd == to.GenericCmd
					)
				)
			)
			{
				this.WriteBits(0, 1);               // no change
				return;
			}
			key ^= to.ServerTime;
			this.WriteBits(1, 1);
			this.WriteDeltaKey(key, from.Angles[0], to.Angles[0], 16);
			this.WriteDeltaKey(key, from.Angles[1], to.Angles[1], 16);
			this.WriteDeltaKey(key, from.Angles[2], to.Angles[2], 16);
			this.WriteDeltaKey(key, from.ForwardMove, to.ForwardMove, 8);
			this.WriteDeltaKey(key, from.RightMove, to.RightMove, 8);
			this.WriteDeltaKey(key, from.Upmove, to.Upmove, 8);
			this.WriteDeltaKey(key, from.Buttons, to.Buttons, 16);

            if (!isMOH) { 
				this.WriteDeltaKey(key, from.Weapon, to.Weapon, 8);

				this.WriteDeltaKey(key, from.ForceSelection, to.ForceSelection, 8);
				this.WriteDeltaKey(key, from.InventorySelection, to.InventorySelection, 8);

				this.WriteDeltaKey(key, from.GenericCmd, to.GenericCmd, 8);
			}
		}
		public void BeginReading(bool oob = false, [CallerMemberName] string callingMethod = null) {
			this.ReadCount = 0;
			this.bit = 0;
			this.OOB = oob;
#if STRONGREADDEBUG
			doDebugLog($"{callingMethod}=>BeginReading(oob:{oob})");
#endif
		}
		public unsafe int ReadBits(int bits, string earlierCallingMethod = null, string note = null, [CallerMemberName] string callingMethod = null) {
			int value = 0;
			int obits = bits;
			bool sgn;
			if (bits < 0) {
				bits = -bits;
				sgn = true;
			} else {
				sgn = false;
			}
			if (this.OOB) {
				if (bits == 8) {
					value = this.Data[this.ReadCount];
					this.ReadCount += 1;
					this.bit += 8;
				} else if (bits == 16) {
					fixed (byte *b = &this.Data[this.ReadCount]) {
						value = *(short*)b;
					}
					this.ReadCount += 2;
					this.bit += 16;
				} else if (bits == 32) {
					fixed (byte *b = &this.Data[this.ReadCount]) {
						value = *(int*)b;
					}
					this.ReadCount += 4;
					this.bit += 32;
				}
			} else {
#if FASTHUFFMAN
				fixed(byte* bufferData = this.Data)
                {

					Int32 nbits = bits & 7;
					Int32 bitIndex = this.Bit;
					if (nbits != 0)
					{
						Int16 allBits = (Int16)(*(Int16*)(bufferData + (bitIndex >> 3)) >> (bitIndex & 7));
						value = allBits & ((1 << nbits) - 1);
						bitIndex += nbits;
						bits -= nbits;
					}

					if (bits != 0)
					{
						for (Int32 i = 0; i < bits; i += 8)
						{
							UInt16 code = (UInt16)(((*(UInt32*)(bufferData + (bitIndex >> 3))) >> (int)((UInt32)bitIndex & 7)) &0x7FF);
							UInt16 entry = FastHuffman.HuffmanDecoderTable[code];
							value |= ((Int32)(entry & 0xFF) << (i + nbits));
							bitIndex += (Int32)(entry >> 8);
						}
					}
					this.Bit = bitIndex;
					this.ReadCount = (bitIndex >> 3) + 1;
				}
#else
				lock (Message.decompressor) {
					int nbits = 0;
					if ((bits&7) != 0) {
						nbits = bits&7;
						for (int i = 0; i < nbits; i++) {
							value |= (Message.decompressor.GetBit(this.Data, ref this.bit)<<i);
						}
						bits -= nbits;
					}
					if (bits != 0) {
						for (int i = 0; i < bits; i+=8) {
							int get = 0;
							Message.decompressor.OffsetReceive(ref get, this.Data, ref this.bit);
							value |= (get<<(i+nbits));
						}
					}
					this.ReadCount = (this.bit>>3)+1;
				}
#endif
		}
			if (sgn) {
				if ((value & (1 << (bits - 1))) != 0) {
					value |= -1 ^ ((1 << bits) - 1);
				}
			}
#if STRONGREADDEBUG
			string overflow = this.ReadCount > this.CurSize ? $" / overflowed:{this.ReadCount}>{this.CurSize}" : "";
			string noteString = note != null ? $" (note: {note})" : "";
			if (earlierCallingMethod != null)
			{
				doDebugLog($"{earlierCallingMethod}=>{callingMethod}=>ReadBits({obits}):{value}{noteString}{overflow}");
			}
            else
			{
				doDebugLog($"{callingMethod}=>ReadBits({obits}):{value}{noteString}{overflow}");
			}
#endif
			return value;
		}
		public int ReadByte([CallerMemberName] string callingMethod = null) {
			int c = (byte)this.ReadBits(8, callingMethod);
			if (this.ReadCount > this.CurSize) {
				c = -1;
			}
			return c;
		}
		public int ReadShort([CallerMemberName] string callingMethod = null) {
			int c = (short)this.ReadBits(16, callingMethod);
			if (this.ReadCount > this.CurSize) {
				c = -1;
			}
			return c;
		}
		public int ReadLong([CallerMemberName] string callingMethod = null) {
			int c = this.ReadBits(32, callingMethod);
			if (this.ReadCount > this.CurSize) {
				c = -1;
			}
			return c;
		}
		public sbyte []ReadString(ProtocolVersion protocol,bool forceNonScrambled = false, [CallerMemberName] string callingMethod = null) {
			bool isMOH = Common.ProtocolIsMOH(protocol);
			bool isMOHithScrambledString = isMOH && protocol > ProtocolVersion.Protocol8 && !forceNonScrambled;
			int realMaxStringChars = isMOH ? Common.MaxStringCharsMOH : Common.MaxStringChars;

			sbyte []str = new sbyte[Common.MaxStringCharsMOH];
			int l, c;
			l = 0;
			do {
				c = this.ReadByte();
				if (c == -1 ) {
					break;
				}
				if (isMOHithScrambledString)
				{
					c = NetByteToStrChar[c];
				}
				if ( c == 0) {
					break;
				}
				if (c == 37) { //'%'
					c = 46; //'.'
				}
				str[l] = (sbyte)c;
				l++;
			} while (l < sizeof(sbyte)* realMaxStringChars - 1);
			if (l <= sizeof(sbyte)* realMaxStringChars) {
				str[l] = 0;
			} else {
				str[sizeof(sbyte)* realMaxStringChars - 1] = 0;
			}
#if STRONGREADDEBUG
			doDebugLog($"{callingMethod}=>ReadString() finished: {str}");
#endif
			return str;
		}
		public string ReadStringAsString(ProtocolVersion protocol, bool forceNonScrambled = false) {
			sbyte []str = this.ReadString(protocol, forceNonScrambled);
			return Common.ToString(str);
		}
		public sbyte []ReadBigString(bool mohScrambledString, [CallerMemberName] string callingMethod = null) {
			sbyte []str = new sbyte[Common.BigInfoString];
			int l, c;
			l = 0;
			do {
				c = this.ReadByte();
				if (c == -1) {
					break;
				}
				if (mohScrambledString)
				{
					c = NetByteToStrChar[c];
				}
				if (c == 0) {
					break;
				}
				if (c == 37) { //'%'
					c = 46; //'.'
				}
				str[l] = (sbyte)c;
				l++;
			} while (l < sizeof(sbyte)*Common.BigInfoString-1);
			str[l] = 0;
#if STRONGREADDEBUG
			doDebugLog($"{callingMethod}=>ReadBigString() finished: {str}");
#endif
			return str;
		}
		public string ReadBigStringAsString(bool mohScrambledString) {
			sbyte []str = this.ReadBigString(mohScrambledString);
			return Common.ToString(str);
		}
		public sbyte []ReadStringLine(ProtocolVersion protocol, [CallerMemberName] string callingMethod = null) {

			bool isMOH = Common.ProtocolIsMOH(protocol);
			bool isMOHithScrambledString = protocol > ProtocolVersion.Protocol8;
			int realMaxStringChars = isMOH ? Common.MaxStringCharsMOH : Common.MaxStringChars;

			sbyte []str = new sbyte[Common.MaxStringCharsMOH];
			int l, c;
			l = 0;
			do {
				c = this.ReadByte();
				if (c == -1 || c == 0 || c == 10) { //'\n'
					break;
				}
				if (c == 37) { //'%'
					c = 46; //'.'
				}
				str[l] = (sbyte)c;
				l++;
			} while (l < sizeof(sbyte)* realMaxStringChars - 1);
			str[l] = 0;
#if STRONGREADDEBUG
			doDebugLog($"{callingMethod}=>ReadStringLine() finished: {str}");
#endif
			return str;
		}
		public string ReadStringLineAsString(ProtocolVersion protocol) {
			sbyte []str = this.ReadStringLine(protocol);
			return Common.ToString(str);
		}
		//we don't really need any Data in assetsless client
		public void ReadData(byte []data, int len, [CallerMemberName] string callingMethod = null) {
			for (int i = 0; i < len; i++) {
				/*data[i] = (byte)*/this.ReadByte();
			}
#if STRONGREADDEBUG
			doDebugLog($"{callingMethod}=>ReadData() finished.");
#endif
		}

		// TODO: This must be adapted to the new Q3 stuff I think, see ReadDeltaEntity.
		public unsafe void WriteDeltaEntity(EntityState* from, EntityState* to,bool force, ClientVersion version, IClientHandler clientHandler, float frameTime)
        {

			bool isMOH = clientHandler is MOHClientHandler;
			bool isMOHSpecialEntNum = isMOH && clientHandler.Protocol > (int)ProtocolVersion.Protocol8;

			ProtocolVersion protocol = (ProtocolVersion)clientHandler.Protocol;

			bool[] deltasNeeded = new bool[146]; // MOHAA

			// a NULL to is a delta remove message
			if (to == null)
			{
				if (from == null)
				{
					return;
				}
                if (isMOHSpecialEntNum)
                {

					this.WriteBits((from->Number + 1) % Common.MaxGEntities, Common.GEntitynumBits);
				} else
                {
					this.WriteBits(from->Number, Common.GEntitynumBits);
				}
				
				this.WriteBits(1, 1);
				return;
			}

			if (to->Number < 0 || to->Number >= Common.MaxGEntities)
			{
				throw new JKClientException($"Bad delta entity number: {to->Number}");
			}

			int lc = 0;
			// build the change vector as bytes so it is endien independent

			var fields = clientHandler.GetEntityStateFields();
			//if(clientHandler.Protocol != (int)ProtocolVersion.Protocol15 && clientHandler.Protocol != (int)ProtocolVersion.Protocol16 && clientHandler.Protocol != (int)ProtocolVersion.Protocol26)
            //{
			//	throw new JKClientException($"WriteDeltaEntity: Only protocols 15 and 16 (Jedi Outcast) and protocol 26 (Jedi Academy) supported right now.");
			//}

			int numFields = fields.Count;
			int* fromF, toF;
			for (int i = 0; i < numFields; i++)
			{
				fromF = (int*)((byte*)from + fields[i].Offset);
				toF = (int*)((byte*)to + fields[i].Offset);
                if (isMOH)
                {
					deltasNeeded[i] = this.DeltaNeeded(fromF, toF, (int)fields[i].Type, fields[i].Bits);
					if (deltasNeeded[i])
					{
						lc = i + 1;
					}
				} else
                {

					if (*fromF != *toF)
					{
						lc = i + 1;
					}
				}
			}

			if (lc == 0)
			{
				// nothing at all changed
				if (!force)
				{
					return;     // nothing at all
				}
				// write two bits for no change
				if (isMOHSpecialEntNum)
				{
					this.WriteBits((to->Number + 1) % Common.MaxGEntities, Common.GEntitynumBits);
				}
				else
				{
					this.WriteBits(to->Number, Common.GEntitynumBits);
				}
				this.WriteBits(0, 1);       // not removed
				this.WriteBits(0, 1);       // no delta
				return;
			}

			if (isMOHSpecialEntNum)
			{
				this.WriteBits((to->Number + 1) % Common.MaxGEntities, Common.GEntitynumBits);
			}
			else
			{
				this.WriteBits(to->Number, Common.GEntitynumBits);
			}
			this.WriteBits(0, 1);           // not removed
			this.WriteBits(1, 1);           // we have a delta

			this.WriteByte(lc); // # of changes

			//oldsize += numFields;  // ?!?!

			float fullFloat;
			int trunc;

			for (int i = 0; i < lc; i++)
			{
				//gLastField = field;
				fromF = (int*)((byte*)from + fields[i].Offset);
				toF = (int*)((byte*)to + fields[i].Offset);

				//if (*fromF == *toF)
				if ((!isMOH && *fromF == *toF) || (isMOH && !deltasNeeded[i]))
				{
					this.WriteBits( 0, 1);   // no change
					continue;
				}

				this.WriteBits( 1, 1);   // changed

                if (isMOH)
                {
					switch (fields[i].Type)
					{
						// normal style
						case NetFieldType.regular:
							this.WriteRegular(fields[i].Bits, toF, protocol);
							break;
						case NetFieldType.angle:
							this.WritePackedAngle(*(float*)toF, fields[i].Bits, protocol);
							break;
						case NetFieldType.animTime:
							this.WritePackedAnimTime(*(float*)fromF, *(float*)toF, frameTime, fields[i].Bits, protocol);
							break;
						case NetFieldType.animWeight:
							this.WritePackedAnimWeight(*(float*)toF, fields[i].Bits, protocol);
							break;
						case NetFieldType.scale:
							this.WritePackedScale(*(float*)toF, fields[i].Bits, protocol);
							break;
						case NetFieldType.alpha:
							this.WritePackedAlpha(*(float*)toF, fields[i].Bits, protocol);
							break;
						case NetFieldType.coord:
							this.WritePackedCoord(*(float*)fromF, *(float*)toF, fields[i].Bits, protocol);
							break;
						case NetFieldType.coordExtra:
							// Team Assault
							this.WritePackedCoordExtra(*(float*)fromF, *(float*)toF, fields[i].Bits, protocol);
							break;
						case NetFieldType.velocity:
							this.WritePackedVelocity(*(float*)toF, fields[i].Bits);
							break;
						case NetFieldType.simple:
							this.WritePackedSimple(*(int*)toF, fields[i].Bits);
							break;
						default:
							throw new JKClientException( $"this.WriteDeltaEntity: unrecognized entity field type {fields[i].Bits} for field {i}\n");
							break;
					}
				} else
                {
					if (fields[i].Bits == 0)
					{
						// float
						fullFloat = *(float*)toF;
						trunc = (int)fullFloat;

						if (fullFloat == 0.0f)
						{
							this.WriteBits(0, 1);
							//oldsize += FLOAT_INT_BITS;  // ??
						}
						else
						{
							this.WriteBits(1, 1);
							if (trunc == fullFloat && trunc + FloatIntBias >= 0 &&
								trunc + FloatIntBias < (1 << FloatIntBits))
							{
								// send as small integer
								this.WriteBits(0, 1);
								this.WriteBits(trunc + FloatIntBias, FloatIntBits);
							}
							else
							{
								// send as full floating point value
								this.WriteBits(1, 1);
								this.WriteBits(*toF, 32);
							}
						}
					}
					else
					{
						if (*toF == 0)
						{
							this.WriteBits(0, 1);
						}
						else
						{
							this.WriteBits(1, 1);
							// integer
							this.WriteBits(*toF, fields[i].Bits);
						}
					}
				}

				
			}

			//gLastField = &noField; // ?

		}

		public unsafe void ReadDeltaEntity(EntityState *from, EntityState *to, int number, IClientHandler clientHandler, float frameTime, ref bool fakeNonDelta, StringBuilder debugString = null, [CallerMemberName] string callingMethod = null) {
			if (number < 0 || number >= Common.MaxGEntities) {
				throw new JKClientException($"Bad delta entity number: {number}");
			}

			bool isMOH = clientHandler is MOHClientHandler;
			ProtocolVersion protocol = (ProtocolVersion)clientHandler.Protocol;

			bool print = debugString != null;

			if (this.ReadBits(1, callingMethod) == 1) {
				Common.MemSet(to, 0, sizeof(EntityState));
				to->Number = Common.MaxGEntities - 1;
                if (print)
                {
					debugString.Append($"{this.ReadCount}: #{number} remove\n");
                }
				return;
			}
			if (this.ReadBits(1, callingMethod) == 0) {
				*to = *from;
				to->Number = number;
				return;
			}


			var fields = clientHandler.GetEntityStateFields();
			int lc = this.ReadByte();

            if (print)
            {
				debugString.Append($"{this.ReadCount}: #{number} ");
			}


			if (lc < 0 || lc > fields.Count)
			{
				OnErrorMessageCreated($"EXCEPTION WILL HAPPEN: lc is {lc}, fields.Count is {fields.Count}, protocol is {protocol}, msgCursize {this.CurSize}, msgReadCount {this.ReadCount}, msgBit {this.Bit}");
			}

			to->Number = number;
			int* fromF, toF;
			int trunc;
			for (int i = 0; i < lc; i++) {
				fromF = (int*)((byte*)from + fields[i].Offset);
				toF = (int*)((byte*)to + fields[i].Offset);
				if (this.ReadBits(1, callingMethod) == 0) {
					*toF = *fromF;
				} else {

                    if (isMOH)
                    {
						// MOHAA netcode is ... very special.
						switch (fields[i].Type)
						{
							case NetFieldType.regular:
								ReadRegular(fields[i].Bits, toF, protocol);
                                if (print)
                                {
									if(fields[i].Bits == 0)
                                    {
										debugString.Append($"{fields[i].Name}:{*(float*)toF} ");
									} else
                                    {
										debugString.Append($"{fields[i].Name}:{*toF} ");
									}
                                }
								break;
							case NetFieldType.angle: // angles, what a mess! it wouldnt surprise me if something goes wrong here ;)
								*(float*)toF = ReadPackedAngle( fields[i].Bits, protocol);
								if (print)
								{
									debugString.Append($"{fields[i].Name}:{*(float*)toF} ");
								}
								break;
							case NetFieldType.animTime: // time
								*(float*)toF = ReadPackedAnimTime( fields[i].Bits, *(float*)fromF, frameTime, protocol);
								if (print)
								{
									debugString.Append($"{fields[i].Name}:{*(float*)toF} ");
								}
								break;
							case NetFieldType.animWeight: // nasty!
								*(float*)toF = ReadPackedAnimWeight( fields[i].Bits, protocol);
								if (print)
								{
									debugString.Append($"{fields[i].Name}:{*(float*)toF} ");
								}
								break;
							case NetFieldType.scale:
								*(float*)toF = ReadPackedScale( fields[i].Bits, protocol);
								if (print)
								{
									debugString.Append($"{fields[i].Name}:{*(float*)toF} ");
								}
								break;
							case NetFieldType.alpha:
								*(float*)toF = ReadPackedAlpha( fields[i].Bits, protocol);
								if (print)
								{
									debugString.Append($"{fields[i].Name}:{*(float*)toF} ");
								}
								break;
							case NetFieldType.coord:
								*(float*)toF = ReadPackedCoord( *(float*)fromF, fields[i].Bits, protocol);
								if (print)
								{
									debugString.Append($"{fields[i].Name}:{*(float*)toF} ");
								}
								break;
							case NetFieldType.coordExtra:
								*(float*)toF = ReadPackedCoordExtra( *(float*)fromF, fields[i].Bits, protocol);
								if (print)
								{
									debugString.Append($"{fields[i].Name}:{*(float*)toF} ");
								}
								break;
							case NetFieldType.velocity:
								*(float*)toF = ReadPackedVelocity( fields[i].Bits);
								if (print)
								{
									debugString.Append($"{fields[i].Name}:{*(float*)toF} ");
								}
								break;
							case NetFieldType.simple:
								*(int*)toF = ReadPackedSimple( *(int*)fromF, fields[i].Bits);
								if (print)
								{
									debugString.Append($"{fields[i].Name}:{*toF} ");
								}
								break;
							default:
								throw new JKClientException($"ReadDeltaEntity (MOH): unrecognized entity field type {i} for field\n");
								//break;
						}
					} else
                    {

						int bits = fields[i].Bits;
						if (bits == 0) {
							if (this.ReadBits(1, callingMethod) == 0) {
								*(float*)toF = 0.0f;
							} else {
								if (this.ReadBits(1, callingMethod) == 0) {
									trunc = this.ReadBits(Message.FloatIntBits, callingMethod);
									trunc -= Message.FloatIntBias;
									*(float*)toF = trunc;
									if (print)
									{
										debugString.Append($"{fields[i].Name}:{trunc} ");
									}
								} else {
									*toF = this.ReadBits(32, callingMethod);
									if (print)
									{
										debugString.Append($"{fields[i].Name}:{*(float*)toF} ");
									}
								}
							}
						} else {
							if (this.ReadBits(1, callingMethod) == 0) {
								*toF = 0;
							} else {
								*toF = this.ReadBits(bits, callingMethod);
								if (print)
								{
									debugString.Append($"{fields[i].Name}:{*toF} ");
								}
							}
						}
					}
					fields[i].Adjust?.Invoke(toF);
					if (*toF == *fromF)
					{
						// We likely received a delta snapshot from -32, which transmitted as deltaNum 0
						// So we assumed it was a non-delta but it actually wasn't.
						// It's a lame way of detecting it but what can ya do!
						fakeNonDelta = true;
						if (print)
						{
							debugString.Append($"\nES: DELTA BUT VALUE UNCHANGED: {fields[i].Name}:{*fromF} ({*(float*)fromF}) == {*toF} ({*(float*)toF}) \n");
						}
					}
				}
			}


			for (int i = lc; i < fields.Count; i++) {
				fromF = (int*)((byte*)from + fields[i].Offset);
				toF = (int*)((byte*)to + fields[i].Offset);
				*toF = *fromF;
				//fields[i].Adjust?.Invoke(toF);
			}
			if (print)
			{
				debugString.Append($"\n");
			}
		}
		public void CreateErrorMessage(string detail)
        {
			OnErrorMessageCreated($"Externally trigger message error: {detail}: msgCursize {this.CurSize}, msgReadCount {this.ReadCount}, msgBit {this.Bit}");
		}
		public unsafe void ReadDeltaPlayerstate(PlayerState *from, PlayerState *to, bool isVehicle, IClientHandler clientHandler, ref bool fakeNonDelta, StringBuilder debugString = null, [CallerMemberName] string callingMethod = null) {

			bool isMOH = clientHandler is MOHClientHandler;
			ProtocolVersion protocol = (ProtocolVersion)clientHandler.Protocol;

			GCHandle fromHandle;
			bool delta = true;
			if (from == null) {
				delta = false;
				fromHandle = GCHandle.Alloc(PlayerState.Null, GCHandleType.Pinned);
				from = (PlayerState *)fromHandle.AddrOfPinnedObject();
			} else {
				fromHandle = new GCHandle();
			}
			*to = *from;
			bool isPilot() {
				return this.ReadBits(1, callingMethod) != 0;
			}

			bool print = debugString != null;
            if (print)
            {
				debugString.Append($"{this.ReadCount}: playerstate ");
			}

			var fields = clientHandler.GetPlayerStateFields(isVehicle, isPilot);
			int lc = this.ReadByte();


			if (lc < 0 || lc > fields.Count)
			{
				OnErrorMessageCreated($"EXCEPTION WILL HAPPEN: lc is {lc}, fields.Count is {fields.Count}, protocol is {protocol}, msgCursize {this.CurSize}, msgReadCount {this.ReadCount}, msgBit {this.Bit}");
			}

			//if (lc >= fields.Count || lc < 0)
			//{
			//	throw new JKClientException($"ReadDeltaPlayerState: lc is {lc}, not between 0 and field count {fields.Count}");
			//}

			int* fromF, toF;
			int trunc;
			for (int i = 0; i < lc; i++) {
				fromF = (int*)((byte*)from + fields[i].Offset);
				toF = (int*)((byte*)to + fields[i].Offset);
				if (this.ReadBits(1, callingMethod) == 0) {
					*toF = *fromF;
				} else {
                    if (isMOH)
                    {
						switch (fields[i].Type)
						{
							case NetFieldType.regular:
								this.ReadRegularSimple( fields[i].Bits, toF, protocol);
								if (print)
								{
									if (fields[i].Bits == 0)
									{
										debugString.Append($"{fields[i].Name}:{*(float*)toF} ");
									}
									else
									{
										debugString.Append($"{fields[i].Name}:{*toF} ");
									}
								}
								break;
							case NetFieldType.angle:
								*(float*)toF = this.ReadPackedAngle( fields[i].Bits, protocol);
								if (print)
								{
									debugString.Append($"{fields[i].Name}:{*(float*)toF} ");
								}
								break;
							case NetFieldType.coord:
								*(float*)toF = this.ReadPackedCoord( *(float*)fromF, fields[i].Bits, protocol);
								if (print)
								{
									debugString.Append($"{fields[i].Name}:{*(float*)toF} ");
								}
								break;
							case NetFieldType.coordExtra:
								*(float*)toF = this.ReadPackedCoordExtra( *(float*)fromF, fields[i].Bits, protocol);
								if (print)
								{
									debugString.Append($"{fields[i].Name}:{*(float*)toF} ");
								}
								break;
							case NetFieldType.velocity:
								*(float*)toF = this.ReadPackedVelocity( fields[i].Bits);
								if (print)
								{
									debugString.Append($"{fields[i].Name}:{*(float*)toF} ");
								}
								break;
							default:
								break;
						}

					} else
                    {

						int bits = fields[i].Bits;
						if (bits == 0) {
							if (this.ReadBits(1, callingMethod, fields[i].Name) == 0) {
								trunc = this.ReadBits(Message.FloatIntBits, callingMethod);
								trunc -= Message.FloatIntBias;
								*(float*)toF = trunc;
								if (print)
								{
									debugString.Append($"{fields[i].Name}:{trunc} ");
								}
							} else {
								*toF = this.ReadBits(32, callingMethod);
								if (print)
								{
									debugString.Append($"{fields[i].Name}:{*(float*)toF} ");
								}
							}
						} else {
							*toF = this.ReadBits(bits, callingMethod, fields[i].Name); 
							if (print)
							{
								debugString.Append($"{fields[i].Name}:{*toF} ");
							}
						}
					}
					fields[i].Adjust?.Invoke(toF);
					if (delta == false && *toF == *fromF)
					{
						// We likely received a delta snapshot from -32, which transmitted as deltaNum 0
						// So we assumed it was a non-delta but it actually wasn't.
						// It's a lame way of detecting it but what can ya do!
						fakeNonDelta = true;
						if (print)
						{
							debugString.Append($"\nPS: DELTA BUT VALUE UNCHANGED: {fields[i].Name}:{*fromF} ({*(float*)fromF}) == {*toF} ({*(float*)toF}) \n");
						}
					}
				}
			}

			for (int i = lc; i < fields.Count; i++) {
				fromF = (int*)((byte*)from + fields[i].Offset);
				toF = (int*)((byte*)to + fields[i].Offset);
				*toF = *fromF;
				//fields[i].Adjust?.Invoke(toF);
			}
			int tmpValue = 0;
			if (this.ReadBits(1, callingMethod, "Stats") != 0) {
				if (this.ReadBits(1, callingMethod) != 0) {
					int bits = isMOH ? this.ReadLong(): this.ReadShort();
					int statCount = isMOH ? 32 : 16;
					for (int i = 0; i < statCount; i++) {
						if ((bits & (1<<i)) != 0) {
							if (i == 4
								&& (clientHandler.Protocol == (int)ProtocolVersion.Protocol25
								|| clientHandler.Protocol == (int)ProtocolVersion.Protocol26)) {
								tmpValue = this.ReadBits(19, callingMethod);
								if (print && (tmpValue != to->Stats[i] || !delta))
                                {
									debugString.Append($"stats[{i}]:{tmpValue} ");
								}
								to->Stats[i] = tmpValue;
							} else {
								tmpValue = this.ReadShort();
								if (print && (tmpValue != to->Stats[i] || !delta))
								{
									debugString.Append($"stats[{i}]:{tmpValue} ");
								}
								to->Stats[i] = tmpValue;
							}
						}
					}
				}
				if (isMOH) {

					int i, bits;

					// parse activeItems
					if (this.ReadBits( 1, callingMethod, "ActiveItems") != 0)
					{
						//LOG("PS_ITEMS");
						bits = this.ReadByte();
						for (i = 0; i < 8; i++)
						{
							if ((bits & (1 << i)) != 0)
							{
								to->ActiveItems[i] = this.ReadShort();
							}
						}
					}

					// parse ammo_amount
					if (this.ReadBits( 1, callingMethod, "AmmoAmount") != 0)
					{
						//LOG("PS_AMMO_AMOUNT");
						bits = this.ReadShort();
						for (i = 0; i < 16; i++)
						{
							if ((bits & (1 << i)) != 0)
							{
								to->AmmoAmount[i] = this.ReadShort();
							}
						}
					}

					// parse ammo_name_index
					if (this.ReadBits( 1, callingMethod, "AmmoNameIndex") != 0)
					{
						//LOG("PS_AMMO");
						bits = this.ReadShort();
						for (i = 0; i < 16; i++)
						{
							if ((bits & (1 << i)) != 0)
							{
								to->AmmoNameIndex[i] = this.ReadShort();
							}
						}
					}

					// parse powerups
					if (this.ReadBits( 1, callingMethod, "MaxAmmoAmount") != 0)
					{
						//LOG("PS_MAX_AMMO_AMOUNT");
						bits = this.ReadShort();
						for (i = 0; i < 16; i++)
						{
							if ((bits & (1 << i)) != 0)
							{
								to->MaxAmmoAmount[i] = this.ReadShort();
							}
						}
					}
				} else
                {

					if (this.ReadBits(1, callingMethod, "Persistant") != 0) {
						int bits = this.ReadShort();
						for (int i = 0; i < 16; i++) {
							if ((bits & (1<<i)) != 0) {
								tmpValue = this.ReadShort();
								if (print && (tmpValue != to->Persistant[i] || !delta))
								{
									debugString.Append($"persistant[{i}]:{tmpValue} ");
								}
								to->Persistant[i] = tmpValue;
							}
						}
					}
					if (this.ReadBits(1, callingMethod, "Ammo") != 0) {
						int bits = this.ReadShort();
						for (int i = 0; i < 16; i++) {
							if ((bits & (1<<i)) != 0) {
								tmpValue  = this.ReadShort();
								if (print && (tmpValue != to->Ammo[i] || !delta))
								{
									debugString.Append($"ammo[{i}]:{tmpValue} ");
								}
								to->Ammo[i] = tmpValue;
							}
						}
					}
					if (this.ReadBits(1, callingMethod, "Powerups") != 0) {
						int bits = this.ReadShort();
						for (int i = 0; i < 16; i++) {
							if ((bits & (1<<i)) != 0) {
								tmpValue = this.ReadLong();
								if (print && (tmpValue != to->PowerUps[i] || !delta))
								{
									debugString.Append($"powerups[{i}]:{tmpValue} ");
								}
								to->PowerUps[i] = tmpValue;
							}
						}
					}
				}
			}
			if (fromHandle.IsAllocated) {
				fromHandle.Free();
			}

			if (print)
			{
				debugString.Append("\n");
			}
		}

		private static readonly int []hData = new int[256]{
			250315,			// 0
			41193,			// 1
			6292,			// 2
			7106,			// 3
			3730,			// 4
			3750,			// 5
			6110,			// 6
			23283,			// 7
			33317,			// 8
			6950,			// 9
			7838,			// 10
			9714,			// 11
			9257,			// 12
			17259,			// 13
			3949,			// 14
			1778,			// 15
			8288,			// 16
			1604,			// 17
			1590,			// 18
			1663,			// 19
			1100,			// 20
			1213,			// 21
			1238,			// 22
			1134,			// 23
			1749,			// 24
			1059,			// 25
			1246,			// 26
			1149,			// 27
			1273,			// 28
			4486,			// 29
			2805,			// 30
			3472,			// 31
			21819,			// 32
			1159,			// 33
			1670,			// 34
			1066,			// 35
			1043,			// 36
			1012,			// 37
			1053,			// 38
			1070,			// 39
			1726,			// 40
			888,			// 41
			1180,			// 42
			850,			// 43
			960,			// 44
			780,			// 45
			1752,			// 46
			3296,			// 47
			10630,			// 48
			4514,			// 49
			5881,			// 50
			2685,			// 51
			4650,			// 52
			3837,			// 53
			2093,			// 54
			1867,			// 55
			2584,			// 56
			1949,			// 57
			1972,			// 58
			940,			// 59
			1134,			// 60
			1788,			// 61
			1670,			// 62
			1206,			// 63
			5719,			// 64
			6128,			// 65
			7222,			// 66
			6654,			// 67
			3710,			// 68
			3795,			// 69
			1492,			// 70
			1524,			// 71
			2215,			// 72
			1140,			// 73
			1355,			// 74
			971,			// 75
			2180,			// 76
			1248,			// 77
			1328,			// 78
			1195,			// 79
			1770,			// 80
			1078,			// 81
			1264,			// 82
			1266,			// 83
			1168,			// 84
			965,			// 85
			1155,			// 86
			1186,			// 87
			1347,			// 88
			1228,			// 89
			1529,			// 90
			1600,			// 91
			2617,			// 92
			2048,			// 93
			2546,			// 94
			3275,			// 95
			2410,			// 96
			3585,			// 97
			2504,			// 98
			2800,			// 99
			2675,			// 100
			6146,			// 101
			3663,			// 102
			2840,			// 103
			14253,			// 104
			3164,			// 105
			2221,			// 106
			1687,			// 107
			3208,			// 108
			2739,			// 109
			3512,			// 110
			4796,			// 111
			4091,			// 112
			3515,			// 113
			5288,			// 114
			4016,			// 115
			7937,			// 116
			6031,			// 117
			5360,			// 118
			3924,			// 119
			4892,			// 120
			3743,			// 121
			4566,			// 122
			4807,			// 123
			5852,			// 124
			6400,			// 125
			6225,			// 126
			8291,			// 127
			23243,			// 128
			7838,			// 129
			7073,			// 130
			8935,			// 131
			5437,			// 132
			4483,			// 133
			3641,			// 134
			5256,			// 135
			5312,			// 136
			5328,			// 137
			5370,			// 138
			3492,			// 139
			2458,			// 140
			1694,			// 141
			1821,			// 142
			2121,			// 143
			1916,			// 144
			1149,			// 145
			1516,			// 146
			1367,			// 147
			1236,			// 148
			1029,			// 149
			1258,			// 150
			1104,			// 151
			1245,			// 152
			1006,			// 153
			1149,			// 154
			1025,			// 155
			1241,			// 156
			952,			// 157
			1287,			// 158
			997,			// 159
			1713,			// 160
			1009,			// 161
			1187,			// 162
			879,			// 163
			1099,			// 164
			929,			// 165
			1078,			// 166
			951,			// 167
			1656,			// 168
			930,			// 169
			1153,			// 170
			1030,			// 171
			1262,			// 172
			1062,			// 173
			1214,			// 174
			1060,			// 175
			1621,			// 176
			930,			// 177
			1106,			// 178
			912,			// 179
			1034,			// 180
			892,			// 181
			1158,			// 182
			990,			// 183
			1175,			// 184
			850,			// 185
			1121,			// 186
			903,			// 187
			1087,			// 188
			920,			// 189
			1144,			// 190
			1056,			// 191
			3462,			// 192
			2240,			// 193
			4397,			// 194
			12136,			// 195
			7758,			// 196
			1345,			// 197
			1307,			// 198
			3278,			// 199
			1950,			// 200
			886,			// 201
			1023,			// 202
			1112,			// 203
			1077,			// 204
			1042,			// 205
			1061,			// 206
			1071,			// 207
			1484,			// 208
			1001,			// 209
			1096,			// 210
			915,			// 211
			1052,			// 212
			995,			// 213
			1070,			// 214
			876,			// 215
			1111,			// 216
			851,			// 217
			1059,			// 218
			805,			// 219
			1112,			// 220
			923,			// 221
			1103,			// 222
			817,			// 223
			1899,			// 224
			1872,			// 225
			976,			// 226
			841,			// 227
			1127,			// 228
			956,			// 229
			1159,			// 230
			950,			// 231
			7791,			// 232
			954,			// 233
			1289,			// 234
			933,			// 235
			1127,			// 236
			3207,			// 237
			1020,			// 238
			927,			// 239
			1355,			// 240
			768,			// 241
			1040,			// 242
			745,			// 243
			952,			// 244
			805,			// 245
			1073,			// 246
			740,			// 247
			1013,			// 248
			805,			// 249
			1008,			// 250
			796,			// 251
			996,			// 252
			1057,			// 253
			11457,			// 254
			13504,			// 255
		};
	}
}
