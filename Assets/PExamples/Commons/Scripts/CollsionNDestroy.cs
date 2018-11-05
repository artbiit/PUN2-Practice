using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PExamples2 { 
public class CollsionNDestroy : MonoBehaviour {



        IEnumerator DestroySec()
        {
            yield return new WaitForSecondsRealtime(5f);
            Photon.Pun.PhotonNetwork.Destroy(gameObject);
        }
        private void OnCollisionEnter(Collision collision)
        {

            StartCoroutine(DestroySec());
        }
    }

}