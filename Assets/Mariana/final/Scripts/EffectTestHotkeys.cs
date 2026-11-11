using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// UI simple para seleccionar y controlar los efectos de la escena final.
public class EffectTestHotkeys : MonoBehaviour
{
    public VfxEffectBase laser;
    public VfxEffectBase orb;
    public VfxEffectBase trail;

    readonly string[] effectNames = { "Laser", "Orb", "Trail" };
    VfxEffectBase[] effects;
    Button[] selectionButtons;
    Text statusText;
    Button playButton;
    int selectedIndex;

    void Awake()
    {
        effects = new[] { laser, orb, trail };
        BuildUI();
        RefreshUI();
    }

    void Update()
    {
        RefreshUI();
    }

    void SelectEffect(int index)
    {
        for (int i = 0; i < effects.Length; i++)
        {
            if (i != index && effects[i] != null && effects[i].IsPlaying)
                effects[i].Pause();
        }

        selectedIndex = index;
        RefreshUI();
    }

    void PlaySelected()
    {
        VfxEffectBase effect = SelectedEffect;
        if (effect == null) return;

        if (effect.IsPaused) effect.Resume();
        else effect.Play();
        RefreshUI();
    }

    void PauseSelected()
    {
        if (SelectedEffect != null) SelectedEffect.Pause();
        RefreshUI();
    }

    VfxEffectBase SelectedEffect => effects != null && selectedIndex < effects.Length
        ? effects[selectedIndex]
        : null;

    void BuildUI()
    {
        if (EventSystem.current == null)
        {
            GameObject eventSystemObject = new GameObject("VFX UI EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }

        GameObject canvasObject = new GameObject("VFX Control UI");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvas.pixelPerfect = true;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(24f, -24f);
        panelRect.sizeDelta = new Vector2(280f, 238f);
        panel.GetComponent<Image>().color = new Color(0.035f, 0.055f, 0.09f, 0.9f);

        CreateLabel(panel.transform, "EFECTOS VFX", new Vector2(16f, -14f), new Vector2(248f, 30f), 22, TextAnchor.MiddleLeft);
        selectionButtons = new Button[effects.Length];
        for (int i = 0; i < effects.Length; i++)
        {
            int effectIndex = i;
            selectionButtons[i] = CreateButton(panel.transform, effectNames[i], new Vector2(16f, -54f - i * 42f), new Vector2(248f, 34f), () => SelectEffect(effectIndex));
        }

        playButton = CreateButton(panel.transform, "Play", new Vector2(16f, -184f), new Vector2(120f, 38f), PlaySelected);
        CreateButton(panel.transform, "Pause", new Vector2(144f, -184f), new Vector2(120f, 38f), PauseSelected);
        statusText = CreateLabel(panel.transform, "Seleccionado: Laser", new Vector2(16f, -220f), new Vector2(248f, 24f), 16, TextAnchor.MiddleLeft);
    }

    Button CreateButton(Transform parent, string label, Vector2 position, Vector2 size, Action action)
    {
        GameObject buttonObject = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.13f, 0.2f, 0.3f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => action());
        CreateLabel(buttonObject.transform, label, Vector2.zero, size, 18, TextAnchor.MiddleCenter);
        return button;
    }

    Text CreateLabel(Transform parent, string label, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(label + " Label", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.font.RequestCharactersInTexture(label, fontSize, FontStyle.Normal);
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    void RefreshUI()
    {
        if (effects == null || selectionButtons == null) return;

        for (int i = 0; i < selectionButtons.Length; i++)
            selectionButtons[i].GetComponent<Image>().color = i == selectedIndex
                ? new Color(0.1f, 0.42f, 0.68f, 1f)
                : new Color(0.13f, 0.2f, 0.3f, 1f);

        VfxEffectBase effect = SelectedEffect;
        if (effect == null)
        {
            statusText.text = "Efecto no asignado";
            playButton.interactable = false;
            return;
        }

        string state = effect.IsPaused ? "Pausado" : effect.IsPlaying ? "Reproduciendo" : "Detenido";
        string status = "Seleccionado: " + effectNames[selectedIndex] + "  |  " + state;
        if (statusText.text != status)
        {
            statusText.text = status;
            statusText.font.RequestCharactersInTexture(status, statusText.fontSize, statusText.fontStyle);
        }

        Text playLabel = playButton.GetComponentInChildren<Text>();
        string playText = effect.IsPaused ? "Reanudar" : "Play";
        if (playLabel.text != playText)
        {
            playLabel.text = playText;
            playLabel.font.RequestCharactersInTexture(playText, playLabel.fontSize, playLabel.fontStyle);
        }
        playButton.interactable = true;
    }
}
