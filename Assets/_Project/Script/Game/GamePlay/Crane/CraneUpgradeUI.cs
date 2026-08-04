using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crusher
{
    public class CraneUpgradeUI : MonoBehaviour
    {
        [SerializeField] private CraneController _craneController;

        [SerializeField] private TextMeshProUGUI _countStone;
        [SerializeField] private TextMeshProUGUI _countCrystalBlue;
        [SerializeField] private TextMeshProUGUI _countCrystalPurple;
        [SerializeField] private TextMeshProUGUI _countCrystalOrange;
        [SerializeField] private TextMeshProUGUI _countCrystalWater;

        [SerializeField] private Button _increaseSawContactSpeedButton;
        [SerializeField] private Button _increaseSawScaleButton;
        [SerializeField] private Button _increaseFuelButton;

        private void Awake()
        {
            _increaseSawContactSpeedButton.onClick.AddListener(IncreaseSawContactSpeed);
            _increaseSawScaleButton.onClick.AddListener(IncreaseSawScale);
            _increaseFuelButton.onClick.AddListener(IncreaseFuelCapacity);
            RefreshMaterials();
        }

        private void OnEnable()
        {
            LevelMapSpawner.ItemSucked += AddSuckedItem;
        }

        private void OnDisable()
        {
            LevelMapSpawner.ItemSucked -= AddSuckedItem;
        }

        private void IncreaseSawContactSpeed()
        {
            _craneController.IncreaseSawContactMoveMultiplier();
        }

        private void IncreaseSawScale()
        {
            _craneController.IncreaseSawHeadScale();
        }

        private void IncreaseFuelCapacity()
        {
            _craneController.IncreaseFuelCapacity();
        }

        private void AddSuckedItem(TypeBlock _, int __)
        {
            RefreshMaterials();
        }

        private void RefreshMaterials()
        {
            _countStone.SetText("{0}", GetSuckedItemCount(TypeBlock.Rock));
            _countCrystalBlue.SetText("{0}", GetSuckedItemCount(TypeBlock.Blue_ore));
            _countCrystalPurple.SetText("{0}", GetSuckedItemCount(TypeBlock.Purple_ore));
            _countCrystalOrange.SetText("{0}", GetSuckedItemCount(TypeBlock.Orange_ore));
            _countCrystalWater.SetText("{0}", GetSuckedItemCount(TypeBlock.Water));
        }

        private static int GetSuckedItemCount(TypeBlock type)
        {
            return LevelMapSpawner.SuckedItems.TryGetValue(type, out int count) ? count : 0;
        }
    }
}
