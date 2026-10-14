using System.Collections.Generic;
using UnityEngine;

namespace GalaxyIO.Tools
{
    /// <summary>
    /// 우주선마다 다른 외곽선 감도를 쓰기 위한 머티리얼 사본 모음. 같은 원본, 같은 감도는 같은 사본을 씀.
    /// </summary>
    public static class OutlineMaterials
    {
        private static readonly int SensitivityId = Shader.PropertyToID("_Sensitivity");

        private static readonly Dictionary<(Material, float), Material> copies = new Dictionary<(Material, float), Material>();

        public static Material Get(Material _source, float _sensitivity)
        {
            if (copies.TryGetValue((_source, _sensitivity), out Material copy) && copy != null) return copy;

            copy = new Material(_source) { name = $"{_source.name} ({_sensitivity})" };
            copy.SetFloat(SensitivityId, _sensitivity);
            copies[(_source, _sensitivity)] = copy;
            return copy;
        }
    }
}
