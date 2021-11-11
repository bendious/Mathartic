using System;
using System.Runtime.InteropServices;
using UnityEngine;


public class PostProcessingMod : MonoBehaviour
{
#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
	[DllImport ("__Internal")]
#else
	[DllImport("PostProcessingMod")]
#endif
	static extern IntPtr Execute();

#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
	[DllImport ("__Internal")]
#else
	[DllImport("PostProcessingMod")]
#endif
	static extern void SetTime(float time);

#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
	[DllImport ("__Internal")]
#else
	[DllImport("PostProcessingMod")]
#endif
	static extern bool UpdateGLShader([MarshalAs(UnmanagedType.LPStr)] string pSrcDataVert, [MarshalAs(UnmanagedType.LPStr)] string pSrcDataFrag);


	bool _Success = false;


	public void UpdateShader(string srcDataVert, string srcDataFrag)
	{
		try
		{
			_Success = UpdateGLShader(srcDataVert, srcDataFrag);
		}
		catch (Exception) { _Success = false; }
	}


	void OnRenderImage (RenderTexture source, RenderTexture destination)
	{
		Graphics.Blit(source, destination);
		if (_Success)
		{
			SetTime(Time.time);
			GL.IssuePluginEvent(Execute(), 1);
		}
	}
}
