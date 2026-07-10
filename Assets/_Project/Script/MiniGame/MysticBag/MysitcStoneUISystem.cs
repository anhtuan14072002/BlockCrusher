using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public partial class MysticStoneUISystem : SystemBase
{
    private TextMeshProUGUI _textMeshProUGUI;

    protected override void OnCreate()
    {
        base.OnCreate();

        var textGameObject = GameObject.Find("MysticStoneText");
        if (textGameObject != null)
        {
            _textMeshProUGUI = textGameObject.GetComponent<TextMeshProUGUI>();
        }
    }

    protected override void OnUpdate()
    {
        if (_textMeshProUGUI == null) return;
        Entities
            .WithAll<Wizard.MysticBagComponent>()
            .ForEach((in Wizard.MysticBagComponent mysticBag, in LocalTransform localTransform) =>
            {
                _textMeshProUGUI.text = $" {mysticBag.MysticStone}";
                _textMeshProUGUI.transform.position = localTransform.Position + new float3(0,0.3f,0);
            }).WithoutBurst().Run();
    }
}