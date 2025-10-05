using UnityEngine;

namespace KinectEx.Sample
{
    public class EffectBehaviour : MonoBehaviour
    {
        [SerializeField] private AudioClip destroySound;    // エフェクト消滅音
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            // 手のオブジェクトと衝突したらエフェクトを削除
            if (other.gameObject.name == "HandRight" || other.gameObject.name == "HandLeft")
            {
                // 消滅音を再生
                AudioSource.PlayClipAtPoint(destroySound, Camera.main.transform.position);
                
                // エフェクトを削除
                Destroy(this.gameObject);
            }
        }
    }
}