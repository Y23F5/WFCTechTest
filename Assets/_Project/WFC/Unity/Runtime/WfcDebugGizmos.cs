using UnityEngine;
using WFCTechTest.WFC.Core;

namespace WFCTechTest.WFC.Unity.Runtime
{
    /// <summary>
    /// @file WfcDebugGizmos.cs
    /// @brief Draws debug markers for interest anchors and generation bounds in the scene view.
    /// </summary>
    public sealed class WfcDebugGizmos : MonoBehaviour
    {
        [SerializeField] private WfcGenerationRunner generationRunner;
        [SerializeField] private Color interestColor = new Color(0.25f, 0.95f, 0.45f);
        [SerializeField] private Color boundsColor = new Color(0.95f, 0.82f, 0.16f);

        private void OnDrawGizmos()
        {
            if (generationRunner == null || generationRunner.LastCompileResult == null)
            {
                return;
            }

            var volume = generationRunner.LastCompileResult.Volume;
            Gizmos.color = boundsColor;
            Gizmos.DrawWireCube(new Vector3((volume.Width - 1) * 0.5f, (volume.Height - 1) * 0.5f, (volume.Depth - 1) * 0.5f), new Vector3(volume.Width, volume.Height, volume.Depth));

            Gizmos.color = interestColor;
            foreach (GridCoord3D anchor in generationRunner.LastReport.InterestAnchorPositions)
            {
                Gizmos.DrawSphere(new Vector3(anchor.X + 0.5f, anchor.Y + 0.5f, anchor.Z + 0.5f), 0.35f);
            }
        }
    }
}
