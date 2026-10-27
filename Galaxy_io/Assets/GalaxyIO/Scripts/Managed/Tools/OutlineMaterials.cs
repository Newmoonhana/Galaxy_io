using System.Collections.Generic;
using UnityEngine;

namespace GalaxyIO.Tools
{
    /// <summary>
    /// 외곽선 머티리얼의 설정값별 사본 모음. 같은 원본, 같은 값은 같은 사본을 씀.
    /// </summary>
    public static class OutlineMaterials
    {
        private static readonly int SensitivityId = Shader.PropertyToID("_Sensitivity");
        private static readonly int EmissionIntensityId = Shader.PropertyToID("_EmissionIntensity");

        private static readonly Dictionary<(Material, float), Material> copies = new Dictionary<(Material, float), Material>();
        private static readonly Dictionary<(Material, float), Material> emissionCopies = new Dictionary<(Material, float), Material>();

        /// <summary>
        /// 우주선마다 다른 외곽선 감도를 쓰는 UI 외곽선 사본.
        /// </summary>
        public static Material Get(Material _source, float _sensitivity)
        {
            if (copies.TryGetValue((_source, _sensitivity), out Material copy) && copy != null) return copy;

            copy = new Material(_source) { name = $"{_source.name} ({_sensitivity})" };
            copy.SetFloat(SensitivityId, _sensitivity);
            copies[(_source, _sensitivity)] = copy;
            return copy;
        }

        /// <summary>
        /// 스프라이트 외곽선 사본. 바꾸기 전 머티리얼의 발광 세기를 이어받음. 발광이 없는 머티리얼이면 원본 외곽선을 반환.
        /// </summary>
        public static Material WithEmission(Material _outline, Material _current)
        {
            if (_current == null || _current == _outline || !_current.HasFloat(EmissionIntensityId)) return _outline;

            float intensity = _current.GetFloat(EmissionIntensityId);
            if (emissionCopies.TryGetValue((_outline, intensity), out Material copy) && copy != null) return copy;

            copy = new Material(_outline) { name = $"{_outline.name} (Emission {intensity})" };
            copy.SetFloat(EmissionIntensityId, intensity);
            emissionCopies[(_outline, intensity)] = copy;
            return copy;
        }
    }
}
