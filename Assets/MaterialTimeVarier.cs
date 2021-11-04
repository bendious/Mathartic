using NCalc;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;
using UnityEngine.UI;


[RequireComponent(typeof(Image))]
public class MaterialTimeVarier : MonoBehaviour
{
	public float m_speedScalar = 1.0f;
	public InputField m_rField;
	public InputField m_gField;
	public InputField m_bField;
	public Text m_rErrorText;
	public Text m_gErrorText;
	public Text m_bErrorText;


	void Update()
	{
		// evaluate given expression strings
		// TODO: move out of per-frame logic?
		InputField[] fields = { m_rField, m_gField, m_bField };
		Expression[] expressions = fields.Select(field => ExpressionFromField(field)).ToArray();
		Text[] errorTexts = { m_rErrorText, m_gErrorText, m_bErrorText };

		for (int i = 0, n = expressions.Length; i < n; ++i)
		{
			// skip evaluating while empty or currently being edited
			Expression exp = expressions[i];
			if (exp == null || EventSystem.current.currentSelectedGameObject == fields[i].gameObject)
			{
				continue;
			}

			// attempt to evaluate
			Text errorText = errorTexts[i];
			try
			{
				errorText.text = exp.Evaluate().ToString();
			}
			catch (System.Exception e)
			{
				errorText.text = e.Message;
			}
		}

		// update material properties
		Material material = GetComponent<Image>().material;
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

	void OnDestroy()
	{
		// reset material since changes to UI Image materials during play persist afterward...
		Material material = GetComponent<Image>().material;
		Assert.IsNotNull(material);
		Shader shader = material.shader;
		Assert.IsNotNull(shader);
		for (int i = 0, n = shader.GetPropertyCount(); i < n; ++i)
		{
			Assert.IsTrue(shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Vector);
			material.SetVector(shader.GetPropertyNameId(i), shader.GetPropertyDefaultVectorValue(i));
		}
	}


	private Expression ExpressionFromField(InputField field)
	{
		Assert.IsNotNull(field);
		string text = field.text;
		return string.IsNullOrEmpty(text) ? null : new Expression(text);
	}
}
