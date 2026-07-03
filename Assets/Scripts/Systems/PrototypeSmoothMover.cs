using UnityEngine;

namespace CIGAgamejam
{
    public sealed class PrototypeSmoothMover : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float _moveSpeed = 5f;

        private Vector3 _targetPosition;

        public Vector3 TargetPosition => _targetPosition;

        private void Awake()
        {
            _targetPosition = transform.position;
        }

        private void Update()
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                _targetPosition,
                _moveSpeed * Time.deltaTime);
        }

        public void SetTarget(Vector3 targetPosition)
        {
            _targetPosition = targetPosition;
        }

        public void SetMoveSpeed(float moveSpeed)
        {
            _moveSpeed = Mathf.Max(0.1f, moveSpeed);
        }
    }
}
