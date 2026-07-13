using UnityEngine;
using UnityEngine.UI;

namespace Crusher
{
    public sealed class CraneUpgradeUI : MonoBehaviour
    {
        [SerializeField] private CraneController _craneController;
        [SerializeField] private SawBlockCutter _sawCutter;
        [SerializeField] private Text _suckedBlockCountText;
        [SerializeField] private Button _increaseSawContactSpeedButton;
        [SerializeField] private Button _increaseSawScaleButton;

        private int _displayedSuckedBlockCount = -1;

        private void Awake()
        {
            _increaseSawContactSpeedButton.onClick.AddListener(IncreaseSawContactSpeed);
            _increaseSawScaleButton.onClick.AddListener(IncreaseSawScale);
            RefreshSuckedBlockCount();
        }

        private void OnEnable()
        {
            TextureBlockSpawner.BlocksSucked += AddSuckedBlocks;
        }

        private void OnDisable()
        {
            TextureBlockSpawner.BlocksSucked -= AddSuckedBlocks;
        }

        private void Update()
        {
            if (_displayedSuckedBlockCount != TextureBlockSpawner.SuckedBlockCount)
                RefreshSuckedBlockCount();
        }

        
        
        private void IncreaseSawContactSpeed()
        {
            _craneController.IncreaseSawContactMoveMultiplier();
        }

        private void IncreaseSawScale()
        {
            _sawCutter.IncreaseSawHeadScale();
        }

        private void AddSuckedBlocks(int count)
        {
            RefreshSuckedBlockCount();
        }

        private void RefreshSuckedBlockCount()
        {
            if (_suckedBlockCountText != null)
                _suckedBlockCountText.text =  TextureBlockSpawner.SuckedBlockCount.ToString();
            _displayedSuckedBlockCount = TextureBlockSpawner.SuckedBlockCount;
        }
    }
}
