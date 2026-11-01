using UnityEngine;
using UnityEngine.EventSystems;

namespace Inspection
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        public Camera viewCamera;
        public InspectionController inspection;
        public float speed = 3.5f;
        public float lookSensitivity = 2f;
        float pitch;
        float verticalVelocity;
        CharacterController character;

        void Awake() { character = GetComponent<CharacterController>(); }
        void Update()
        {
            if (inspection != null && inspection.IsEditing) return;
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (Input.GetMouseButton(1) && !overUI)
            {
                transform.Rotate(Vector3.up * Input.GetAxis("Mouse X") * lookSensitivity);
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * lookSensitivity, -75f, 75f);
                viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            float x = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            float z = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            Vector3 direction = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1f);
            verticalVelocity = character.isGrounded ? -2f : verticalVelocity + Physics.gravity.y * Time.deltaTime;
            character.Move((direction * speed + Vector3.up * verticalVelocity) * Time.deltaTime);
        }
    }
}
