using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PExamples2
{
    /// <summary>
    /// Local Object에 붙어서 카메라가 해당 오브젝트를 따라다니도록 만든 스크립트
    /// </summary>
    public class CameraWork : MonoBehaviour
    {

        #region Public Properties

        [Tooltip("The distance in the local x-z plane to the target")]
        public float distance = 7.0f;

        [Tooltip("The height we want the camera to be above the target")]
        public float height = 3.0f;

        [Tooltip("The Smooth time lag for the height of the camera.")]
        public float heightSmoothLag = 0.3f;

        [Tooltip("Allow the camera to be offseted vertically from the target, for example giving more view of the sceneray and less ground.")]
        public Vector3 centerOffset = Vector3.zero;

        [Tooltip("Set this as false if a component of a prefab being instanciated by Photon Network, and manually call OnStartFollowing() when and if needed.")]
        public bool followOnStart = false;


        #endregion

        #region private Properties
        //카메라의 트랜스폼을 받아온다.
        [SerializeField]
        Transform cameraTransform;

        //타겟을 잃어 버리거나 카메라가 전환 된 경우 내부적으로 플래그를 유지하여 다시 연결한다.
        bool isFollowing;

        //현재 속도를 나타내며, 이 값이 호출될 때마다 SmoothDamp()에 의해 수정된다.
        private float heightVelocity = 0.0f;

        //SmoothDamp()를 사용하여 도달하려는 위치를 나타낸다.
        private float targetHegiht = 100000.0f;
        #endregion


        #region MonoBehaviour Callbacks

        // Use this for initialization
        void Start()
        {
            if (followOnStart)
            {
                OnStartFollowing();
            }
        }

        // Update is called once per frame
        void LateUpdate()
        {
            //이 오브젝트는 LoadLevel() 에 의해서 파괴되지 않을 수 있다. 따라서 새로운 장면을 로드할 때 메인카메라가 교체되는지 확인해야 한다.
            if (cameraTransform == null && isFollowing) OnStartFollowing();

            if (isFollowing) Apply();
        }


        #endregion

        #region public Methods
        /// <summary>
        /// 시작 이벤트를 발생시킨다. 움직이게 만들 카메라를 입력할 때 사용된다.
        /// </summary>
        public void OnStartFollowing()
        {
            Debug.Log("시작");
            cameraTransform = Camera.main.transform;
            isFollowing = true;
            Cut();
        }
        #endregion

        #region private Methods

        /// <summary>
        /// 카메라가 이 오브젝트를 천천히 쫓아오도록 한다.
        /// </summary>
        private void Apply()
        {

            Vector3 targetCenter = transform.position + centerOffset;

            //지금 각도와 타겟의 각도를 계산한다.
            float originalTargetAngle = transform.eulerAngles.y;
            float currentAngle = cameraTransform.eulerAngles.y;

            //카메라가 잠겨있을 때 실제 목표 각도를 조정한다.
            float targetAngle = originalTargetAngle;

            currentAngle = targetAngle;

            targetHegiht = targetCenter.y + height;

            //높이 Damp
            float currentHeight = cameraTransform.position.y;
            currentHeight = Mathf.SmoothDamp(currentHeight, targetHegiht, ref heightVelocity, heightSmoothLag);

            //각도를 사원수로 변환한다.
            Quaternion currentRotation = Quaternion.Euler(0, currentAngle, 0);

            //x-z 평면에서 카메라의 위치를 이 오브젝트 뒤의 거리로 설정한다.
            cameraTransform.position = targetCenter;
            cameraTransform.position += currentRotation * Vector3.back * distance;

            //카메라의 높이를 재정의 한다.
            cameraTransform.position = new Vector3(cameraTransform.position.x, currentHeight, cameraTransform.position.z);

            //항상 이 오브젝트를 쳐다보도록 만든다.
            SetUpRotation(targetCenter);
        }
        /// <summary>
        /// 카메라를 이 오브젝트 혹은 중앙에 직접 배치한다.
        /// </summary>
        private void Cut()
        {
            float oldHeightSmooth = heightSmoothLag;
            heightSmoothLag = 0.001f;
            Apply();
            heightSmoothLag = oldHeightSmooth;
        }

        /// <summary>
        /// 카메라는 항상 이 오브젝트를 바라봐야 한다.
        /// </summary>
        /// <param name="centerPos"></param>
        private void SetUpRotation(Vector3 centerPos)
        {
            //현재 카메라의 위치
            Vector3 cameraPos = cameraTransform.position;
            //중앙 -> 카메라 위치로의 오프셋을 생성
            Vector3 offsetToCenter = centerPos - cameraPos;

            //Y축 주위로만 기본 회전을 생성한다.
            Quaternion yRotation = Quaternion.LookRotation(new Vector3(offsetToCenter.x, 0, offsetToCenter.z));

            //카메라와의 거리만큼의 정면과 카메라와의 높이만큼의 바닥측을 가르키는 좌표를 만든다.
            Vector3 relativeOffset = Vector3.forward * distance + Vector3.down * height;
            //이 오브젝트를 y축으로 바라보는 사원수와 정확히 바라볼 y축의 정보를 곱하여 쳐다보도록 만든다.
            //따라서 이 오브젝트 측을 바라본다.
            cameraTransform.rotation = yRotation * Quaternion.LookRotation(relativeOffset);
        }
        #endregion
    }

}
