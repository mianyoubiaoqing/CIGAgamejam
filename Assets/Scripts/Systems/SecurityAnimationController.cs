using UnityEngine;

namespace CIGAgamejam
{
    public sealed class SecurityAnimationController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Animator _animatorToDisable;
        [SerializeField] private Sprite[] _idleFrames;
        [SerializeField] private Sprite[] _moveFrames;
        [SerializeField] private Sprite[] _removeToolFrames;
        [SerializeField, Min(0.1f)] private float _idleFramesPerSecond = 8f;
        [SerializeField, Min(0.1f)] private float _moveFramesPerSecond = 5f;
        [SerializeField, Min(0.1f)] private float _removeToolFramesPerSecond = 18f;
        [SerializeField, Min(0.001f)] private float _moveThreshold = 0.01f;
        [SerializeField] private bool _normalizeFrameHeights = true;
        [SerializeField, Min(0.01f)] private float _moveScaleMultiplier = 1f;

        private PrototypeSmoothMover _mover;
        private Sprite[] _currentLoopFrames;
        private float _loopTimer;
        private int _loopFrameIndex;
        private bool _isRemovingTool;
        private float _removeTimer;
        private int _removeFrameIndex;
        private Vector3 _baseScale;
        private float _referenceSpriteHeight;
        private bool _scaleReferenceReady;
        private bool _forceMoveLoop;

        private void Awake()
        {
            ResolveReferences();
            if (_animatorToDisable != null)
                _animatorToDisable.enabled = false;

            PlayLoop(_idleFrames);
        }

        private void OnEnable()
        {
            EventBus<OnSecurityRemovedTool>.Subscribe(HandleSecurityRemovedTool);
        }

        private void OnDisable()
        {
            EventBus<OnSecurityRemovedTool>.Unsubscribe(HandleSecurityRemovedTool);
        }

        private void Update()
        {
            ResolveReferences();
            if (_spriteRenderer == null)
                return;

            EnsureScaleReference();
            if (_isRemovingTool)
            {
                UpdateRemoveToolAnimation();
                return;
            }

            bool shouldPlayMove = ShouldPlayMoveLoop();
            Sprite[] frames = shouldPlayMove ? _moveFrames : _idleFrames;
            PlayLoop(frames);
            UpdateLoopAnimation(frames, shouldPlayMove ? _moveFramesPerSecond : _idleFramesPerSecond);
        }

        private void ResolveReferences()
        {
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (_animatorToDisable == null)
                _animatorToDisable = GetComponentInChildren<Animator>();
            if (_mover == null)
                _mover = GetComponent<PrototypeSmoothMover>();
        }

        private bool IsMoving()
        {
            return _mover != null
                && Vector3.Distance(transform.position, _mover.TargetPosition) > _moveThreshold;
        }

        private bool ShouldPlayMoveLoop()
        {
            return _forceMoveLoop || IsMoving();
        }

        public void SetForceMoveLoop(bool forceMoveLoop)
        {
            _forceMoveLoop = forceMoveLoop;
        }

        private void PlayLoop(Sprite[] frames)
        {
            if (frames == null || frames.Length == 0 || ReferenceEquals(_currentLoopFrames, frames))
                return;

            _currentLoopFrames = frames;
            _loopTimer = 0f;
            _loopFrameIndex = 0;
            SetFrame(frames[0], ReferenceEquals(frames, _moveFrames));
        }

        private void UpdateLoopAnimation(Sprite[] frames, float framesPerSecond)
        {
            if (frames == null || frames.Length == 0)
                return;

            _loopTimer += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(0.1f, framesPerSecond);
            while (_loopTimer >= frameDuration)
            {
                _loopTimer -= frameDuration;
                _loopFrameIndex = (_loopFrameIndex + 1) % frames.Length;
                SetFrame(frames[_loopFrameIndex], ReferenceEquals(frames, _moveFrames));
            }
        }

        private void UpdateRemoveToolAnimation()
        {
            if (_removeToolFrames == null || _removeToolFrames.Length == 0)
            {
                _isRemovingTool = false;
                return;
            }

            _removeTimer += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(0.1f, _removeToolFramesPerSecond);
            while (_removeTimer >= frameDuration)
            {
                _removeTimer -= frameDuration;
                _removeFrameIndex++;
                if (_removeFrameIndex >= _removeToolFrames.Length)
                {
                    _isRemovingTool = false;
                    PlayLoop(ShouldPlayMoveLoop() ? _moveFrames : _idleFrames);
                    return;
                }

                SetFrame(_removeToolFrames[_removeFrameIndex], false);
            }
        }

        private void HandleSecurityRemovedTool(OnSecurityRemovedTool e)
        {
            if (!isActiveAndEnabled || _spriteRenderer == null || _removeToolFrames == null || _removeToolFrames.Length == 0)
                return;

            _isRemovingTool = true;
            _removeTimer = 0f;
            _removeFrameIndex = 0;
            EnsureScaleReference();
            SetFrame(_removeToolFrames[0], false);
        }

        private void EnsureScaleReference()
        {
            if (_scaleReferenceReady || _spriteRenderer == null)
                return;

            Sprite referenceSprite = _idleFrames != null && _idleFrames.Length > 0 && _idleFrames[0] != null
                ? _idleFrames[0]
                : _spriteRenderer.sprite;
            if (referenceSprite == null || referenceSprite.bounds.size.y <= 0f)
                return;

            _baseScale = transform.localScale;
            _referenceSpriteHeight = referenceSprite.bounds.size.y;
            _scaleReferenceReady = true;
        }

        private void SetFrame(Sprite sprite, bool isMoveFrame)
        {
            if (sprite == null || _spriteRenderer == null)
                return;

            _spriteRenderer.sprite = sprite;
            if (!_normalizeFrameHeights || !_scaleReferenceReady || sprite.bounds.size.y <= 0f)
                return;

            float frameScale = _referenceSpriteHeight / sprite.bounds.size.y;
            if (isMoveFrame)
                frameScale *= _moveScaleMultiplier;

            transform.localScale = _baseScale * frameScale;
        }
    }
}
