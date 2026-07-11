using TMPro;
using UnityEngine;

public sealed class DisplaySpawnedAmount : MonoBehaviour
{
    private TextMeshProUGUI _textMeshProUGUI;
    private Wizard.MiniStoneGameAuthoring _stoneGame;

    private void Awake()
    {
        _textMeshProUGUI = GetComponent<TextMeshProUGUI>();
        _stoneGame = FindObjectOfType<Wizard.MiniStoneGameAuthoring>();
    }

    private void Update()
    {
        if (_textMeshProUGUI == null || _stoneGame == null) return;
        _textMeshProUGUI.text = $"{_stoneGame.SpawnedAmount}";
    }
}
