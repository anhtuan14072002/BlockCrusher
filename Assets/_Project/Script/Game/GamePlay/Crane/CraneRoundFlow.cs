using UnityEngine;
using UnityEngine.UI;

namespace Crusher
{
    public sealed class CraneRoundFlow : MonoBehaviour
    {
        public static CraneRoundFlow Instance { get; private set; }

        [SerializeField] private GameObject _popupEndRound;
        [SerializeField] private Button _continueButton;
        [SerializeField] private GameObject _funcGamePlay;
        [SerializeField] private GameObject _playGame;
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _btnBack;
        [SerializeField] private TextureBlockSpawner _blockSpawner;

        private void Awake()
        {
            Instance = this;
            _continueButton.onClick.AddListener(ContinueRound);
            _playButton.onClick.AddListener(PlayRound);
            _btnBack.onClick.AddListener(BackRound);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            _continueButton.onClick.RemoveListener(ContinueRound);
            _playButton.onClick.RemoveListener(PlayRound);
            _btnBack.onClick.RemoveListener(BackRound);
        }

        public void EndRound()
        {
            CraneController.Instance.StopRound();
            _popupEndRound.SetActive(true);
        }

        private void BackRound()
        {
            CraneController.Instance.StopRound();
            ShowUpgradeScreen();
        }

        private void ContinueRound()
        {
            _popupEndRound.SetActive(false);
            ShowUpgradeScreen();
        }

        private void PlayRound()
        {
            CraneController.Instance.StartRound();
            _funcGamePlay.SetActive(false);
            _btnBack.gameObject.SetActive(true);
            _playGame.SetActive(false);
        }

        private void ShowUpgradeScreen()
        {
            _blockSpawner.Spawn();
            _funcGamePlay.SetActive(true);
            _btnBack.gameObject.SetActive(false);
            _playGame.SetActive(true);
        }
    }
}
