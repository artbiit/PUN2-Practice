using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;
using PExamples2;
using UnityEngine.UI;
namespace PExample01
{
    public class GameManger : MonoBehaviourPunCallbacks
    {

        GameObject Character;

        public Text Hits;

        //싱글턴
        public static GameManger instance;
        #region MonoBehaviour Callbacks
        void Start()
        {
            if (instance != null) Destroy(this);
            instance = this;
           this.Character =  PhotonNetwork.Instantiate("PEx01Player", Vector3.zero, Quaternion.identity);
            Debug.Log("캐릭터 생성");
            
        }

        private void OnDestroy()
        {
            PhotonNetwork.Destroy(Character);
        }
        #endregion
        #region publicMethods
        //방에서 나가기 버튼을 눌렀을 때 호출할 메소드
        public void LeaveRoom()
        {
            PhotonNetwork.LeaveRoom();
        }
        #endregion

        #region MonoBehaviourPun Callbacks
        /// <summary>
        /// 유저가 들어왔을 때 호출
        /// </summary>
        /// <param name="newPlayer"></param>
        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            Debug.Log("유저 추가 : "+newPlayer.NickName);
            if (PhotonNetwork.IsMasterClient)
                PhotonNetwork.LoadLevel("RoomFor" + PhotonNetwork.CurrentRoom.PlayerCount);
        }

        /// <summary>
        /// 유저가 나갔을 때 호출
        /// </summary>
        /// <param name="otherPlayer"></param>
        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            Debug.Log("유저 감소 : " + otherPlayer.NickName);
            //방장만이 호출한다.
            if(PhotonNetwork.IsMasterClient)
            PhotonNetwork.LoadLevel("RoomFor" + PhotonNetwork.CurrentRoom.PlayerCount);
        }

        /// <summary>
        /// 방에서 나가고서 호출
        /// </summary>
        public override void OnLeftRoom()
        {
            SceneManager.LoadScene(0);
            //방에서 나가고서 초기 화면으로 돌아간다.
        }
        #endregion
    }
}
