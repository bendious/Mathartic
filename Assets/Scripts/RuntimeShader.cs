using System;
using System.Runtime.InteropServices;
using UnityEngine;


public class RuntimeShader : MonoBehaviour
{
#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
	[DllImport ("__Internal")]
#else
	[DllImport("RuntimeShader")]
#endif
	private static extern void RegisterPlugin();

#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
	[DllImport ("__Internal")]
#else
	[DllImport("RuntimeShader")]
#endif
	static extern IntPtr Execute();

#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
	[DllImport ("__Internal")]
#else
	[DllImport("RuntimeShader")]
#endif
	static extern void SetTime(float time);

#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
	[DllImport ("__Internal")]
#else
	[DllImport("RuntimeShader")]
#endif
	static extern bool UpdateGLShader([MarshalAs(UnmanagedType.LPStr)] string pSrcDataVert, [MarshalAs(UnmanagedType.LPStr)] string pSrcDataFrag);


	private bool m_shaderReady = false;

	private float m_startTime = 0.0f;


	public void UpdateShader(string srcDataVert, string srcDataFrag)
	{
		try
		{
			m_shaderReady = UpdateGLShader(srcDataVert, srcDataFrag);
		}
		catch (Exception) { m_shaderReady = false; }
	}

	public void ResetTime()
	{
		m_startTime = Time.time;
	}


	void Start()
	{
		RegisterPlugin();
	}

	void OnRenderImage (RenderTexture source, RenderTexture destination)
	{
		Graphics.Blit(source, destination);
		if (m_shaderReady)
		{
			SetTime(Time.time - m_startTime);
			GL.IssuePluginEvent(Execute(), 1);
		}
	}
}
