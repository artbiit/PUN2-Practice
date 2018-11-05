using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
namespace PExamples2
{
    //Photon.PunBehaviour에서 변경되었다.
    public class CtrController : MonoBehaviourPun
    {
        #region Serialize Variables
        [Tooltip("This Object's CharacterController")]
        public CharacterController controller;

        [Tooltip("Jump Power")]
        public float JumpPower=3f;
        #endregion


        #region MonoBehaviour Callbacks

        void Start()
        {
            controller = GetComponent<CharacterController>();
            //자신의 캐릭터라면 카메라가 쫓아올 수 있도록 설정해준다.

            if (photonView.IsMine)
            {
                Debug.Log("읰");
                gameObject.AddComponent<CameraWork>().OnStartFollowing();
            }
            
        }
        private void FixedUpdate()
        {
            if (photonView.IsMine)
            {
                
                //좌우 회전을 넣어준다.
                transform.Rotate(Vector3.up, Input.GetAxis("Horizontal") * Time.deltaTime * 30f);

                //이동할 전후 상하 값을 만든다.
                Vector3 dir = transform.forward * Input.GetAxis("Vertical");
                dir *= 5f;
                if (!controller.isGrounded) dir.y = Physics.gravity.y;
                else if (Input.GetKeyDown(KeyCode.Space)) dir.y += JumpPower;
                dir *= Time.deltaTime;
                //이동
                controller.Move(dir);
                
            }
        }


        #endregion

 
    }
}
