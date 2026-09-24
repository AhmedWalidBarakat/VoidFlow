using UnityEngine;

namespace VoidFlow
{
    // Slowly spins and bobs a decoration, like the knives and case in the start hall.
    public class Floaty : MonoBehaviour
    {
        public Vector3 spin = new(0f, 45f, 0f); // degrees per second, world axes
        public float bobHeight = 0.15f;
        public float bobSpeed = 1f;

        Vector3 basePosition;
        float phase;

        void Start()
        {
            basePosition = transform.localPosition;
            phase = Random.value * 10f;
        }

        void Update()
        {
            transform.Rotate(spin * Time.deltaTime, Space.World);
            transform.localPosition = basePosition + Vector3.up * (Mathf.Sin((Time.time + phase) * bobSpeed) * bobHeight);
        }
    }
}
