using System;
using System.Collections.Generic;

namespace ImpossibleRobert.Common.Geometry
{
    internal enum FontOutlineFormat
    {
        Unknown,
        TrueType,
        Cff1,
        Cff2,
        ColorOnly
    }

    internal readonly struct FontVariationAxisInfo
    {
        public readonly string tag;
        public readonly float minimum;
        public readonly float defaultValue;
        public readonly float maximum;

        public FontVariationAxisInfo(string tag, float minimum, float defaultValue, float maximum)
        {
            this.tag = tag;
            this.minimum = minimum;
            this.defaultValue = defaultValue;
            this.maximum = maximum;
        }
    }

    /// <summary>
    /// Lightweight sfnt table inspection used before selecting an outline parser. Keeping this
    /// independent from either parser makes unsupported formats visible and deterministic.
    /// </summary>
    internal sealed class OpenTypeFontInfo
    {
        internal static OpenTypeFontInfo InspectSfntOffset(byte[] bytes, int offset) => InspectSfnt(bytes, offset);
        readonly FontVariationAxisInfo[] _variationAxes;

        OpenTypeFontInfo(
            FontOutlineFormat outlineFormat,
            bool hasColorTables,
            bool isVariable,
            FontVariationAxisInfo[] variationAxes,
            int fingerprint,
            int sfntOffset)
        {
            OutlineFormat = outlineFormat;
            HasColorTables = hasColorTables;
            IsVariable = isVariable;
            _variationAxes = variationAxes ?? Array.Empty<FontVariationAxisInfo>();
            Fingerprint = fingerprint;
            SfntOffset = sfntOffset;
        }

        public FontOutlineFormat OutlineFormat { get; }
        public bool HasColorTables { get; }
        public bool IsVariable { get; }
        public IReadOnlyList<FontVariationAxisInfo> VariationAxes => _variationAxes;
        public int Fingerprint { get; }

        /// <summary>
        /// Absolute byte offset of the selected face's sfnt offset table within the source data.
        /// Zero for a single-face file; non-zero when the source is a TrueType/OpenType collection
        /// (<c>ttcf</c>) and a specific face was selected.
        /// </summary>
        public int SfntOffset { get; }

        public string DisplayName
        {
            get
            {
                switch (OutlineFormat)
                {
                    case FontOutlineFormat.TrueType:
                        return IsVariable ? "TrueType variable (default instance)" : "TrueType";
                    case FontOutlineFormat.Cff1:
                        return "OpenType CFF1";
                    case FontOutlineFormat.Cff2:
                        return "OpenType CFF2 variable";
                    case FontOutlineFormat.ColorOnly:
                        return "color glyph data without monochrome outlines";
                    default:
                        return "unknown outline format";
                }
            }
        }

        public static OpenTypeFontInfo Inspect(byte[] bytes)
        {
            return Inspect(bytes, 0);
        }

        /// <summary>
        /// Inspects a single font file or one face of a TrueType/OpenType collection (<c>ttcf</c>).
        /// <paramref name="faceIndex"/> selects the face inside a collection and is ignored for a
        /// single-face file. Out-of-range indices resolve to the first face.
        /// </summary>
        public static OpenTypeFontInfo Inspect(byte[] bytes, int faceIndex, bool requireExactFace = false)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length < 12)
                throw new ArgumentException("Font data is too short to contain an sfnt header.", nameof(bytes));

            uint signature = ReadUInt32(bytes, 0);
            if (requireExactFace)
            {
                int count = signature == 0x74746366u ? CheckedUInt32ToInt(ReadUInt32(bytes, 8)) : 1;
                if (faceIndex < 0 || faceIndex >= count) throw new ArgumentOutOfRangeException(nameof(faceIndex), "The source does not contain the requested font face.");
            }
            int sfntOffset = signature == 0x74746366u // 'ttcf'
                ? ResolveCollectionFaceOffset(bytes, faceIndex)
                : 0;

