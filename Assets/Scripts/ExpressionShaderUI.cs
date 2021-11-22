using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;


public class ExpressionShaderUI : MonoBehaviour
{
	public InputField m_xMinField;
	public InputField m_xMaxField;
	public InputField m_yMinField;
	public InputField m_yMaxField;
	public InputField m_outMinField;
	public InputField m_outMaxField;
	public InputField m_equalityEpsilonField;
	public InputField m_rField;
	public InputField m_gField;
	public InputField m_bField;
	public Text m_rErrorText;
	public Text m_gErrorText;
	public Text m_bErrorText;
	public GameObject m_paramListParent;
	public Toggle m_randomizeDiscontinuousToggle;
	public InputField m_randomizeDepthMaxField;


	private ValueTuple<InputField, InputField, Text>[] m_paramFields = { };

	private readonly ExpressionShader m_internals = new ExpressionShader();


	private void Start()
	{
		UpdateLimits();
		Randomize();
	}


	public void UpdateShader(bool noTimeReset)
	{
		// get expression strings
		ValueTuple<InputField, Text>[] fieldPairs = { (m_rField, m_rErrorText), (m_gField, m_gErrorText), (m_bField, m_bErrorText) };
		string[] expressionsRaw = fieldPairs.Select(pair => pair.Item1.text).ToArray();
		ValueTuple<string, string>[] paramsRaw = m_paramFields.Select(fields => (fields.Item1.text, fields.Item2.text)).ToArray();

		// pass in for evaluation
		string[] errors = m_internals.UpdateShader(expressionsRaw, paramsRaw, noTimeReset);
		Assert.AreEqual(errors.Length, expressionsRaw.Length + paramsRaw.Length);

		// update error messages
		foreach (ValueTuple<ValueTuple<InputField, Text>, string> tuple in fieldPairs.Concat(m_paramFields.Select(fields => (fields.Item2, fields.Item3))).Zip(errors, (pair, err) => (pair, err)))
		{
			UpdateErrorText(tuple.Item1.Item2, tuple.Item2, tuple.Item1.Item1);
		}
	}

	public void UpdateLimits()
	{
		float.TryParse(m_xMinField.text, out m_internals.m_xMin);
		float.TryParse(m_xMaxField.text, out m_internals.m_xMax);
		float.TryParse(m_yMinField.text, out m_internals.m_yMin);
		float.TryParse(m_yMaxField.text, out m_internals.m_yMax);
		float.TryParse(m_outMinField.text, out m_internals.m_outMin);
		float.TryParse(m_outMaxField.text, out m_internals.m_outMax);

		float.TryParse(m_equalityEpsilonField.text, out m_internals.m_epsilon);

		UpdateShader(true); // this will early-out if the shader is unchanged
	}

	public void Zoom(bool inward)
	{
		float scalar = inward ? 0.5f : 2.0f;
		m_xMinField.text = ScaleLimitAndStringify(m_internals.m_xMin, scalar);
		m_xMaxField.text = ScaleLimitAndStringify(m_internals.m_xMax, scalar);
		m_yMinField.text = ScaleLimitAndStringify(m_internals.m_yMin, scalar);
		m_yMaxField.text = ScaleLimitAndStringify(m_internals.m_yMax, scalar);

		UpdateLimits();
	}

	public void UpdateParamFields()
	{
		List<ValueTuple<InputField, InputField, Text>> paramFieldsList = new List<ValueTuple<InputField, InputField, Text>>();
		for (int i = 0, n = m_paramListParent.transform.childCount; i < n; ++i)
		{
			GameObject child = m_paramListParent.transform.GetChild(i).gameObject;
			if (!child.activeSelf)
			{
				continue; // ignore placeholder object
			}

			// TODO: don't assume objects will always be set up w/ children ordered as ({RemoveButton}, {NameField}, {StaticText}, {ExpressionField}, {ErrorText})?
			InputField[] inputFields = child.GetComponentsInChildren<InputField>();
			Assert.IsTrue(inputFields.Length == 2);
			paramFieldsList.Add((inputFields.First(), inputFields.Last(), child.GetComponentsInChildren<Text>().Last()));
		}
		m_paramFields = paramFieldsList.ToArray();

		UpdateShader(false); // this will early-out if the shader is unchanged
	}

	public void Randomize()
	{
		// get max depth
		uint recursionMax = 0;
		try
		{
			recursionMax = uint.Parse(m_randomizeDepthMaxField.text);
		}
		catch (Exception) { }

		// randomize & update shader
		ValueTuple<string, string>[] paramsRaw = m_paramFields.Select(fields => (fields.Item1.text, fields.Item2.text)).ToArray();
		string[] expStrings = m_internals.Randomize(recursionMax, m_randomizeDiscontinuousToggle.isOn, paramsRaw);

		// fill in text fields
		// TODO: detect & eliminate redundant parentheses?
		Assert.AreEqual(expStrings.Length, 3);
		m_rField.text = expStrings[0];
		UpdateErrorText(m_rErrorText, "", m_rField); // TODO: don't assume random expressions will always be valid?
		m_gField.text = expStrings[1];
		UpdateErrorText(m_gErrorText, "", m_gField);
		m_bField.text = expStrings[2];
		UpdateErrorText(m_bErrorText, "", m_bField);
	}


	private void UpdateErrorText(Text errorText, string str, InputField field)
	{
		// update text
		Assert.IsNotNull(errorText);
		errorText.text = str ?? "";

		// resize field to meet errorText w/o overlap
		// TODO: don't assume field is left-aligned and errorText is right-aligned and content-sized?
		Assert.IsNotNull(field);
		RectTransform tf = field.GetComponent<RectTransform>();
		tf.sizeDelta = new Vector2(-(errorText.preferredWidth - errorText.GetComponent<RectTransform>().anchoredPosition.x + tf.anchoredPosition.x), tf.sizeDelta.y);
	}

	private string ScaleLimitAndStringify(float x, float scalar)
	{
		const float zoomMin = 0.01f; // due to input fields not understanding scientific notation, so small enough numbers would cause a jump back up
		float xScaled = x * scalar;
		return (xScaled < zoomMin && xScaled > -zoomMin ? zoomMin * (x < 0 ? -1 : 1) : xScaled).ToString();
	}
}
