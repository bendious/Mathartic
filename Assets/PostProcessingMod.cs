using System;
using System.Runtime.InteropServices;
using UnityEngine;


public class PostProcessingMod : MonoBehaviour
{
	[DllImport("PostProcessingMod")]
	static extern IntPtr Execute();

	[DllImport("PostProcessingMod")]
	static extern void SetTime(float time);

	[DllImport("PostProcessingMod")]
	static extern bool Init([MarshalAs(UnmanagedType.LPStr)] string pSrcData, [MarshalAs(UnmanagedType.U8)] int SrcDataSize);

	bool _Success = false;


	public void UpdateShader(string srcData)
	{
		try
		{
			_Success = Init(srcData, srcData.Length);
		}
		catch (Exception) { _Success = false; }
	}


	void OnRenderImage (RenderTexture source, RenderTexture destination)
	{
		if (_Success)
		{
			SetTime(Time.time);
		}
		Graphics.Blit(source, destination);
		if (_Success) GL.IssuePluginEvent(Execute(), 1);
	}
}
