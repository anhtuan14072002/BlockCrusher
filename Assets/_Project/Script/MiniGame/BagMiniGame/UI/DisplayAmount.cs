using TMPro;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
public partial class DisplayAmount : SystemBase
{
    private TextMeshProUGUI _textMeshProUGUI;
    private RectTransform _rectTransform;

    protected override void OnCreate()
    {
        base.OnCreate();
        var textGameObject = GameObject.Find("TextAmount");
        if (textGameObject != null)
        {
            _textMeshProUGUI = textGameObject.GetComponent<TextMeshProUGUI>();
        }
    }

    protected override void OnUpdate()
    {
        if (_textMeshProUGUI == null) return;
        Entities
            .WithAll<Wizard.MiniStoneComponent>()
            .ForEach((in Wizard.MiniStoneComponent amount) =>
            {
                _textMeshProUGUI.text = $"{amount.Amount}";
            }).WithoutBurst().Run();
        Entities
            .WithAll<Wizard.MiniBagComponent>()
            .ForEach((in LocalTransform localTransform) =>
            {
                _textMeshProUGUI.transform.position = localTransform.Position;
            }).WithoutBurst().Run();
        
    }
}