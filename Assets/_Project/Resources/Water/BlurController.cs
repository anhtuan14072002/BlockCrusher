///-----------------------------------------------------------------
///   Class:          BlurController
///   Description:    Created by Unity, edited by VC.
///   Author:         VueCode
///   GitHub:         https://github.com/ivuecode/
///-----------------------------------------------------------------
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class BlurController : MonoBehaviour
{
    private static readonly int BlurSizeId = Shader.PropertyToID("_BlurSize");

    [Header("Blue Settings")]
    public int iterations = 3;                   // Blur iterations - larger number means more blur.
    public float blurSpread = 0.6f;              // Blur spread for each iteration. Lower values give better looking blur.

    [Header("Camera Sync")]
    public Camera sourceCamera;
    public Canvas compositeCanvas;
    public Transform alignmentRoot;

    static Material m_Material = null;
    protected Material material { get { if (m_Material == null) { m_Material = new Material(blurShader) { hideFlags = HideFlags.DontSave }; } return m_Material; } }

    private Camera _effectCamera;

    public Shader blurShader = null;             // The blur iteration shader just takes 4 texture samples and averages them.
                                                 // By applying it repeatedly and spreading out sample locations
                                                 // we get a Gaussian blur approximation.

    private void OnEnable()
    {
        _effectCamera = GetComponent<Camera>();
    }

    private void OnPreCull()
    {
        if (sourceCamera == null)
            return;

        transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);
        _effectCamera.projectionMatrix = sourceCamera.projectionMatrix;

        if (compositeCanvas != null && alignmentRoot != null)
        {
            Vector3 alignmentPoint = alignmentRoot.position;
            int childCount = alignmentRoot.childCount;
            if (childCount > 0)
            {
                alignmentPoint = Vector3.zero;
                for (int i = 0; i < childCount; i++)
                    alignmentPoint += alignmentRoot.GetChild(i).position;
                alignmentPoint /= childCount;
            }

            float planeDistance = Vector3.Dot(
                alignmentPoint - sourceCamera.transform.position, sourceCamera.transform.forward);
            compositeCanvas.planeDistance = Mathf.Max(sourceCamera.nearClipPlane + 0.01f, planeDistance);
        }
    }


    // Performs one blur iteration.
    public void FourTapCone(RenderTexture source, RenderTexture dest, int iteration)
    {
        float off = 0.5f + iteration * blurSpread;
        material.SetFloat(BlurSizeId, off);
        Graphics.Blit(source, dest, material);
    }

    // Downsamples the texture to a quarter resolution.
    private void DownSample4x(RenderTexture source, RenderTexture dest)
    {
        material.SetFloat(BlurSizeId, 1f);
        Graphics.Blit(source, dest, material);
    }

    // Called by the camera to apply the image effect
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        int rtW = source.width / 4;
        int rtH = source.height / 4;
        RenderTexture buffer = RenderTexture.GetTemporary(rtW, rtH, 0);

        // Copy source to the 4x4 smaller texture.
        DownSample4x(source, buffer);

        // Blur the small texture
        for (int i = 0; i < iterations; i++)
        {
            RenderTexture buffer2 = RenderTexture.GetTemporary(rtW, rtH, 0);
            FourTapCone(buffer, buffer2, i);
            RenderTexture.ReleaseTemporary(buffer);
            buffer = buffer2;
        }
        Graphics.Blit(buffer, destination);
        RenderTexture.ReleaseTemporary(buffer);
    }
}
