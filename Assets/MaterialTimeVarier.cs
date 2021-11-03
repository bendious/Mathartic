using UnityEngine;
using UnityEngine.Assertions;


[RequireComponent(typeof(Renderer))]
public class MaterialTimeVarier : MonoBehaviour
{
	public float m_speedScalar = 1.0f;


	void Update()
	{
		Material material = GetComponent<Renderer>().material;
		Assert.IsNotNull(material);
		Shader shader = material.shader;
		Assert.IsNotNull(shader);
		for (int i = 0, n = shader.GetPropertyCount(); i < n; ++i)
		{
			Assert.IsTrue(shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Vector);
			int nameId = shader.GetPropertyNameId(i);
			Vector4 vecOld = material.GetVector(nameId);
			Vector4 offsetPcts = (new Vector4(Random.value, Random.value, Random.value, Random.value) * 2.0f - Vector4.one) * m_speedScalar * Time.deltaTime;
			material.SetVector(nameId, vecOld + offsetPcts);
		}
	}
}
