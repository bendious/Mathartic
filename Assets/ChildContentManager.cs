using UnityEngine;
using UnityEngine.Assertions;


[RequireComponent(typeof(RectTransform))]
public class ChildContentManager : MonoBehaviour
{
	public void Add()
	{
		GameObject placeholderChild = transform.GetChild(0).gameObject;
		Assert.IsTrue(!placeholderChild.activeSelf);
		int visibleChildCount = transform.childCount - 1;
		float childHeight = placeholderChild.GetComponent<RectTransform>().rect.height;
		Vector3 childPosLocal = Vector3.down * visibleChildCount * childHeight;
		Vector3 childPosGlobal = transform.position + childPosLocal;
		GameObject newChild = Instantiate(placeholderChild, childPosGlobal, Quaternion.identity, transform);
		newChild.name = "Param" + visibleChildCount;
		newChild.SetActive(true);

		RefitHeight(Mathf.Abs(childPosLocal.y) + childHeight);
	}

	public void Remove()
	{
		int visibleChildCount = transform.childCount - 1;
		if (visibleChildCount < 1) // don't destroy placeholder
		{
			return;
		}
		GameObject lastChild = transform.GetChild(visibleChildCount).gameObject;
		Assert.IsTrue(lastChild.activeSelf);
		lastChild.transform.SetParent(null); // since Destroy() doesn't take effect immediately, yet we want to re-list the scroll view's content immediately
		Destroy(lastChild);

		RefitHeight((visibleChildCount - 1) * transform.GetChild(0).gameObject.GetComponent<RectTransform>().rect.height);
	}


	private void RefitHeight(float height)
	{
		RectTransform contentRect = GetComponent<RectTransform>();
		contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, height);
	}
}
