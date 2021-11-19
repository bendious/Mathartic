using System;
using System.Linq;
using UnityEngine.Assertions;


public static class Utility
{
	public static int Modulo(int x, int m)
	{
		int r = x % m;
		return (r < 0) ? r + m : r;
	}

	public static float Modulo(float x, float m)
	{
		float r = x % m;
		return (r < 0) ? r + m : r;
	}

	public static float Fract(float x) => x - (float)Math.Truncate(x);

	public static int EnumNumTypes<T>()
	{
		return Enum.GetValues(typeof(T)).Length;
	}

	public static T RandomWeighted<T>(T[] values, float[] weights)
	{
		Assert.IsFalse(weights.Any(f => f < 0.0f));

		// NOTE the array slice to handle values[] w/ shorter length than weights[] by ignoring the excess weights; the opposite situation works out equivalently w/o explicit handling since weightRandom will never result in looping beyond the number of weights given
		float weightSum = new ArraySegment<float>(weights, 0, Math.Min(values.Length, weights.Length)).Sum();
		float weightRandom = UnityEngine.Random.Range(0.0f, weightSum);

		int idxItr = 0;
		while (weightRandom >= weights[idxItr])
		{
			weightRandom -= weights[idxItr];
			++idxItr;
		}

		Assert.IsTrue(weightRandom >= 0.0f && idxItr < values.Length);
		return values[idxItr];
	}

	public static T RandomWeightedEnum<T>(float[] weights) where T : System.Enum
	{
		/*const*/ int typeCount = EnumNumTypes<T>();
		Assert.IsTrue(weights.Length <= typeCount);
		return RandomWeighted(Enumerable.Range(0, typeCount).Select(i => {
			Assert.IsTrue(Enum.IsDefined(typeof(T), i));
			return (T)Enum.ToObject(typeof(T), i);
		}).ToArray(), weights);
	}

	public static string FormatFloatGLSL(float f)
	{
		return "float(" + f + ")"; // this prevents GLSL parsing issues from floats w/o decimals being interpreted as ints, w/o truncating to a fixed number of decimals
	}
}
