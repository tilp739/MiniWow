using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniWow.Client.Gameplay
{
    /// <summary>
    /// Caméra à la troisième personne, derrière le joueur, dans le style des MMORPG classiques :
    /// clic droit maintenu pour tourner autour du personnage, molette pour zoomer.
    /// Purement visuelle : n'influence pas le gameplay, sauf par la direction de déplacement relative à la vue.
    /// </summary>
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        private const float MouseSensitivity = 0.25f;
        private const float ZoomStep = 1f;

        [SerializeField] private Transform _target;
        [SerializeField] private float _distance = 9f;
        [SerializeField] private float _minDistance = 3f;
        [SerializeField] private float _maxDistance = 20f;
        [SerializeField] private float _pivotHeight = 1.8f;
        [SerializeField] private float _minPitch = 5f;
        [SerializeField] private float _maxPitch = 70f;
        [SerializeField] private float _smoothing = 20f;
        [SerializeField] private bool _invertMouseY = true;

        private float _yaw;
        private float _pitch = 20f;
        private Camera _camera;

        /// <summary>Direction horizontale « devant » la caméra : base des déplacements du joueur.</summary>
        public Vector3 FlatForward => Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;

        /// <summary>Direction horizontale « à droite » de la caméra.</summary>
        public Vector3 FlatRight => Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;

        public Camera Camera => _camera;

        public void SetTarget(Transform target)
        {
            _target = target;
            if (_target != null)
            {
                _yaw = _target.eulerAngles.y;
            }
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void Start()
        {
            if (_target != null)
            {
                _yaw = _target.eulerAngles.y;
            }
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    _yaw += delta.x * MouseSensitivity;
                    float pitchDelta = _invertMouseY ? -delta.y : delta.y;
                    _pitch = Mathf.Clamp(_pitch + pitchDelta * MouseSensitivity, _minPitch, _maxPitch);
                }

                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    _distance = Mathf.Clamp(_distance - Mathf.Sign(wheel) * ZoomStep, _minDistance, _maxDistance);
                }
            }

            Vector3 pivot = _target.position + Vector3.up * _pivotHeight;
            Vector3 desired = pivot + Quaternion.Euler(_pitch, _yaw, 0f) * (Vector3.back * _distance);

            float t = 1f - Mathf.Exp(-_smoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, t);
            transform.rotation = Quaternion.LookRotation(pivot - transform.position);
        }
    }
}
