using UnityEngine;

namespace AIBuilder.CreatureStage
{
    [ExecuteAlways, RequireComponent(typeof(LineRenderer), typeof(StageRoute))]
    public sealed class StageRouteLine : MonoBehaviour
    {
        private readonly Vector3[] positions = new Vector3[65];
        private StageRoute route;
        private LineRenderer line;
        private void Update()
        {
            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            if (route == null) route = GetComponent<StageRoute>();
            if (line == null) line = GetComponent<LineRenderer>();
            if (!route.IsUsedByStory())
            {
                if (line.positionCount != 0) line.positionCount = 0;
                return;
            }
            try
            {
                for (var i = 0; i < positions.Length; i++)
                { route.Sample((double)i / (positions.Length - 1), out positions[i], out _); positions[i].y += 0.03f; }
                line.positionCount = positions.Length;
                line.SetPositions(positions);
            }
            catch (System.InvalidOperationException) { line.positionCount = 0; }
        }
    }
}
