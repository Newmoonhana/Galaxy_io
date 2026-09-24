using System.Security.Cryptography;
using System.Text;

namespace ImpossibleRobert.Common.Geometry
{
    /// <summary>Font data is immutable; glyph IDs come from the caller's shaping backend.</summary>
    public interface ICurveFontFace
    {
        int FontId { get; }
        string ContentHash { get; }
        int UnitsPerEm { get; }
        int GlyphCount { get; }
        int SourceByteCount { get; }
        CurveGlyph GetGlyph(int glyphIndex);
        bool TryGetGlyphIndex(int codepoint, out int glyphIndex);
        float GetAdvanceWidthByGlyphIndex(int glyphIndex);
    }

    internal static class FontGeometryHash
    {
        internal static string ComputeContentHash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                StringBuilder result = new StringBuilder(64);
                foreach (byte value in hash) result.Append(value.ToString("x2"));
                return result.ToString();
            }
        }
    }
}
