﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TempGridItem : MonoBehaviour
{
    [SerializeField]
    private RectTransform m_rectTransform = null;
    public RectTransform ItemRectTransform => m_rectTransform;

    [SerializeField]
    private CanvasGroup m_canvasGroup = null;

    [SerializeField]
    private Text m_label = null;

    private Vector2 m_itemSize = default;
    public Vector2 ItemSize => m_itemSize;

    public void Setup(in string name)
    {
        m_canvasGroup.alpha = 1.0f;
        m_label.text = name;
    }

    public void Hide()
    {
        m_canvasGroup.alpha = 0.0f;
    }

    public virtual void RefreshUI()
    {
        // to refresh the data from data to UI
    }

    public void RefreshSize()
    {
        m_itemSize = new Vector2(m_rectTransform.rect.width, m_rectTransform.rect.height);
    }

    public void SetSize(Vector2 size)
    {
        // check if it work or not
        m_itemSize = size;
        m_rectTransform.sizeDelta = size;
    }

    #region mono method

    private void Reset()
    {
        m_rectTransform = this.GetComponent<RectTransform>();
        m_itemSize = new Vector2(m_rectTransform.rect.width, m_rectTransform.rect.height);
    }

    private void Awake()
    {
        m_rectTransform = this.GetComponent<RectTransform>();
        m_itemSize = new Vector2(m_rectTransform.rect.width, m_rectTransform.rect.height);
        m_label.text = this.GetInstanceID().ToString();
    }

    #endregion 

}
