using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImpossibleRobert.Common.Geometry
{
    /// <summary>Shares parsed immutable sources without keeping unused font files alive.</summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    public static partial class FontCurveSourceCache
    {
#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        static readonly object s_gate = new object();
        static readonly Dictionary<string, WeakReference<ICurveFontFace>> s_faces = new Dictionary<string, WeakReference<ICurveFontFace>>(StringComparer.Ordinal);

        /// <summary>Reads retained source and curve payloads without parsing or populating glyphs. Parser lookup structures and object overhead are excluded.</summary>
        public static void GetRetainedPayload(ICurveFontFace face, out long sourceBytes, out long curveBytes)
        {
            sourceBytes = face is TrueTypeFontFace trueType ? trueType.SourceByteCount : 0;
            curveBytes = face is TrueTypeFontFace ttf ? ttf.CachedCurveBytes : face is CffFontFace cff ? cff.CachedCurveBytes : 0;
        }

        public static ICurveFontFace GetOrCreate(byte[] bytes, int faceIndex = 0)
        {
            OpenTypeFontInfo info = OpenTypeFontInfo.Inspect(bytes, faceIndex, requireExactFace: true);
            return GetOrCreate(bytes, info);
        }

        internal static ICurveFontFace GetOrCreate(byte[] bytes, OpenTypeFontInfo info)
        {
            string key = FontGeometryHash.ComputeContentHash(bytes) + ":" + info.SfntOffset;
            lock (s_gate)
            {
                if (s_faces.TryGetValue(key, out WeakReference<ICurveFontFace> reference) && reference.TryGetTarget(out ICurveFontFace face))
                    return face;
                byte[] ownedBytes = (byte[])bytes.Clone();
                ICurveFontFace created = info.OutlineFormat switch
                {
                    FontOutlineFormat.TrueType => new TrueTypeFontFace(ownedBytes, info.SfntOffset),
                    FontOutlineFormat.Cff1 when info.SfntOffset == 0 => new CffFontFace(ownedBytes),
                    _ => throw new NotSupportedException("Canonical curves are available for TrueType and single-face CFF1 sources. Prepare a static supported font instance for this source.")
                };
                if (s_faces.Count >= 64)
                {
                    List<string> dead = new List<string>();
                    foreach (KeyValuePair<string, WeakReference<ICurveFontFace>> item in s_faces)
                        if (!item.Value.TryGetTarget(out _)) dead.Add(item.Key);
                    foreach (string stale in dead) s_faces.Remove(stale);
                }
                s_faces[key] = new WeakReference<ICurveFontFace>(created);
                return created;
            }
        }

        internal static TrueTypeFontFace GetTrueType(byte[] bytes, int sfntOffset)
        {
            OpenTypeFontInfo info = OpenTypeFontInfo.InspectSfntOffset(bytes, sfntOffset);
            return (TrueTypeFontFace)GetOrCreate(bytes, info);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { lock (s_gate) s_faces.Clear(); }
    }

    /// <summary>A font owns one bounded curve cache, independent of flattening and field density.</summary>
    internal sealed class CurveGlyphCache
    {
        const int MaximumEntries = 512;
        const long MaximumBytes = 16L * 1024 * 1024;
        readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        readonly LinkedList<int> _order = new LinkedList<int>();
        readonly object _gate = new object();
        internal long Bytes { get; private set; }

        internal long ReadPayloadBytes() { lock (_gate) return Bytes; }

        internal CurveGlyph GetOrCreate(int glyph, Func<int, CurveGlyph> create)
        {
            lock (_gate)
            {
                if (_entries.TryGetValue(glyph, out Entry entry))
                {
                    _order.Remove(entry.Node);
                    _order.AddFirst(entry.Node);
                    return entry.Glyph;
                }
                CurveGlyph value = create(glyph);
                long bytes = value?.PayloadBytes ?? 8;
                while (_entries.Count > 0 && (_entries.Count >= MaximumEntries || Bytes + bytes > MaximumBytes))
                {
                    int oldest = _order.Last.Value;
                    Bytes -= _entries[oldest].Bytes;
                    _entries.Remove(oldest);
                    _order.RemoveLast();
                }
                if (bytes <= MaximumBytes)
                {
                    _entries.Add(glyph, new Entry(value, _order.AddFirst(glyph), bytes));
                    Bytes += bytes;
                }
                return value;
            }
        }

        readonly struct Entry
        {
            internal readonly CurveGlyph Glyph;
            internal readonly LinkedListNode<int> Node;
            internal readonly long Bytes;
            internal Entry(CurveGlyph glyph, LinkedListNode<int> node, long bytes) { Glyph = glyph; Node = node; Bytes = bytes; }
        }
    }
}
