using System;
using UnityEngine;
using UnityEngine.UI;
public class PokerAlert: MonoBehaviour {
    private GameObject alertPanel;

    private Action PrimaryAction;
    private Action SecondaryAction;
    private String Title;
    private String Message;
    private String PrimaryButtonTitle;
    private String SecondaryButtonTitle;

    private float Height = 300;
    private float Width = 200;

    public void Initialize(
        Action primaryAction = null,
        Action secondaryAction = null,
        String title = "Title",
        String message = "Message",
        String primaryButtonTitle = "OK",
        String secondaryButtonTitle = "Cancel"
    ) {
        PrimaryAction = primaryAction;
        SecondaryAction = secondaryAction;
        Title = title;
        Message = message;
        PrimaryButtonTitle = primaryButtonTitle;
        SecondaryButtonTitle = secondaryButtonTitle;
    }

    public void SetHeight(float height) {
        Height = height;
    }

    public void SetWidth(float width) {
        Width = width;
    }

    public void BuildAlert(Canvas canvas) {
        alertPanel = new GameObject("AlertPanel");
        alertPanel.transform.SetParent(canvas.transform, false);

        RectTransform panelTransform = alertPanel.AddComponent<RectTransform>();
        panelTransform.sizeDelta = new Vector2(Height, Width);
        panelTransform.anchorMin = new Vector2(0.5f, 0.5f);
        panelTransform.anchorMax = new Vector2(0.5f, 0.5f);
        panelTransform.pivot = new Vector2(0.5f, 0.5f);
        panelTransform.anchoredPosition = Vector2.zero;

        Image panelImage = alertPanel.AddComponent<Image>();
        panelImage.color = Color.black;

        GameObject titleObject = new GameObject("TitleText");
        titleObject.transform.SetParent(alertPanel.transform, false);

        RectTransform titleTransform = titleObject.AddComponent<RectTransform>();
        titleTransform.sizeDelta = new Vector2(280, 40);
        titleTransform.anchorMin = new Vector2(0.5f, 1);
        titleTransform.anchorMax = new Vector2(0.5f, 1);
        titleTransform.pivot = new Vector2(0.5f, 1);
        titleTransform.anchoredPosition = new Vector2(0, -20);

        Text titleText = titleObject.AddComponent<Text>();
        titleText.text = Title;
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.color = Color.white;
        titleText.fontSize = 24;
        titleText.alignment = TextAnchor.MiddleCenter;

        GameObject messageObject = new GameObject("MessageText");
        messageObject.transform.SetParent(alertPanel.transform, false);

        RectTransform messageTransform = messageObject.AddComponent<RectTransform>();
        messageTransform.sizeDelta = new Vector2(280, 100);
        messageTransform.anchorMin = new Vector2(0.5f, 0.5f);
        messageTransform.anchorMax = new Vector2(0.5f, 0.5f);
        messageTransform.pivot = new Vector2(0.5f, 0.5f);
        messageTransform.anchoredPosition = Vector2.zero;

        Text messageText = messageObject.AddComponent<Text>();
        messageText.text = Message;
        messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        messageText.color = Color.white;
        messageText.fontSize = 16;
        messageText.alignment = TextAnchor.MiddleCenter;

        GameObject primaryButtonObject = new GameObject("PrimaryButton");
        primaryButtonObject.transform.SetParent(alertPanel.transform, false);

        RectTransform primaryButtonTransform = primaryButtonObject.AddComponent<RectTransform>();
        primaryButtonTransform.sizeDelta = new Vector2(100, 40);
        primaryButtonTransform.anchorMin = new Vector2(0.5f, 0);
        primaryButtonTransform.anchorMax = new Vector2(0.5f, 0);
        primaryButtonTransform.pivot = new Vector2(0.5f, 0);
        primaryButtonTransform.anchoredPosition = new Vector2(-60, 20);

        Button primaryButton = primaryButtonObject.AddComponent<Button>();
        primaryButton.onClick.AddListener(() => PrimaryAction?.Invoke());
        Image primaryButtonImage = primaryButtonObject.AddComponent<Image>();
        primaryButtonImage.color = Color.gray;

        Text primaryButtonText = new GameObject("PrimaryButtonText").AddComponent<Text>();
        primaryButtonText.transform.SetParent(primaryButtonObject.transform, false);
        primaryButtonText.text = PrimaryButtonTitle;
        primaryButtonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        primaryButtonText.color = Color.white;
        primaryButtonText.fontSize = 16;
        primaryButtonText.alignment = TextAnchor.MiddleCenter;

        GameObject secondaryButtonObject = new GameObject("SecondaryButton");
        secondaryButtonObject.transform.SetParent(alertPanel.transform, false);

        RectTransform secondaryButtonTransform = secondaryButtonObject.AddComponent<RectTransform>();
        secondaryButtonTransform.sizeDelta = new Vector2(100, 40);
        secondaryButtonTransform.anchorMin = new Vector2(0.5f, 0);
        secondaryButtonTransform.anchorMax = new Vector2(0.5f, 0);
        secondaryButtonTransform.pivot = new Vector2(0.5f, 0);
        secondaryButtonTransform.anchoredPosition = new Vector2(60, 20);

        Button secondaryButton = secondaryButtonObject.AddComponent<Button>();
        secondaryButton.onClick.AddListener(() => SecondaryAction?.Invoke());
        Image secondaryButtonImage = secondaryButtonObject.AddComponent<Image>();
        secondaryButtonImage.color = Color.gray;

        Text secondaryButtonText = new GameObject("SecondaryButtonText").AddComponent<Text>();
        secondaryButtonText.transform.SetParent(secondaryButtonObject.transform, false);
        secondaryButtonText.text = SecondaryButtonTitle;
        secondaryButtonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        secondaryButtonText.color = Color.white;
        secondaryButtonText.fontSize = 16;
        secondaryButtonText.alignment = TextAnchor.MiddleCenter;
    }

    public void RemoveAlert()
    {
        if (alertPanel != null)
        {
            Destroy(alertPanel);
            alertPanel = null;
        }
    }
}