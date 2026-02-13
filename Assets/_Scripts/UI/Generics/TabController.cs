using TMPro;
using UnityEngine;

namespace SyncedRush.UI
{
	public class TabController : MonoBehaviour
	{
		[SerializeField] TextMeshProUGUI tabText;
		public void SetTextDark(bool dark)
		{
			tabText.color = dark ? Color.black : Color.white;
		}
	}
}