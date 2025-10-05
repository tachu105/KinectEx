//======================================
/*
@autor ktk.kumamoto
@date 2015.3.6 create
@note UvScroll
*/
//======================================

using UnityEngine;
using System.Collections;

public class UvScroll : MonoBehaviour {
	
	public float scrollSpeed_u = 0.5F;
	public float scrollSpeed_v = 0.5F;
	private Renderer rend;
	void Start() {
		rend = GetComponent<Renderer>();
	}
	void Update() {
		float offset_u = Time.time * - scrollSpeed_u;
		float offset_v = Time.time * - scrollSpeed_v; 
		rend.material.SetTextureOffset("_MainTex", new Vector2(offset_u, offset_v));
	}
}