using GalaxyIO.DOTS.Client.Networking;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    public sealed class GameStartBackend : MonoBehaviour
    {
        [SerializeField]
        public GameObject Screen;

        public void OnPressGameStart()
        {
            if (ClientNetworkRequestManager.RequestPlayerSpawn())
            {
                Screen.SetActive(false);
            }
        }
    }
}