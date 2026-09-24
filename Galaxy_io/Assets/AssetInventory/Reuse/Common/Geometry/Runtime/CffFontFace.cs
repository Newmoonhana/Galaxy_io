using System;
using System.Collections.Generic;
using System.IO;
using Typography.OpenFont;
using Typography.OpenFont.CFF;
using UnityEngine;
using Bounds = UnityEngine.Bounds;

namespace ImpossibleRobert.Common.Geometry
{
    internal sealed class CffFontFace : ICurveFontFace
    {
        readonly Typeface _typeface;
        readonly float _inverseUnitsPerEm;
        readonly CurveGlyphCache _glyphs = new CurveGlyphCache();
        internal long CachedCurveBytes => _glyphs.ReadPayloadBytes();

        readonly Func<int, CurveGlyph> _readGlyph;
        public int FontId { get; }
        public string ContentHash { get; }
        public int UnitsPerEm { get; }
        public int GlyphCount => _typeface.GlyphCount;
        public int SourceByteCount { get; }

        internal CffFontFace(byte[] bytes)
        {
            _readGlyph = ReadGlyph;
            OpenTypeFontInfo info = OpenTypeFontInfo.Inspect(bytes);
            if (info.OutlineFormat != FontOutlineFormat.Cff1 || info.SfntOffset != 0)
                throw new NotSupportedException("Canonical CFF curves require a single-face CFF1 font.");
            using (MemoryStream stream = new MemoryStream(bytes, false)) _typeface = new OpenFontReader().Read(stream);
            if (_typeface == null || !_typeface.IsCffFont) throw new ArgumentException("The CFF1 font could not be parsed.", nameof(bytes));
            FontId = info.Fingerprint;
            ContentHash = FontGeometryHash.ComputeContentHash(bytes);
            SourceByteCount = bytes.Length;
            UnitsPerEm = Math.Max(1, (int)_typeface.UnitsPerEm);
            _inverseUnitsPerEm = 1f / UnitsPerEm;
        }

        public bool TryGetGlyphIndex(int codepoint, out int glyphIndex)
        {
            glyphIndex = codepoint >= 0 && codepoint <= 0x10ffff ? _typeface.GetGlyphIndex(codepoint) : -1;
            return glyphIndex > 0;
        }

        public float GetAdvanceWidthByGlyphIndex(int glyphIndex)
        {
            return glyphIndex >= 0 && glyphIndex < GlyphCount ? _typeface.GetAdvanceWidthFromGlyphIndex((ushort)glyphIndex) * _inverseUnitsPerEm : 0;
        }

        public CurveGlyph GetGlyph(int glyphIndex)
        {
            return _glyphs.GetOrCreate(glyphIndex, _readGlyph);
        }

        CurveGlyph ReadGlyph(int glyphIndex)
        {
            if (glyphIndex < 0 || glyphIndex >= GlyphCount) return null;
            Glyph glyph = _typeface.GetGlyph((ushort)glyphIndex);
            if (glyph == null || !glyph.IsCffGlyph || glyph.GetCff1GlyphData() == null) return null;
            CurveTranslator translator = new CurveTranslator();
            new CffEvaluationEngine().Run(translator, glyph.GetCff1GlyphData(), _inverseUnitsPerEm);
            return translator.Build(GetAdvanceWidthByGlyphIndex(glyphIndex));
        }

        sealed class CurveTranslator : IGlyphTranslator
        {
            readonly List<CurveContour> _contours = new List<CurveContour>();
            readonly List<CurveSegment> _segments = new List<CurveSegment>();
            Vector2 _current;
            Vector2 _start;

            public void BeginRead(int contourCount) { _contours.Clear(); _segments.Clear(); _current = _start = Vector2.zero; }
            public void EndRead() => CloseContour();
            public void MoveTo(float x, float y) { CloseContour(); _current = _start = new Vector2(x, y); }
            public void LineTo(float x, float y)
            {
                Vector2 end = new Vector2(x, y);
                if ((end - _current).sqrMagnitude > 1e-12f) _segments.Add(CurveSegment.Line(_current, end));
                _current = end;
            }
            public void Curve3(float x1, float y1, float x2, float y2)
            {
                Vector2 end = new Vector2(x2, y2);
                _segments.Add(CurveSegment.Quadratic(_current, new Vector2(x1, y1), end));
                _current = end;
            }
            public void Curve4(float x1, float y1, float x2, float y2, float x3, float y3)
            {
                Vector2 end = new Vector2(x3, y3);
                _segments.Add(CurveSegment.Cubic(_current, new Vector2(x1, y1), new Vector2(x2, y2), end));
                _current = end;
            }
            public void CloseContour()
            {
                if (_segments.Count == 0) return;
                if ((_current - _start).sqrMagnitude > 1e-12f) _segments.Add(CurveSegment.Line(_current, _start));
                _contours.Add(new CurveContour(_segments.ToArray()));
                _segments.Clear();
            }
            internal CurveGlyph Build(float advance)
            {
                CloseContour();
                if (_contours.Count == 0) return null;
                Bounds bounds = _contours[0][0].Bounds;
                foreach (CurveContour contour in _contours)
                    for (int index = 0; index < contour.Count; index++) bounds.Encapsulate(contour[index].Bounds);
                return new CurveGlyph(_contours.ToArray(), bounds, advance);
            }
        }
    }
}
