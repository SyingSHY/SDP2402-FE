using TMPro;
using UnityEngine;

public class DynamicChatBox : MonoBehaviour
{
    public TextMeshProUGUI messageText;
    public RectTransform chatBoxRect;
    
    public void SetMessage(string message)
    {
        messageText.text = message;

        var newHeight = 33 + messageText.preferredHeight;
        
        Canvas.ForceUpdateCanvases();
        chatBoxRect.sizeDelta = new Vector2(chatBoxRect.sizeDelta.x, 33 > newHeight ? 33 : newHeight);
    }
}
