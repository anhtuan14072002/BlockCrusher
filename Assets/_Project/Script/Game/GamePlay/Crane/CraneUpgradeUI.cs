using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crusher
{
    public sealed class CraneUpgradeUI : MonoBehaviour
    {
        [SerializeField] private CraneController _craneController;
        [SerializeField] private TextMeshProUGUI _suckedBlockCountText;
        [SerializeField] private Button _increaseSawContactSpeedButton;
        [SerializeField] private Button _increaseSawScaleButton;
        [SerializeField] private Button _increaseFuelButton;

        private readonly StringBuilder _materialsText = new StringBuilder(128);

        private void Awake()
        {
            _increaseSawContactSpeedButton.onClick.AddListener(IncreaseSawContactSpeed);
            _increaseSawScaleButton.onClick.AddListener(IncreaseSawScale);
            _increaseFuelButton.onClick.AddListener(IncreaseFuelCapacity);
            ConfigureMaterialsPanel();
            RefreshMaterials();
        }

        private void OnEnable()
        {
            TextureBlockSpawner.ItemSucked += AddSuckedItem;
        }

        private void OnDisable()
        {
            TextureBlockSpawner.ItemSucked -= AddSuckedItem;
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

        private void AddSuckedItem(string _, int __)
        {
            RefreshMaterials();
        }

        private void ConfigureMaterialsPanel()
        {
            if (_suckedBlockCountText == null)
                return;

            RectTransform panel = _suckedBlockCountText.rectTransform.parent as RectTransform;
            if (panel != null)
            {
                panel.anchorMin = Vector2.right;
                panel.anchorMax = Vector2.right;
                panel.pivot = Vector2.right;
                panel.anchoredPosition = Vector2.zero;
                panel.sizeDelta = new Vector2(300f, 210f);
            }

            _suckedBlockCountText.fontSize = 28f;
            _suckedBlockCountText.fontStyle = FontStyles.Bold;
            _suckedBlockCountText.alignment = TextAlignmentOptions.TopLeft;
            _suckedBlockCountText.margin = new Vector4(16f, 12f, 16f, 12f);
            _suckedBlockCountText.raycastTarget = false;
        }

        private void RefreshMaterials()
        {
            if (_suckedBlockCountText == null)
                return;

            _materialsText.Clear();
            _materialsText.Append("NGUYEN LIEU: ").Append(TextureBlockSpawner.SuckedBlockCount);

            IReadOnlyDictionary<string, int> items = TextureBlockSpawner.SuckedItems;
            if (items.Count == 0)
            {
                _materialsText.Append("\nChua thu duoc");
            }
            else
            {
                foreach (KeyValuePair<string, int> item in items)
                {
                    _materialsText.Append('\n');
                    AppendDisplayName(item.Key);
                    _materialsText.Append(": ").Append(item.Value);
                }
            }

            _suckedBlockCountText.SetText(_materialsText);
        }

        private void AppendDisplayName(string collectibleId)
        {
            switch (collectibleId)
            {
                case "dirt": _materialsText.Append("Dat"); return;
                case "rock": _materialsText.Append("Da"); return;
                case "orange_ore": _materialsText.Append("Quang cam"); return;
                case "blue_ore": _materialsText.Append("Quang xanh"); return;
                case "purple_ore": _materialsText.Append("Quang tim"); return;
            }

            bool upperNext = true;
            for (int i = 0; i < collectibleId.Length; i++)
            {
                char character = collectibleId[i];
                if (character == '_')
                {
                    _materialsText.Append(' ');
                    upperNext = true;
                    continue;
                }

                _materialsText.Append(upperNext ? char.ToUpperInvariant(character) : character);
                upperNext = false;
            }
        }
    }
}
