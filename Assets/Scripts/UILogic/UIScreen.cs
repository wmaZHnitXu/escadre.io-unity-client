// Scripts/UI/UIScreen.cs
using UnityEngine;
using System;

public abstract class UIScreen : MonoBehaviour
{

    [SerializeField] protected CanvasGroup canvasGroup;

    public bool IsVisible { get; protected set; }


    protected virtual void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {

                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    public virtual void Show(Action onShown = null)
    {
        gameObject.SetActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        IsVisible = true;
        OnShow(); // Вызов кастомной логики для дочерних классов
        onShown?.Invoke();
    }

    public virtual void Hide(bool instant = true, Action onHidden = null)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        IsVisible = false;
        OnHide();
        gameObject.SetActive(false);
        onHidden?.Invoke();
    }

    protected virtual void OnShow()
    {
        
    }

    protected virtual void OnHide()
    {
    
    }
}