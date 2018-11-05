using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using PExamples2;
namespace PExample01
{
    /// <summary>
    /// CtrController는 공용이라 이곳에 별도로 추가 조작을 구현했다.
    /// </summary>
    public class CharacterBehaviour : MonoBehaviourPun, IPunObservable
    {


        #region private Variables
        bool Shoot; //정면으로 총알 발사
        bool ChangeShape; //모양 변경
        bool ChangeColor;//생삭 변경
        MeshFilter meshFilter;
        int meshIndex=3; //Meshes의 인덱스
        static PrimitiveType[] Meshes = { PrimitiveType.Sphere, PrimitiveType.Capsule, PrimitiveType.Cylinder, PrimitiveType.Cube, PrimitiveType.Plane, PrimitiveType.Quad };
        static Dictionary<PrimitiveType, Mesh> meshDictionoroy = new Dictionary<PrimitiveType, Mesh>();
        new Renderer renderer;
        static Color[] colors = { Color.red, Color.yellow, Color.black, Color.blue, Color.green };
        int colorindex = 0;
        #endregion

        #region MonoBehavior Callbacks
        // Use this for initialization
        void Start()
        {
            
            meshFilter = GetComponent<MeshFilter>();
            renderer = GetComponent<Renderer>();
            colorindex = Random.Range(0, colors.Length);
            renderer.material.color = colors[colorindex];
        }

        // Update is called once per frame
        void Update()
        {

            //자신의 것만 조작이 가능하다.
            if (photonView.IsMine)
            {
                Shoot = Input.GetKey(KeyCode.Q);
                ChangeShape = Input.GetKeyDown(KeyCode.W);
                ChangeColor = Input.GetKeyDown(KeyCode.E);


                if (Shoot)
                {
                    GameObject bullet = PhotonNetwork.Instantiate("Bullet", transform.position + transform.forward * 2, Quaternion.identity);
                    bullet.transform.LookAt(transform.up);
                    bullet.GetComponent<Rigidbody>().velocity = transform.forward * 50f;
                    bullet.AddComponent<CollsionNDestroy>();
                }
            }


            if (ChangeShape)
            {
                meshIndex++;
                if (meshIndex == 6) meshIndex = 0;
                changeMesh();
            }

            if (ChangeColor)
            {
                colorindex++;
                if (colorindex == colors.Length) colorindex = 0;
                renderer.material.color = colors[colorindex];
            }

        }
        #endregion

        private void changeMesh()
        {

            if (!meshDictionoroy.ContainsKey(Meshes[meshIndex])){
                GameObject g = GameObject.CreatePrimitive(Meshes[meshIndex]);
                meshDictionoroy.Add (Meshes[meshIndex], g.GetComponent<MeshFilter>().sharedMesh);
                Destroy(g);
            }
            Mesh m;
            if (meshDictionoroy.TryGetValue(Meshes[meshIndex], out m)) meshFilter.mesh = m;


        }

        #region Behaviours


        #endregion

        #region Callbakcs
        /// <summary>
        /// 오브젝트에 대한 지연보상과 동기화를 위한 직렬화 스트림
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="info"></param>
        void IPunObservable.OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            //isWriting이면 이 객체의 주인은 자신이며, 스트림에 데이터를 쓰는 입장인 것이다.
            if (stream.IsWriting)
            {
                //두개의 상태 변수를 전송한다.
                stream.SendNext(meshIndex);
                stream.SendNext(colorindex);
            }
            else//아니라면 isReading이며 이 객체의 주신은 타인이다.
            {
                //전송과 똑같은 순서로 받는다.
                meshIndex = (int)stream.ReceiveNext();
                colorindex = (int)stream.ReceiveNext();
                changeMesh();
                renderer.material.color = colors[colorindex];
            }
        }
        #endregion


        #region Rigidbody Callbacks
        int hits=0;
        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.name == "Bullet(Clone)")
            {
                hits++;
                GameManger.instance.Hits.text = "Hits : " + hits;
            }

        }
        #endregion
    }


}
