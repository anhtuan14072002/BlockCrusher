using UnityEngine;

[DisallowMultipleComponent]
public sealed class LevelWater : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Color previousColor = Gizmos.color;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.color = new Color(0.1f, 0.65f, 1f, 0.8f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawSphere(Vector3.zero, 0.35f);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
