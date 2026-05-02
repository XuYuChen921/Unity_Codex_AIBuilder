using UnityEngine;

namespace AIBuilder
{
    public sealed class RotateAroundSelf : MonoBehaviour
    {
        public float Speed = 12f;

        private void Update()
        {
            transform.Rotate(Vector3.up, Speed * Time.deltaTime, Space.World);
        }
    }
}
