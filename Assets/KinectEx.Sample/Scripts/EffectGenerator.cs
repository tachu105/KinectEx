using System.Collections;
using UnityEngine;

namespace KinectEx.Sample
{
    public class EffectGenerator : MonoBehaviour
    {
        [SerializeField] private GameObject effectPrefab;   // 生成するエフェクトのプレハブ
    
        [SerializeField] private float xRangeMin = 5f;      // エフェクト生成範囲のX座標最小値
        [SerializeField] private float xRangeMax = 5f;      // エフェクト生成範囲のX座標最大値
        [SerializeField] private float yRangeMin = 5f;      // エフェクト生成範囲のY座標最小値
        [SerializeField] private float yRangeMax = 5f;      // エフェクト生成範囲のY座標最大値
    
        [SerializeField] private float generateIntervalSec = 1f;    // エフェクト生成間隔（秒）
    
        void Start()
        {
            StartCoroutine(GenerateEffect());   // エフェクト生成コルーチン開始
        }
    
        /// <summary>
        /// エフェクトを指定時間間隔に生成するコルーチン
        /// </summary>
        private IEnumerator GenerateEffect()
        {
            while (true)
            {
                // 生成場所をランダムに決定
                Vector3 generatePosition = new Vector3(Random.Range(xRangeMin, xRangeMax), Random.Range(yRangeMin, yRangeMax), 0);
            
                // エフェクトを生成
                Instantiate(effectPrefab, generatePosition, Quaternion.identity);
            
                // 次の生成まで指定時間待機
                yield return new WaitForSeconds(generateIntervalSec);
            }
        }
    }
}