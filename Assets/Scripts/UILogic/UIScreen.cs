// Scripts/UI/Core/UIScreen.cs (предлагаю создать папку Core или Base для таких классов)
using UnityEngine;
using System;

public abstract class UIScreen : MonoBehaviour
{
    // CanvasGroup опционален, но часто полезен даже без анимаций
    // для управления interactable и blocksRaycasts.
    // Если он вам точно не нужен сейчас, можете убрать.
    [SerializeField] protected CanvasGroup canvasGroup;

    // Флаг, показывающий, активен ли экран в данный момент
    // (не путать с gameObject.activeSelf, т.к. мы можем "показать" экран,
    // который уже был активен, но не видим по логике UIManager)
    public bool IsVisible { get; protected set; }


    protected virtual void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                // Добавляем CanvasGroup, если его нет, так как он часто полезен
                // даже для простого управления interactable и blocksRaycasts.
                // Если вы точно не хотите его использовать, закомментируйте эту строку.
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    /// <summary>
    /// Вызывается, когда экран должен стать видимым и активным.
    /// </summary>
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

    /// <summary>
    /// Вызывается, когда экран должен быть скрыт.
    /// </summary>
    /// <param name="instant">Если true, скрытие произойдет мгновенно без учета анимаций (для текущей версии неактуально, но полезно для будущего).</param>
    public virtual void Hide(bool instant = true, Action onHidden = null)
    {
        // В текущей версии "instant" не влияет, т.к. нет анимаций.
        // Оставляем для совместимости с будущим.
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        IsVisible = false;
        OnHide(); // Вызов кастомной логики для дочерних классов
        gameObject.SetActive(false); // Деактивируем GameObject после всех операций
        onHidden?.Invoke();
    }

    /// <summary>
    /// Пользовательский метод, вызываемый после того, как экран был показан.
    /// Переопределите в дочерних классах для специфической логики при показе.
    /// </summary>
    protected virtual void OnShow()
    {
        // Например, здесь можно обновлять данные на экране
        // Debug.Log($"{gameObject.name} screen shown.");
    }

    /// <summary>
    /// Пользовательский метод, вызываемый перед тем, как экран будет скрыт.
    /// Переопределите в дочерних классах для специфической логики при скрытии.
    /// </summary>
    protected virtual void OnHide()
    {
        // Например, здесь можно сбрасывать состояние или отписываться от событий
        // Debug.Log($"{gameObject.name} screen hidden.");
    }
}