            return InspectSfnt(bytes, sfntOffset);
        }

        static int ResolveCollectionFaceOffset(byte[] bytes, int faceIndex)
        {
            // TTC header: 'ttcf' (4), version (4), uint32 numFonts (4), uint32 offsetTable[numFonts].
            if (bytes.Length < 16)
                throw new ArgumentException("TrueType collection header is truncated.", nameof(bytes));

            int fontCount = CheckedUInt32ToInt(ReadUInt32(bytes, 8));
            if (fontCount <= 0)
                throw new ArgumentException("TrueType collection declares no faces.", nameof(bytes));

            long directoryEnd = 12L + (long)fontCount * 4L;
            if (directoryEnd > bytes.Length)
                throw new ArgumentException("TrueType collection face directory is truncated.", nameof(bytes));

            int selectedFace = faceIndex < 0 || faceIndex >= fontCount ? 0 : faceIndex;
            int sfntOffset = CheckedUInt32ToInt(ReadUInt32(bytes, 12 + selectedFace * 4));
            if (sfntOffset < 0 || sfntOffset > bytes.Length - 12)
                throw new ArgumentException("TrueType collection face offset is out of range.", nameof(bytes));

            return sfntOffset;
        }

        static OpenTypeFontInfo InspectSfnt(byte[] bytes, int sfntOffset)
        {
            if (sfntOffset < 0 || sfntOffset > bytes.Length - 12)
                throw new ArgumentException("Font offset table is out of range.", nameof(bytes));

            uint signature = ReadUInt32(bytes, sfntOffset);
            if (signature != 0x00010000u && signature != 0x4F54544Fu && signature != 0x74727565u)
                throw new ArgumentException("Font data is not a supported OpenType/TrueType sfnt file.", nameof(bytes));

            int tableCount = ReadUInt16(bytes, sfntOffset + 4);
            long directoryEnd = (long)sfntOffset + 12L + (long)tableCount * 16L;
            if (directoryEnd > bytes.Length)
                throw new ArgumentException("Font table directory is truncated.", nameof(bytes));

            bool hasGlyf = false;
            bool hasLoca = false;
            bool hasCff1 = false;
            bool hasCff2 = false;
            bool hasBitmapColor = false;
            bool hasLayeredColor = false;
            bool hasFvar = false;
            int fvarOffset = -1;
            int fvarLength = 0;

            for (int i = 0; i < tableCount; i++)
            {
                int recordOffset = sfntOffset + 12 + i * 16;
                string tag = ReadTag(bytes, recordOffset);
                int tableOffset = CheckedUInt32ToInt(ReadUInt32(bytes, recordOffset + 8));
                int tableLength = CheckedUInt32ToInt(ReadUInt32(bytes, recordOffset + 12));
                if (tableOffset < 0 || tableLength < 0 || tableOffset > bytes.Length - tableLength)
                    throw new ArgumentException($"Font table '{tag}' extends beyond the source data.", nameof(bytes));

                switch (tag)
                {
                    case "glyf": hasGlyf = true; break;
                    case "loca": hasLoca = true; break;
                    case "CFF ": hasCff1 = true; break;
                    case "CFF2": hasCff2 = true; break;
                    case "fvar":
                        hasFvar = true;
                        fvarOffset = tableOffset;
                        fvarLength = tableLength;
                        break;
                    case "COLR":
                    case "CPAL":
                    case "SVG ":
                        hasLayeredColor = true;
                        break;
                    case "CBDT":
                    case "CBLC":
                    case "sbix":
                        hasBitmapColor = true;
                        break;
                }
            }

            // Embedded bitmap strikes (sbix, CBDT/CBLC) hold the visible artwork while any glyf or
            // CFF table carries placeholder outlines that extrude to nothing, so a bitmap-only color
            // font is reported as having no usable monochrome outline (for example Apple Color Emoji).
            // Layered vector color (COLR, SVG) composites over real base outlines, which stay
            // extrudable, so those fonts keep their base outline format (for example Segoe UI Emoji).
            FontOutlineFormat format;
            if (hasBitmapColor && !hasLayeredColor)
                format = FontOutlineFormat.ColorOnly;
            else if (hasGlyf && hasLoca)
                format = FontOutlineFormat.TrueType;
            else if (hasCff1)
                format = FontOutlineFormat.Cff1;
            else if (hasCff2)
                format = FontOutlineFormat.Cff2;
            else if (hasLayeredColor || hasBitmapColor)
                format = FontOutlineFormat.ColorOnly;
            else
                format = FontOutlineFormat.Unknown;

            bool hasColor = hasBitmapColor || hasLayeredColor;

            FontVariationAxisInfo[] axes = hasFvar
                ? ParseVariationAxes(bytes, fvarOffset, fvarLength)
                : Array.Empty<FontVariationAxisInfo>();
            return new OpenTypeFontInfo(
                format,
                hasColor,
                hasFvar,
                axes,
                ComputeFingerprint(bytes, sfntOffset),
                sfntOffset);
        }

        static FontVariationAxisInfo[] ParseVariationAxes(byte[] bytes, int tableOffset, int tableLength)
        {
            if (tableOffset < 0 || tableLength < 16 || tableOffset > bytes.Length - tableLength)
                return Array.Empty<FontVariationAxisInfo>();

            int axesArrayOffset = ReadUInt16(bytes, tableOffset + 4);
            int axisCount = ReadUInt16(bytes, tableOffset + 8);
            int axisSize = ReadUInt16(bytes, tableOffset + 10);
            if (axisCount <= 0 || axisSize < 20)
                return Array.Empty<FontVariationAxisInfo>();

            long axesStart = (long)tableOffset + axesArrayOffset;
            long axesEnd = axesStart + (long)axisCount * axisSize;
            long tableEnd = (long)tableOffset + tableLength;
            if (axesStart < tableOffset || axesEnd > tableEnd || axesEnd > bytes.Length)
                return Array.Empty<FontVariationAxisInfo>();

            FontVariationAxisInfo[] axes = new FontVariationAxisInfo[axisCount];
            for (int i = 0; i < axisCount; i++)
            {
                int axisOffset = (int)axesStart + i * axisSize;
                axes[i] = new FontVariationAxisInfo(
                    ReadTag(bytes, axisOffset),
                    ReadFixed16_16(bytes, axisOffset + 4),
                    ReadFixed16_16(bytes, axisOffset + 8),
                    ReadFixed16_16(bytes, axisOffset + 12));
            }
            return axes;
        }

        internal static int ComputeFingerprint(byte[] bytes)
        {
            return ComputeFingerprint(bytes, 0);
        }

        /// <summary>
        /// Content-derived identifier for the source bytes, disambiguated by the selected face's
        /// sfnt offset so distinct faces of one collection never share a cache key.
        /// </summary>
        internal static int ComputeFingerprint(byte[] bytes, int sfntOffset)
        {
            unchecked
            {
                const uint offsetBasis = 2166136261u;
                const uint prime = 16777619u;
                uint hash = offsetBasis;
                int length = bytes.Length;
                hash = (hash ^ (uint)length) * prime;
                // Only disambiguate a selected collection face; a single-face file (offset 0) keeps
                // its original fingerprint so previously prepared Font3D data stays valid.
                if (sfntOffset != 0)
                    hash = (hash ^ (uint)sfntOffset) * prime;

                int sampleCount = Math.Min(4096, length);
                int step = Math.Max(1, length / Math.Max(1, sampleCount));
                for (int i = 0; i < length; i += step)
                    hash = (hash ^ bytes[i]) * prime;

                for (int i = Math.Max(0, length - 256); i < length; i++)
                    hash = (hash ^ bytes[i]) * prime;

                return (int)hash;
            }
        }

        static int CheckedUInt32ToInt(uint value)
        {
            return value > int.MaxValue ? -1 : (int)value;
        }

        static float ReadFixed16_16(byte[] bytes, int offset)
        {
            return unchecked((int)ReadUInt32(bytes, offset)) / 65536f;
        }

        static ushort ReadUInt16(byte[] bytes, int offset)
        {
            return (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
        }

        static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)((bytes[offset] << 24) |
                          (bytes[offset + 1] << 16) |
                          (bytes[offset + 2] << 8) |
                          bytes[offset + 3]);
        }

        static string ReadTag(byte[] bytes, int offset)
        {
            return new string(new[]
            {
                (char)bytes[offset],
                (char)bytes[offset + 1],
                (char)bytes[offset + 2],
                (char)bytes[offset + 3]
            });
        }
    }

}
