using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;
namespace PExample01
{
    public class Launcher : MonoBehaviourPunCallbacks //Photon.PUNBehaviour에서 변경되었다.
    {
        #region Serialize variable
        /// <summary>
        /// 유저 이름 입력 변수
        /// </summary>
        public InputField playerName;
        /// <summary>
        /// 안내 문구 변수
        /// </summary>
        public Text Notice;
        //settings에서 직접 해도 되지만 변수로 설정할 수 있다.
        public string gameVersion = "1.0";
        /// <summary>
        /// 방의 최대 인원을 정하는 변수 Inspector창에서 설정한다.
        /// </summary>
        [Tooltip("The maximum number of players per room. When a room is full, it can't be joined by new players, and so new room will be created")]
        [SerializeField]
        private byte maxPlayersPerRoom = 5;
        #endregion
        #region MonoBehaviour Callbacks
        private void Awake()
        {
            //MasterClient에 의한 scene 동기화 옵션. MasterClient에서 Room에 속한 유저들의 Scene을 동기화 한다.
            PhotonNetwork.AutomaticallySyncScene = true;
        }
        #endregion
        #region publicMethod
        /// <summary>
        /// 연결 여부에 따라 포톤에 연결을 시도하거나 방에 입장을 시도한다.
        /// </summary>
        public void Connect()
        {
            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.JoinRandomRoom();
            }
            else
            {
                //닉네임을 지정해준다.
                PhotonNetwork.NickName = playerName.text;
                //2로 넘어오면서 버전을 별도로 설정하도록 변경되었다.
                PhotonNetwork.GameVersion = gameVersion;
                PhotonNetwork.ConnectUsingSettings();
                Notice.text = "연결 시도 중 입니다.";
            }
        }

        /// <summary>
        /// PlayerName InputField에서 엔터를 쳤으면 해당 유저 이름으로 연결을 시도 한다.
        /// </summary>
        public void EnterOnConnect()
        {
            if(Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter))
            {
                isEmptyPlayerName();
            }
        }
        /// <summary>
        /// PlayButton으로 클릭을 통해 연결을 시도 한다.
        /// </summary>
        public void ClickOnConnect()
        {
            isEmptyPlayerName();
        }

        /// <summary>
        /// 플레이어 이름이 비어있는지 확인후 연결을 시도 한다.
        /// </summary>
        private void isEmptyPlayerName()
        {
            if (string.IsNullOrEmpty(playerName.text))
            {
                Notice.text = "이름을 입력해주세요.";
                return;
            }
            Connect();
        }
        #endregion

        #region MonoBehaviourPunCallbacks Callbacks
        //Master 서버 (PUN 서버 중 가장 클라이언트에 가깝게 위치한 서버)에 연결되었을 때 호출되는 콜백
        public override void OnConnectedToMaster()
        {
            Notice.text = "연결에 성공하였습니다.";
            Debug.Log("Master 서버에 연결됨");
            PhotonNetwork.JoinRandomRoom();
        }


        //어떠한 이유로든 연결이 해제되었을 때 콜백
        public override void OnDisconnected(DisconnectCause cause)
        { 
            Notice.text = "연결이 해제 되었습니다.";
            Debug.Log("Disconnect : " + cause);
        }

        //방에 대한 랜덤입장이 실패할경우 호출되는 콜백
        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            Notice.text = "방에 입장하지 못했습니다. \n새 방을 만들어볼게요.";
            //임의의 방에 입장 못했을 시 방이 없는 것이므로 새로 하나 생성한다.
            PhotonNetwork.CreateRoom("Test", new RoomOptions { MaxPlayers = maxPlayersPerRoom });
        }

        //방 입장에 성공하였을때 호출되는 콜백
        public override void OnJoinedRoom()
        {
            Notice.text = "방에 입장하였습니다.";
            //주의할점은 방을 생성한 클라이언트는 OnCreateRoom() 를 먼저 호출 후 이 콜백에 들어온다.
            Debug.Log("방 생성 : " + PhotonNetwork.CurrentRoom.Name);
           

        }

        //방을 생성하였을 때 호출되는 콜백
        public override void OnCreatedRoom()
        {
            //방을 생성한 클라이언트 이므로 마스터 클라이언트다. 따라서 scene을 호출한다.
            PhotonNetwork.LoadLevel("RoomFor1");
        }


        #endregion
    }

}
