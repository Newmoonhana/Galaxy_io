using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ImpossibleRobert.Common.Geometry
{
    /// <summary>
    /// TTF/OTF byte-level parser that extracts glyph outlines from raw font data.
    /// Parses cmap, glyf, loca, head, hhea, hmtx, maxp, the legacy <c>kern</c> table, and the
    /// OpenType <c>GPOS</c> (Pair Adjustment) and <c>GSUB</c> (Ligature Substitution) tables.
    /// Preserves canonical quadratic segments for all geometry consumers.
    ///
    /// <para>Kerning entries from <c>kern</c> and <c>GPOS</c> are merged into a single lookup
    /// keyed by post-cmap glyph index pair. Ligature substitutions (LookupType 4) are exposed
    /// via <see cref="TryMatchLigature"/> so the shaper can replace e.g. "f i" with the "fi"
    /// glyph before geometry generation.</para>
    /// </summary>
    internal class TrueTypeFontFace : ICurveFontFace
    {
        readonly CurveGlyphCache _glyphs = new CurveGlyphCache();
        internal long CachedCurveBytes => _glyphs.ReadPayloadBytes();

        readonly Func<int, CurveGlyph> _readGlyph;
        readonly byte[] _fontBytes;
        readonly int _sfntOffset;
        readonly Dictionary<int, int> _codepointToGlyphIndex = new Dictionary<int, int>();
        // Kerning: packed key (leftGlyphIdx << 16) | rightGlyphIdx -> EM-normalized offset.
        // Negative values tighten the pair, positive loosen it. Populated from both the legacy
        // kern table and from GPOS PairAdjustment lookups (LookupType 2, Format 1 + 2).
        readonly Dictionary<int, float> _kerning = new Dictionary<int, float>();

        // GSUB ligature substitutions: input glyph sequence -> output glyph index. Stored as
        // a dictionary keyed by the first input glyph; each bucket carries the remaining tail
        // and the substitute glyph. Walks are linear (real fonts have a small set per first
        // glyph), so we avoid a multi-key hash.
        readonly Dictionary<int, List<LigatureEntry>> _ligatures = new Dictionary<int, List<LigatureEntry>>();

        struct LigatureEntry
        {
            public int[] tailGlyphs;   // glyphs that must follow the head, in order
            public int substituteGlyph;
        }

        // Table offsets
        int _glyfOffset, _locaOffset;
        int _hmtxOffset;
        bool _locaIsLong;
        int _unitsPerEm;
        int _numGlyphs;
        int _numHMetrics;
        float _invUnitsPerEm;

        /// <summary>Number of kerning pairs loaded from the font's <c>kern</c> table.</summary>
        public int KerningPairCount => _kerning.Count;

        /// <summary>
        /// Stable content-derived identifier for the loaded font bytes. Used as a cache key
        /// component so that swapping the source TTF on a live text object
        /// invalidates previously cached glyph geometry even if the font file happened to
        /// parse into the same <c>numGlyphs</c> / <c>unitsPerEm</c> values as the old one.
        /// </summary>
        public int FontId { get; }

        public string ContentHash { get; }

        /// <summary>Units per EM from the font's <c>head</c> table. Typically 1000 (OTF/CFF) or 2048 (TTF).</summary>
        public int UnitsPerEm => _unitsPerEm;

        /// <summary>Total glyph count (post-cmap) advertised by the font's <c>maxp</c> table.</summary>
        public int GlyphCount => _numGlyphs;

        public int SourceByteCount => _fontBytes.Length;

        public TrueTypeFontFace(byte[] fontBytes)
            : this(fontBytes, 0)
        {
        }

        /// <summary>
        /// Parses a single font file, or one face of a TrueType collection when
        /// <paramref name="sfntOffset"/> is the selected face's sfnt offset table position.
        /// Table records inside the directory use absolute file offsets, so only the directory
        /// base moves for a collection.
        /// </summary>
        public TrueTypeFontFace(byte[] fontBytes, int sfntOffset)
        {
            _readGlyph = index => ParseGlyph(index);
            _fontBytes = fontBytes ?? throw new ArgumentNullException(nameof(fontBytes));
            _sfntOffset = sfntOffset;
            FontId = OpenTypeFontInfo.ComputeFingerprint(fontBytes, sfntOffset);
            ContentHash = sfntOffset == 0
                ? FontGeometryHash.ComputeContentHash(fontBytes)
                : FontGeometryHash.ComputeContentHash(fontBytes) + "#" + sfntOffset.ToString(CultureInfo.InvariantCulture);
            ParseTableDirectory();
        }

        public CurveGlyph GetGlyph(int glyphIndex)
        {
            return HasGlyphIndex(glyphIndex) ? _glyphs.GetOrCreate(glyphIndex, _readGlyph) : null;
        }

        public bool HasGlyph(int codepoint)
        {
            return GetGlyphIndex(codepoint) >= 0;
        }

        public bool HasGlyphIndex(int glyphIndex)
        {
            return glyphIndex >= 0 && glyphIndex < _numGlyphs;
        }

        public bool TryGetGlyphIndex(int codepoint, out int glyphIndex)
        {
            glyphIndex = GetGlyphIndex(codepoint);
            return glyphIndex >= 0;
        }

        /// <summary>
        /// Advance width for a codepoint (e.g. 'A'). Returns 0 if the codepoint has no glyph.
        /// </summary>
        public float GetAdvanceWidth(int codepoint)
        {
            int glyphIndex = GetGlyphIndex(codepoint);
            if (glyphIndex < 0) return 0f;
            return GetAdvanceWidthByGlyphIndex(glyphIndex);
        }

        /// <summary>
        /// Advance width by raw glyph index (post-cmap). Used internally while parsing glyph
        /// tables where we already resolved the glyph index and must NOT re-run the cmap.
        /// </summary>
        public float GetAdvanceWidthByGlyphIndex(int glyphIndex)
        {
            if (!HasGlyphIndex(glyphIndex) || _hmtxOffset < 0 || _numHMetrics <= 0) return 0f;
            int metricIndex = Mathf.Min(glyphIndex, _numHMetrics - 1);
            int offset = _hmtxOffset + metricIndex * 4;
            ushort advWidth = ReadUInt16(offset);
            return advWidth * _invUnitsPerEm;
        }

        /// <summary>
        /// Horizontal kerning adjustment between two codepoints in EM-normalized units.
        /// Negative values tighten the pair (e.g. AV). Returns 0 when either glyph is
        /// missing or the pair has no kern entry.
        /// </summary>
        public float GetKerning(int leftCodepoint, int rightCodepoint)
        {
            if (_kerning.Count == 0) return 0f;
            int leftIdx = GetGlyphIndex(leftCodepoint);
            int rightIdx = GetGlyphIndex(rightCodepoint);
            if (leftIdx < 0 || rightIdx < 0) return 0f;
            int key = (leftIdx << 16) | (rightIdx & 0xFFFF);
            return _kerning.TryGetValue(key, out float offset) ? offset : 0f;
        }

        /// <summary>
        /// Enumerate all kerning pairs as (leftGlyphIndex, rightGlyphIndex, emNormalizedOffset).
        /// Glyph indices retain source-font identity independently of Unicode mapping.
        /// </summary>
        public IEnumerable<(int leftGlyphIndex, int rightGlyphIndex, float emOffset)> EnumerateKerningPairs()
        {
            foreach (var kv in _kerning)
            {
                int left = (kv.Key >> 16) & 0xFFFF;
                int right = kv.Key & 0xFFFF;
                yield return (left, right, kv.Value);
            }
        }

        /// <summary>
        /// Reverse-lookup from glyph index to one of its codepoints (the first one registered
        /// in the cmap). Returns -1 when the glyph is only referenced by compound-glyph parts.
        /// </summary>
        public int TryGetCodepointForGlyphIndex(int glyphIndex)
        {
            foreach (var kv in _codepointToGlyphIndex)
            {
                if (kv.Value == glyphIndex) return kv.Key;
            }
            return -1;
        }

        void ParseTableDirectory()
        {
            // Validate sfVersion at the selected face's offset table base.
            uint sfVersion = ReadUInt32(_sfntOffset);
            if (sfVersion != 0x00010000 && sfVersion != 0x4F54544F) // TrueType or CFF
                throw new ArgumentException("Not a valid TTF/OTF font file.");

            ushort numTables = ReadUInt16(_sfntOffset + 4);

            int headOffset = -1, cmapOffset = -1, maxpOffset = -1, hheaOffset = -1, kernOffset = -1;
            int gposOffset = -1, gsubOffset = -1;
            _glyfOffset = -1;
            _locaOffset = -1;
            _hmtxOffset = -1;

            for (int i = 0; i < numTables; i++)
            {
                int entryOffset = _sfntOffset + 12 + i * 16;
                string tag = ReadTag(entryOffset);
                int tableOffset = (int)ReadUInt32(entryOffset + 8);

                switch (tag)
                {
                    case "head": headOffset = tableOffset; break;
                    case "cmap": cmapOffset = tableOffset; break;
                    case "glyf": _glyfOffset = tableOffset; break;
                    case "loca": _locaOffset = tableOffset; break;
                    case "maxp": maxpOffset = tableOffset; break;
                    case "hhea": hheaOffset = tableOffset; break;
                    case "hmtx": _hmtxOffset = tableOffset; break;
                    case "kern": kernOffset = tableOffset; break;
                    case "GPOS": gposOffset = tableOffset; break;
                    case "GSUB": gsubOffset = tableOffset; break;
                }
            }

            if (_glyfOffset < 0 || _locaOffset < 0)
            {
                throw new NotSupportedException(
                    "TrueTypeFontFace requires TrueType glyf/loca outlines. " +
                    "Use FontOutlineProviderFactory for CFF or diagnostic routing.");
            }

            // Parse head
            if (headOffset >= 0)
            {
                _unitsPerEm = ReadUInt16(headOffset + 18);
                _invUnitsPerEm = 1f / _unitsPerEm;
                short indexToLocFormat = ReadInt16(headOffset + 50);
                _locaIsLong = indexToLocFormat == 1;
            }

            // Parse maxp
            if (maxpOffset >= 0)
                _numGlyphs = ReadUInt16(maxpOffset + 4);

            // Parse hhea
            if (hheaOffset >= 0)
                _numHMetrics = ReadUInt16(hheaOffset + 34);

            // Parse cmap
            if (cmapOffset >= 0)
                ParseCmap(cmapOffset);

            // Parse kern (optional; fonts without a kern table keep an empty dictionary)
            if (kernOffset >= 0)
                ParseKern(kernOffset);

            // Parse GPOS PairAdjustment lookups - many modern fonts (and almost all OTFs) keep
            // their kerning here rather than in the legacy kern table. We MERGE into _kerning
            // so the public API stays a single lookup; GPOS values take precedence over kern
            // for the same pair because they appear after kern in this order.
            if (gposOffset >= 0)
                TryParseGPOS(gposOffset);

            // Parse GSUB Ligature Substitution lookups (LookupType 4). Optional - fonts without
            // ligatures keep an empty _ligatures dictionary.
            if (gsubOffset >= 0)
                TryParseGSUB(gsubOffset);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GPOS PairAdjustment parsing (LookupType 2)
        // ─────────────────────────────────────────────────────────────────────

        void TryParseGPOS(int gposOffset)
        {
            try
            {
                ushort major = ReadUInt16(gposOffset);
                if (major != 1) return; // Only GPOS 1.x is widely used.
                ushort scriptListOff = ReadUInt16(gposOffset + 4); _ = scriptListOff;
                ushort featureListOff = ReadUInt16(gposOffset + 6); _ = featureListOff;
                ushort lookupListOff = ReadUInt16(gposOffset + 8);
                int lookupListBase = gposOffset + lookupListOff;
                ushort lookupCount = ReadUInt16(lookupListBase);
                for (int li = 0; li < lookupCount; li++)
                {
                    ushort lookupOff = ReadUInt16(lookupListBase + 2 + li * 2);
                    int lookupBase = lookupListBase + lookupOff;
                    ushort lookupType = ReadUInt16(lookupBase);
                    if (lookupType != 2 && lookupType != 9) continue; // 9 = extension positioning
                    ushort subTableCount = ReadUInt16(lookupBase + 4);
                    for (int s = 0; s < subTableCount; s++)
                    {
                        ushort subOff = ReadUInt16(lookupBase + 6 + s * 2);
                        int subBase = lookupBase + subOff;
                        int effectiveType = lookupType;
                        int effectiveBase = subBase;
                        if (lookupType == 9)
                        {
                            // Extension subtable header: uint16 format, uint16 extensionLookupType,
                            // uint32 extensionOffset (relative to this header).
                            ushort extType = ReadUInt16(subBase + 2);
                            uint extOff = ReadUInt32(subBase + 4);
                            if (extType != 2) continue;
                            effectiveType = 2;
                            effectiveBase = subBase + (int)extOff;
                        }
                        if (effectiveType == 2) ParseGposPairAdjustment(effectiveBase);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Font Geometry] GPOS parse failed: {ex.Message}");
            }
        }

        void ParseGposPairAdjustment(int subBase)
        {
            ushort posFormat = ReadUInt16(subBase);
            ushort coverageOff = ReadUInt16(subBase + 2);
            ushort valueFormat1 = ReadUInt16(subBase + 4);
            ushort valueFormat2 = ReadUInt16(subBase + 6);
            int[] coverage = ParseCoverage(subBase + coverageOff);
            if (coverage == null || coverage.Length == 0) return;
            int v1Size = ValueRecordSize(valueFormat1);
            int v2Size = ValueRecordSize(valueFormat2);

            if (posFormat == 1)
            {
                // Per-glyph PairSet: each first glyph in coverage has a list of second-glyph
                // pairs with positioning values. We only consume X advance on the first glyph
                // (the canonical horizontal-kerning channel).
                ushort pairSetCount = ReadUInt16(subBase + 8);
                if (pairSetCount != coverage.Length) return;
                for (int i = 0; i < pairSetCount; i++)
                {
                    ushort pairSetOff = ReadUInt16(subBase + 10 + i * 2);
                    int pairSetBase = subBase + pairSetOff;
                    ushort pairCount = ReadUInt16(pairSetBase);
                    int cursor = pairSetBase + 2;
                    int leftIdx = coverage[i];
                    int recordStride = 2 + v1Size + v2Size;
                    for (int p = 0; p < pairCount; p++)
                    {
                        ushort rightIdx = ReadUInt16(cursor);
                        short xAdvance = ExtractXAdvance(cursor + 2, valueFormat1);
                        cursor += recordStride;
                        if (xAdvance == 0) continue;
                        int key = (leftIdx << 16) | rightIdx;
                        _kerning[key] = xAdvance * _invUnitsPerEm;
                    }
                }
            }
            else if (posFormat == 2)
            {
                // Class-based: two ClassDef tables partition glyphs into classes, then a
                // class1Count × class2Count grid of value records holds the offsets.
                ushort classDef1Off = ReadUInt16(subBase + 8);
                ushort classDef2Off = ReadUInt16(subBase + 10);
                ushort class1Count = ReadUInt16(subBase + 12);
                ushort class2Count = ReadUInt16(subBase + 14);
                int classRecordSize = v1Size + v2Size;
                int gridBase = subBase + 16;

                var class1 = ParseClassDef(subBase + classDef1Off);
                var class2 = ParseClassDef(subBase + classDef2Off);
                if (class1 == null || class2 == null) return;

                // Precompute X advances per (c1, c2)
                var grid = new float[class1Count * class2Count];
                for (int c1 = 0; c1 < class1Count; c1++)
                {
                    for (int c2 = 0; c2 < class2Count; c2++)
                    {
                        int recBase = gridBase + (c1 * class2Count + c2) * classRecordSize;
                        short x = ExtractXAdvance(recBase, valueFormat1);
                        grid[c1 * class2Count + c2] = x * _invUnitsPerEm;
                    }
                }

                // For every coverage glyph (these are the left-side / class1 glyphs), and for
                // every right-side glyph found in class2, emit a kerning entry if the X advance
                // is non-zero. We restrict to glyphs that actually appear in class2 (instead of
                // looping all _numGlyphs) to avoid exploding the dictionary.
                foreach (int leftIdx in coverage)
                {
                    if (!class1.TryGetValue(leftIdx, out int c1)) c1 = 0;
                    foreach (var kv2 in class2)
                    {
                        int rightIdx = kv2.Key;
                        int c2 = kv2.Value;
                        float dx = grid[c1 * class2Count + c2];
                        if (Mathf.Approximately(dx, 0f)) continue;
                        int key = (leftIdx << 16) | rightIdx;
                        _kerning[key] = dx;
                    }
                }
            }
        }

        static int ValueRecordSize(ushort valueFormat)
        {
            int size = 0;
            // 8 bit flags: xPlacement, yPlacement, xAdvance, yAdvance, xPlaDevice, yPlaDevice,
            // xAdvDevice, yAdvDevice. Each present field contributes 2 bytes.
            for (int b = 0; b < 8; b++) if ((valueFormat & (1 << b)) != 0) size += 2;
            return size;
        }

        short ExtractXAdvance(int recordOffset, ushort valueFormat)
        {
            // Field order within the record matches the bit order above. We need only
            // bit 2 (xAdvance) for horizontal kerning. Walk preceding bits to compute the
            // offset of the xAdvance int16 inside the record.
            int off = 0;
            if ((valueFormat & 0x0001) != 0) off += 2; // xPlacement
            if ((valueFormat & 0x0002) != 0) off += 2; // yPlacement
            if ((valueFormat & 0x0004) == 0) return 0; // xAdvance not present
            return ReadInt16(recordOffset + off);
        }

        int[] ParseCoverage(int covBase)
        {
            ushort format = ReadUInt16(covBase);
            if (format == 1)
            {
                ushort glyphCount = ReadUInt16(covBase + 2);
                var result = new int[glyphCount];
                for (int i = 0; i < glyphCount; i++)
                    result[i] = ReadUInt16(covBase + 4 + i * 2);
                return result;
            }
            if (format == 2)
            {
                ushort rangeCount = ReadUInt16(covBase + 2);
                var list = new List<int>(rangeCount * 4);
                for (int r = 0; r < rangeCount; r++)
                {
                    int rBase = covBase + 4 + r * 6;
                    ushort startGlyph = ReadUInt16(rBase);
                    ushort endGlyph = ReadUInt16(rBase + 2);
                    for (int g = startGlyph; g <= endGlyph; g++) list.Add(g);
                }
                return list.ToArray();
            }
            return Array.Empty<int>();
        }

        Dictionary<int, int> ParseClassDef(int classBase)
        {
            ushort format = ReadUInt16(classBase);
            var result = new Dictionary<int, int>();
            if (format == 1)
            {
                ushort startGlyph = ReadUInt16(classBase + 2);
                ushort glyphCount = ReadUInt16(classBase + 4);
                for (int i = 0; i < glyphCount; i++)
                {
                    ushort cls = ReadUInt16(classBase + 6 + i * 2);
                    if (cls != 0) result[startGlyph + i] = cls;
                }
            }
            else if (format == 2)
            {
                ushort rangeCount = ReadUInt16(classBase + 2);
                for (int r = 0; r < rangeCount; r++)
                {
                    int rBase = classBase + 4 + r * 6;
                    ushort startGlyph = ReadUInt16(rBase);
                    ushort endGlyph = ReadUInt16(rBase + 2);
                    ushort cls = ReadUInt16(rBase + 4);
                    if (cls == 0) continue;
                    for (int g = startGlyph; g <= endGlyph; g++) result[g] = cls;
                }
            }
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GSUB Ligature Substitution parsing (LookupType 4)
        // ─────────────────────────────────────────────────────────────────────

        void TryParseGSUB(int gsubOffset)
        {
            try
            {
                ushort major = ReadUInt16(gsubOffset);
                if (major != 1) return;
                ushort lookupListOff = ReadUInt16(gsubOffset + 8);
                int lookupListBase = gsubOffset + lookupListOff;
                ushort lookupCount = ReadUInt16(lookupListBase);
                for (int li = 0; li < lookupCount; li++)
                {
                    ushort lookupOff = ReadUInt16(lookupListBase + 2 + li * 2);
                    int lookupBase = lookupListBase + lookupOff;
                    ushort lookupType = ReadUInt16(lookupBase);
                    if (lookupType != 4 && lookupType != 7) continue;
                    ushort subTableCount = ReadUInt16(lookupBase + 4);
                    for (int s = 0; s < subTableCount; s++)
                    {
                        ushort subOff = ReadUInt16(lookupBase + 6 + s * 2);
                        int subBase = lookupBase + subOff;
                        int effectiveType = lookupType;
                        int effectiveBase = subBase;
                        if (lookupType == 7)
                        {
                            ushort extType = ReadUInt16(subBase + 2);
                            uint extOff = ReadUInt32(subBase + 4);
                            if (extType != 4) continue;
                            effectiveType = 4;
                            effectiveBase = subBase + (int)extOff;
                        }
                        if (effectiveType == 4) ParseGsubLigatureSubst(effectiveBase);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Font Geometry] GSUB parse failed: {ex.Message}");
            }
        }

        void ParseGsubLigatureSubst(int subBase)
        {
            ushort substFormat = ReadUInt16(subBase);
            if (substFormat != 1) return;
            ushort coverageOff = ReadUInt16(subBase + 2);
            ushort ligSetCount = ReadUInt16(subBase + 4);
            int[] coverage = ParseCoverage(subBase + coverageOff);
            if (coverage == null || coverage.Length != ligSetCount) return;

            for (int i = 0; i < ligSetCount; i++)
            {
                ushort ligSetOff = ReadUInt16(subBase + 6 + i * 2);
                int ligSetBase = subBase + ligSetOff;
                ushort ligCount = ReadUInt16(ligSetBase);
                int firstGlyph = coverage[i];
                if (!_ligatures.TryGetValue(firstGlyph, out var bucket))
                {
                    bucket = new List<LigatureEntry>(ligCount);
                    _ligatures[firstGlyph] = bucket;
                }
                for (int l = 0; l < ligCount; l++)
                {
                    ushort ligOff = ReadUInt16(ligSetBase + 2 + l * 2);
                    int ligBase = ligSetBase + ligOff;
                    ushort substGlyph = ReadUInt16(ligBase);
                    ushort compCount = ReadUInt16(ligBase + 2);
                    // compCount includes the head glyph that's already matched via coverage,
                    // so the tail length is compCount - 1.
                    int tailLen = Mathf.Max(0, compCount - 1);
                    var tail = new int[tailLen];
                    for (int c = 0; c < tailLen; c++)
                        tail[c] = ReadUInt16(ligBase + 4 + c * 2);
                    bucket.Add(new LigatureEntry { tailGlyphs = tail, substituteGlyph = substGlyph });
                }
                // Longest-first ordering simplifies the greedy match in TryMatchLigature.
                bucket.Sort((a, b) => b.tailGlyphs.Length.CompareTo(a.tailGlyphs.Length));
            }
        }

        /// <summary>Number of glyph entries with at least one ligature substitution.</summary>
        public int LigatureHeadCount => _ligatures.Count;

        /// <summary>
        /// Greedy ligature match starting at <paramref name="startIndex"/> in the given
        /// codepoint sequence. If a ligature applies, returns the substitute glyph index and
        /// sets <paramref name="consumed"/> to the number of codepoints replaced. Otherwise
        /// returns -1 and <paramref name="consumed"/>=1 so callers can advance one codepoint.
        /// </summary>
        public int TryMatchLigature(int[] codepoints, int startIndex, out int consumed)
        {
            consumed = 1;
            if (codepoints == null || startIndex < 0 || startIndex >= codepoints.Length) return -1;
            int headGlyph = GetGlyphIndex(codepoints[startIndex]);
            if (headGlyph < 0) return -1;
            if (!_ligatures.TryGetValue(headGlyph, out var bucket)) return -1;
            for (int e = 0; e < bucket.Count; e++)
            {
                var entry = bucket[e];
                int needed = entry.tailGlyphs.Length;
                if (startIndex + 1 + needed > codepoints.Length) continue;
                bool match = true;
                for (int t = 0; t < needed; t++)
                {
                    int tg = GetGlyphIndex(codepoints[startIndex + 1 + t]);
                    if (tg != entry.tailGlyphs[t]) { match = false; break; }
                }
                if (match)
                {
                    consumed = 1 + needed;
                    return entry.substituteGlyph;
                }
            }
            return -1;
        }

        /// <summary>
        /// Parse the legacy <c>kern</c> table. Only format 0 horizontal kerning subtables are
        /// read; other formats (1-3) and vertical kerning are skipped gracefully. Entries with
        /// a zero offset are dropped to keep the dictionary tight.
        /// </summary>
        void ParseKern(int kernOffset)
        {
            ushort version = ReadUInt16(kernOffset);
            // Apple's extended kern (version 1.0 fixed = 0x00010000) has a different header
            // and stores subtables differently. We only support Microsoft/OT's version 0 here.
            if (version != 0) return;

            ushort nTables = ReadUInt16(kernOffset + 2);
            int cursor = kernOffset + 4;

            for (int t = 0; t < nTables; t++)
            {
                // Subtable header: uint16 version, uint16 length, uint16 coverage
                ushort subVersion = ReadUInt16(cursor);
                ushort subLength = ReadUInt16(cursor + 2);
                ushort coverage = ReadUInt16(cursor + 4);
                int subStart = cursor;
                cursor += subLength;

                if (subVersion != 0) continue; // Older Mac v1.0 extensions: skip

                // Coverage bits: 0=horizontal, 1=minimum (not offset), 2=cross-stream, 3=override
                // Format is the high byte. We only handle horizontal format-0 offset tables.
                int format = coverage >> 8;
                bool horizontal = (coverage & 0x01) != 0;
                bool minimum = (coverage & 0x02) != 0;
                if (format != 0 || !horizontal || minimum) continue;

                int pairsHeader = subStart + 6;
                ushort nPairs = ReadUInt16(pairsHeader);
                int pairsCursor = pairsHeader + 8; // Skip searchRange/entrySelector/rangeShift

                for (int p = 0; p < nPairs; p++)
                {
                    ushort leftIdx = ReadUInt16(pairsCursor);
                    ushort rightIdx = ReadUInt16(pairsCursor + 2);
                    short value = ReadInt16(pairsCursor + 4);
                    pairsCursor += 6;

                    if (value == 0) continue;
                    int key = (leftIdx << 16) | rightIdx;
                    // Last-write wins when multiple subtables define the same pair.
                    _kerning[key] = value * _invUnitsPerEm;
                }
            }
        }

        void ParseCmap(int cmapOffset)
        {
            ushort numSubtables = ReadUInt16(cmapOffset + 2);

            // First pass: prefer full-Unicode (format 12) tables.
            for (int i = 0; i < numSubtables; i++)
            {
                int subtableEntry = cmapOffset + 4 + i * 8;
                ushort platformId = ReadUInt16(subtableEntry);
                ushort encodingId = ReadUInt16(subtableEntry + 2);
                int subtableOffset = cmapOffset + (int)ReadUInt32(subtableEntry + 4);

                // Platform 3 / encoding 10 = full Unicode; Platform 0 / encoding 4 or 6 = full Unicode.
                bool fullUnicode = (platformId == 3 && encodingId == 10) ||
                                   (platformId == 0 && (encodingId == 4 || encodingId == 6));
                if (!fullUnicode) continue;

                ushort format = ReadUInt16(subtableOffset);
                if (format == 12)
                {
                    ParseCmapFormat12(subtableOffset);
                    return;
                }
            }

            // Second pass: BMP (format 4).
            for (int i = 0; i < numSubtables; i++)
            {
                int subtableEntry = cmapOffset + 4 + i * 8;
                ushort platformId = ReadUInt16(subtableEntry);
                ushort encodingId = ReadUInt16(subtableEntry + 2);
                int subtableOffset = cmapOffset + (int)ReadUInt32(subtableEntry + 4);

                if ((platformId == 3 && encodingId == 1) || platformId == 0)
                {
                    ushort format = ReadUInt16(subtableOffset);
                    if (format == 4)
                    {
                        ParseCmapFormat4(subtableOffset);
                        return;
                    }
                }
            }
        }

        void ParseCmapFormat12(int offset)
        {
            // offset: uint16 format, uint16 reserved, uint32 length, uint32 language, uint32 numGroups
            uint numGroups = ReadUInt32(offset + 12);
            int groupOffset = offset + 16;
            for (uint g = 0; g < numGroups; g++)
            {
                uint startCharCode = ReadUInt32(groupOffset);
                uint endCharCode = ReadUInt32(groupOffset + 4);
                uint startGlyphId = ReadUInt32(groupOffset + 8);
                groupOffset += 12;

                for (uint c = startCharCode; c <= endCharCode; c++)
                {
                    int glyphIndex = (int)(startGlyphId + (c - startCharCode));
                    if (glyphIndex > 0 && !_codepointToGlyphIndex.ContainsKey((int)c))
                        _codepointToGlyphIndex[(int)c] = glyphIndex;
                }
            }
        }

        void ParseCmapFormat4(int offset)
        {
            ushort segCountX2 = ReadUInt16(offset + 6);
            int segCount = segCountX2 / 2;

            int endCodesOffset = offset + 14;
            int startCodesOffset = endCodesOffset + segCountX2 + 2;
            int idDeltaOffset = startCodesOffset + segCountX2;
            int idRangeOffset = idDeltaOffset + segCountX2;

            for (int i = 0; i < segCount; i++)
            {
                ushort endCode = ReadUInt16(endCodesOffset + i * 2);
                ushort startCode = ReadUInt16(startCodesOffset + i * 2);
                short idDelta = ReadInt16(idDeltaOffset + i * 2);
                ushort idRange = ReadUInt16(idRangeOffset + i * 2);

                for (int c = startCode; c <= endCode; c++)
                {
                    int glyphIndex;
                    if (idRange == 0)
                    {
                        glyphIndex = (c + idDelta) & 0xFFFF;
                    }
                    else
                    {
                        int glyphIdArrayOffset = idRangeOffset + i * 2 + idRange + (c - startCode) * 2;
                        glyphIndex = ReadUInt16(glyphIdArrayOffset);
                        if (glyphIndex != 0)
                            glyphIndex = (glyphIndex + idDelta) & 0xFFFF;
                    }

                    if (glyphIndex > 0 && !_codepointToGlyphIndex.ContainsKey(c))
                        _codepointToGlyphIndex[c] = glyphIndex;
                }
            }
        }

        int GetGlyphIndex(int codepoint)
        {
            return _codepointToGlyphIndex.TryGetValue(codepoint, out int idx) ? idx : -1;
        }

        int GetGlyphOffset(int glyphIndex)
        {
            if (_locaOffset < 0 || _glyfOffset < 0) return -1;

            if (_locaIsLong)
                return _glyfOffset + (int)ReadUInt32(_locaOffset + glyphIndex * 4);
            else
                return _glyfOffset + ReadUInt16(_locaOffset + glyphIndex * 2) * 2;
        }

        CurveGlyph ParseGlyph(int glyphIndex, int depth = 0)
        {
            if (depth > 16) throw new InvalidOperationException("Compound glyph nesting exceeded 16 levels.");
            int glyphOffset = GetGlyphOffset(glyphIndex);
            int glyphEnd = GetGlyphOffset(glyphIndex + 1);
            if (glyphOffset < 0 || glyphEnd <= glyphOffset) return null;
            short count = ReadInt16(glyphOffset);
            return count < 0 ? ParseCompoundGlyph(glyphIndex, glyphOffset, depth) : ParseSimpleGlyph(glyphOffset, count, glyphIndex);
        }

        CurveGlyph ParseSimpleGlyph(int offset, int numberOfContours, int glyphIndex)
        {
            if (numberOfContours == 0) return null;

            // Read bounding box
            short xMin = ReadInt16(offset + 2);
            short yMin = ReadInt16(offset + 4);
            short xMax = ReadInt16(offset + 6);
            short yMax = ReadInt16(offset + 8);

            // Read end points of each contour
            int endPointsOffset = offset + 10;
            ushort[] endPoints = new ushort[numberOfContours];
            for (int i = 0; i < numberOfContours; i++)
                endPoints[i] = ReadUInt16(endPointsOffset + i * 2);

            int numPoints = endPoints[numberOfContours - 1] + 1;

            // Skip instructions
            int instructionLengthOffset = endPointsOffset + numberOfContours * 2;
            ushort instructionLength = ReadUInt16(instructionLengthOffset);
            int flagsOffset = instructionLengthOffset + 2 + instructionLength;

            // Read flags
            byte[] flags = new byte[numPoints];
            int flagPos = flagsOffset;
            for (int i = 0; i < numPoints; i++)
            {
                flags[i] = _fontBytes[flagPos++];
                if ((flags[i] & 0x08) != 0) // Repeat flag
                {
                    byte repeatCount = _fontBytes[flagPos++];
                    for (int r = 0; r < repeatCount && i + 1 < numPoints; r++)
                    {
                        i++;
                        flags[i] = flags[i - 1];
                    }
                }
            }

            // Read X coordinates
            float[] xCoords = new float[numPoints];
            int xPos = flagPos;
            float xAccum = 0;
            for (int i = 0; i < numPoints; i++)
            {
                bool xShort = (flags[i] & 0x02) != 0;
                bool xSame = (flags[i] & 0x10) != 0;

                if (xShort)
                {
                    float dx = _fontBytes[xPos++];
                    xAccum += xSame ? dx : -dx;
                }
                else if (!xSame)
                {
                    xAccum += ReadInt16(xPos);
                    xPos += 2;
                }
                xCoords[i] = xAccum * _invUnitsPerEm;
            }

            // Read Y coordinates
            float[] yCoords = new float[numPoints];
            int yPos = xPos;
            float yAccum = 0;
            for (int i = 0; i < numPoints; i++)
            {
                bool yShort = (flags[i] & 0x04) != 0;
                bool ySame = (flags[i] & 0x20) != 0;

                if (yShort)
                {
                    float dy = _fontBytes[yPos++];
                    yAccum += ySame ? dy : -dy;
                }
                else if (!ySame)
                {
                    yAccum += ReadInt16(yPos);
                    yPos += 2;
                }
                yCoords[i] = yAccum * _invUnitsPerEm;
            }

            List<CurveContour> contours = new List<CurveContour>();
            Vector2[] sourcePoints = new Vector2[numPoints];
            for (int index = 0; index < numPoints; index++) sourcePoints[index] = new Vector2(xCoords[index], yCoords[index]);
            int startPoint = 0;
            for (int contour = 0; contour < numberOfContours; contour++)
            {
                int endPoint = endPoints[contour];
                int length = endPoint - startPoint + 1;
                bool firstOn = (flags[startPoint] & 1) != 0;
                bool lastOn = (flags[endPoint] & 1) != 0;
                Vector2 first = firstOn ? sourcePoints[startPoint] : lastOn ? sourcePoints[endPoint] : (sourcePoints[startPoint] + sourcePoints[endPoint]) * .5f;
                Vector2 current = first;
                List<CurveSegment> segments = new List<CurveSegment>();
                for (int index = firstOn ? 1 : 0; index < length; index++)
                {
                    int point = startPoint + index;
                    Vector2 position = sourcePoints[point];
                    if ((flags[point] & 1) != 0)
                    {
                        if ((position - current).sqrMagnitude > 1e-12f) segments.Add(CurveSegment.Line(current, position));
                        current = position;
                    }
                    else
                    {
                        int next = startPoint + (index + 1) % length;
                        bool nextOn = (flags[next] & 1) != 0;
                        Vector2 end = nextOn ? sourcePoints[next] : (position + sourcePoints[next]) * .5f;
                        segments.Add(CurveSegment.Quadratic(current, position, end));
                        current = end;
                        if (nextOn) index++;
                    }
                }
                if ((current - first).sqrMagnitude > 1e-12f) segments.Add(CurveSegment.Line(current, first));
                if (segments.Count > 0) contours.Add(new CurveContour(segments.ToArray()));
                startPoint = endPoint + 1;
            }
            return new CurveGlyph(contours.ToArray(), GlyphBounds(offset), GetAdvanceWidthByGlyphIndex(glyphIndex), sourcePoints);
        }

        CurveGlyph ParseCompoundGlyph(int glyphIndex, int offset, int depth)
        {
            List<CurveContour> contours = new List<CurveContour>();
            List<Vector2> points = new List<Vector2>();
            int position = offset + 10;
            int iterations = 0;
            ushort flags;
            do
            {
                if (++iterations > 256) throw new InvalidOperationException("Compound glyph exceeded 256 components.");
                flags = ReadUInt16(position);
                int componentId = ReadUInt16(position + 2);
                position += 4;
                bool xy = (flags & 2) != 0;
                int first, second;
                if ((flags & 1) != 0)
                {
                    first = xy ? ReadInt16(position) : ReadUInt16(position);
                    second = xy ? ReadInt16(position + 2) : ReadUInt16(position + 2);
                    position += 4;
                }
                else
                {
                    first = xy ? (sbyte)_fontBytes[position] : _fontBytes[position];
                    second = xy ? (sbyte)_fontBytes[position + 1] : _fontBytes[position + 1];
                    position += 2;
                }
                Matrix4x4 matrix = Matrix4x4.identity;
                if ((flags & 8) != 0) { matrix.m00 = matrix.m11 = ReadF2Dot14(position); position += 2; }
                else if ((flags & 64) != 0) { matrix.m00 = ReadF2Dot14(position); matrix.m11 = ReadF2Dot14(position + 2); position += 4; }
                else if ((flags & 128) != 0)
                {
                    matrix.m00 = ReadF2Dot14(position); matrix.m01 = ReadF2Dot14(position + 2);
                    matrix.m10 = ReadF2Dot14(position + 4); matrix.m11 = ReadF2Dot14(position + 6); position += 8;
                }
                CurveGlyph component = ParseGlyph(componentId, depth + 1);
                if (component == null) continue;
                Vector2 translation;
                if (xy)
                {
                    translation = new Vector2(first, second) * _invUnitsPerEm;
                    if ((flags & 2048) != 0) translation = matrix.MultiplyVector(translation);
                }
                else
                {
                    if (first < 0 || first >= points.Count || second < 0 || second >= component.SourcePoints.Length)
                        throw new InvalidOperationException("Compound glyph contains an invalid attachment point.");
                    translation = points[first] - (Vector2)matrix.MultiplyPoint3x4(component.SourcePoints[second]);
                }
                matrix.m03 = translation.x; matrix.m13 = translation.y;
                for (int contour = 0; contour < component.ContourCount; contour++) contours.Add(component.GetContour(contour).Transform(matrix));
                foreach (Vector2 point in component.SourcePoints) points.Add(matrix.MultiplyPoint3x4(point));
            } while ((flags & 32) != 0);
            return new CurveGlyph(contours.ToArray(), GlyphBounds(offset), GetAdvanceWidthByGlyphIndex(glyphIndex), points.ToArray());
        }

        Bounds GlyphBounds(int offset)
        {
            float left = ReadInt16(offset + 2) * _invUnitsPerEm, bottom = ReadInt16(offset + 4) * _invUnitsPerEm;
            float right = ReadInt16(offset + 6) * _invUnitsPerEm, top = ReadInt16(offset + 8) * _invUnitsPerEm;
            return new Bounds(new Vector3((left + right) * .5f, (bottom + top) * .5f, 0), new Vector3(right - left, top - bottom, 0));
        }

        // Binary readers (big-endian)
        ushort ReadUInt16(int offset) => (ushort)((_fontBytes[offset] << 8) | _fontBytes[offset + 1]);
        short ReadInt16(int offset) => (short)((_fontBytes[offset] << 8) | _fontBytes[offset + 1]);
        uint ReadUInt32(int offset) => (uint)((_fontBytes[offset] << 24) | (_fontBytes[offset + 1] << 16) | (_fontBytes[offset + 2] << 8) | _fontBytes[offset + 3]);

        string ReadTag(int offset)
        {
            return new string(new[]
            {
                (char)_fontBytes[offset],
                (char)_fontBytes[offset + 1],
                (char)_fontBytes[offset + 2],
                (char)_fontBytes[offset + 3]
            });
        }

        float ReadF2Dot14(int offset)
        {
            short raw = ReadInt16(offset);
            return raw / 16384f;
        }
    }
}
